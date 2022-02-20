using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace AAM.Processing
{
    public class MapPawnProcessor
    {
        public readonly Map Map;

        public double ProcessAverageInterval;
        public double LastListUpdateTimeMS;
        public double LastProcessTimeMS;
        public int TargetProcessedPawnCount;
        public int LastProcessedPawnCount;
        public int LastTargetsConsideredCount;
        public int LastAnimationsConsideredCount;
        public int LastCellsConsideredCount;
        public uint TotalThreadedExceptionCount;

        private int lastIndex;
        private readonly Stopwatch sw = new Stopwatch();
        private readonly Stopwatch sw2 = new Stopwatch();
        private readonly Stopwatch sw3 = new Stopwatch();
        private readonly List<AnimationStartParameters> tempAnimationStarts = new List<AnimationStartParameters>(64);
        private readonly List<Pawn> pawnList = new List<Pawn>();
        private List<Pawn> pawnListWrite;

        private uint tick;

        public MapPawnProcessor(Map map)
        {
            this.Map = map;
            sw3.Start();
        }

        public IReadOnlyList<Pawn> GetPawnList() => pawnList;

        public void UpdatePawnList()
        {
            sw2.Restart();
            try
            {
                pawnListWrite.Clear();
                for (int i = 0; i < Map.mapPawns.AllPawnsSpawned.Count; i++)
                {
                    // Using foreach loop reduces risk of exception from concurrent write - but the risk is still very much there.
                    var pawn = Map.mapPawns.AllPawnsSpawned[i];

                    // Not de-spawned, dead or downed.
                    if (pawn.Dead || pawn.Downed)
                        continue;

                    // Not an animal, must have tool user intelligence.
                    if (pawn.RaceProps.Animal || pawn.RaceProps.intelligence < Intelligence.ToolUser)
                        continue;

                    // Must be holding a valid melee weapon.
                    // Note: the other checks are still necessary because things like hauling bots can technically hold weapons.
                    if (pawn.GetFirstMeleeWeapon() == null)
                        continue;

                    var data = pawn.GetMeleeData();
                    if (data == null)
                        continue;

                    pawnListWrite.Add(pawn);
                }
            }
            catch (Exception e)
            {
                Core.Error("Exception in map pawn processor thread:", e);
                TotalThreadedExceptionCount++;
            }
            finally
            {
                sw2.Stop();
                LastListUpdateTimeMS = sw2.Elapsed.TotalMilliseconds;
            }
        }

        private void TickUpdatePawnList()
        {
            if (tick % Core.Settings.PawnProcessorTickInterval != 0)
                return;

            // Run sync.
            pawnListWrite = pawnList;
            UpdatePawnList();
        }

        public void Tick()
        {
            tick++;

            TickUpdatePawnList();

            var list = GetPawnList();
            if (list == null || list.Count == 0)
                return;

            int processed = 0;
            int max = list.Count;
            int index = lastIndex;
            if (index >= max)
                index = 0;
            sw.Restart();

            LastTargetsConsideredCount = 0;
            LastAnimationsConsideredCount = 0;
            LastCellsConsideredCount = 0;

            for (int i = 0; i < max; i++)
            {
                // Profiling.
                if (index == 0)
                {
                    ProcessAverageInterval = sw3.Elapsed.TotalMilliseconds;
                    sw3.Restart();
                }

                Process(list[index]);
                processed++;
                index++;
                if (index >= max)
                    index = 0;

                if (sw.Elapsed.TotalMilliseconds >= Core.Settings.MaxCPUTimePerTick)
                    break;
            }

            sw.Stop();

            //Core.Log($"Processed {processed}: from {lastIndex} to {lastIndex} [)");
            LastProcessTimeMS = sw.Elapsed.TotalMilliseconds;
            TargetProcessedPawnCount = max;
            LastProcessedPawnCount = processed;
            lastIndex = index;
        }

        private bool IsPlayerControlled(Pawn pawn) => pawn.IsColonistPlayerControlled;

        // TODO add better checks - including mod settings.
        private bool ShouldAutoGrapple(Pawn pawn) => false;//!IsPlayerControlled(pawn) || pawn.GetMeleeData().ResolvedAutoGrapple;
        private bool ShouldAutoExecute(Pawn pawn) => !IsPlayerControlled(pawn) || pawn.GetMeleeData().ResolvedAutoExecute;

        public void Process(Pawn pawn)
        {
            if (pawn.Dead || pawn.Downed || !pawn.Spawned || pawn.IsInAnimation() || pawn.InMentalState)
                return;

            // TODO check that:
            // Not in active melee combat (in the case of grappling)...

            Map map = pawn.Map;
            IntVec3 pawnPos = pawn.Position;
            bool exec = ShouldAutoExecute(pawn);
            bool grapple = ShouldAutoGrapple(pawn);
            var meleeWeapon = pawn.GetFirstMeleeWeapon();
            tempAnimationStarts.Clear();

            if (exec && !grapple)
            {
                // Can execute, but cannot grapple.
                // We only need to consider the immediate left and right of the pawn.

                var allowedExecutions = AnimDef.GetExecutionAnimationsForWeapon(meleeWeapon.def); // TODO cache.

                IntVec3 left  = pawnPos - new IntVec3(1, 0, 0);
                IntVec3 right = pawnPos + new IntVec3(1, 0, 0);

                // Make space mask. This is an int where each bit represents the 'un-standability' of a cell in a 7x7 area surrounding the pawn.
                // Each anim def has a corresponding space requirement mask, where each bit is 1 if that cell is required to be standable.
                // By combining the masks (very fast), we check if there is enough space.
                ulong spaceMask = SpaceChecker.MakeOccupiedMask(map, pawnPos);
                LastCellsConsideredCount += 49;

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                void CheckAllAnimations(Pawn victim, bool flipX)
                {
                    foreach(var def in allowedExecutions)
                    {
                        LastAnimationsConsideredCount++;

                        // Check for space.
                        if ((spaceMask & (flipX ? def.ClearMask : def.FlipClearMask)) != 0)
                            continue;

                        tempAnimationStarts.Add(new AnimationStartParameters(def, pawn, victim){FlipX = flipX});
                    }
                }

                // For each target...
                foreach (var target in map.attackTargetsCache.GetPotentialTargetsFor(pawn))
                {
                    if (target.Thing is not Pawn p)
                        continue;

                    LastTargetsConsideredCount++;

                    if (!CanBeExecutedNow(pawn, p))
                        continue;

                    var pos = p.Position;

                    if (pos == right)
                        CheckAllAnimations(p, false);
                    else if (pos == left)
                        CheckAllAnimations(p, true);
                }

                // TODO work with list of possible animations here...
                if (tempAnimationStarts.Count > 0)
                {
                    foreach (var start in tempAnimationStarts)
                        Core.Log(start.ToString());

                    tempAnimationStarts.RandomElement().TryTrigger();
                }
            }
        }

        private bool CanBeExecutedNow(Pawn executioner, Pawn victim)
        {
            // TODO check mod settings...
            return !victim.Dead && !victim.Downed && victim.Spawned && !victim.IsInAnimation() && AttackTargetFinder.IsAutoTargetable(victim);
        }
    }
}
