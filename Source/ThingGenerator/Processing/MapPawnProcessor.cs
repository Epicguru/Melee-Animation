using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly List<Pawn> pawnListThreaded = new List<Pawn>();
        private readonly List<Pawn> pawnList = new List<Pawn>();
        private List<Pawn> pawnListWrite;
        private Task runningUpdateTask;

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

            // Threaded mode....
            if (Core.Settings.Multithread)
            {
                if (runningUpdateTask is { IsCompleted: false })
                {
                    Core.Warn("MapPawnProcessor is still running threaded updated, even though a new one needs to be done now! Forcing thread join...");
                    runningUpdateTask.Wait();
                }

                pawnListWrite = pawnListThreaded;
                runningUpdateTask = Task.Run(UpdatePawnList);
                return;
            }
            
            // Regular, sync mode.

            // Clean up async mode if that was previously active...
            if (runningUpdateTask is { IsCompleted: false })
                runningUpdateTask.Wait();
            runningUpdateTask = null;

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
            int mapWidth = map.Size.x;
            int mapHeight = map.Size.z;
            IntVec3 pawnPos = pawn.Position;
            bool exec = ShouldAutoExecute(pawn);
            bool grapple = ShouldAutoGrapple(pawn);
            var meleeWeapon = pawn.GetFirstMeleeWeapon();
            tempAnimationStarts.Clear();

            if (exec && !grapple)
            {
                var allowedExecutions = AnimDef.GetExecutionAnimationsForWeapon(meleeWeapon.def); // TODO cache?
                // Is it even worth checking for strange positioning with the animations?

                IEnumerable<AnimationStartParameters> Check(AnimDef def, bool flipX)
                {
                    // Check that the animation has enough space to start.
                    foreach (var cell in def.GetMustBeClearCells(flipX, false, pawnPos))
                    {
                        LastCellsConsideredCount++;
                        if (!SpaceChecker.IsValidPawnPosFast(map, mapWidth, mapHeight, cell))
                            yield break;
                    }

                    // Get the animation start cell. Might be good to cache, along with other cell calculations.
                    var rawS = def.TryGetCell(AnimCellData.Type.PawnStart, flipX, false, 1);
                    if (rawS == null)
                        yield break;

                    var start = rawS.Value.ToIntVec3 + pawnPos;

                    // For each potential target...
                    //Core.Log($"{pawn} has {Map.attackTargetsCache.GetPotentialTargetsFor(pawn).Count} targets");
                    foreach (var target in Map.attackTargetsCache.GetPotentialTargetsFor(pawn))
                    {
                        if (target.Thing is not Pawn p)
                            continue;
                        LastTargetsConsideredCount++;

                        // TODO AUTO ATTACK CHECK.

                        // Filter out invalid targets.
                        if (!CanBeExecutedNow(pawn, p))
                            continue;

                        // Are they standing in the right position for the animation to start?
                        if (p.Position != start)
                            continue;

                        // Return it as a possibility.
                        yield return new AnimationStartParameters(def, pawn, p)
                        {
                            FlipX = flipX
                        };
                    }
                }

                // For all possible animations with this weapon...
                foreach (var e in allowedExecutions)
                {
                    LastAnimationsConsideredCount++;
                    // Check the regular animation.
                    foreach (var item in Check(e, false))
                        tempAnimationStarts.Add(item);

                    // Check the animation mirrored. No vertical support...
                    foreach (var item in Check(e, true))
                        tempAnimationStarts.Add(item);
                }

                // TODO work with list of possible animations here...
                if (tempAnimationStarts.Count > 0)
                {
                    if (map == Find.CurrentMap)
                        Core.Log($"{pawn} can do {tempAnimationStarts.Count} animations.");
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
