using AAM.Data;
using AAM.Grappling;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Verse;
using Verse.AI;

namespace AAM.Processing
{
    public class MapPawnProcessor
    {
        static readonly IntVec3[] AdjacentExceptLR = new IntVec3[]
        {
            new IntVec3(1, 0, 1),
            new IntVec3(-1, 0, 1),
            new IntVec3(1, 0, -1),
            new IntVec3(-1, 0, -1),
            new IntVec3(0, 0, 1),
            new IntVec3(0, 0, -1)
        };
        static readonly uint[] AdjacentExceptLR_Masks = new uint[]
        {
            MakeMask(AdjacentExceptLR[0]),
            MakeMask(AdjacentExceptLR[1]),
            MakeMask(AdjacentExceptLR[2]),
            MakeMask(AdjacentExceptLR[3]),
            MakeMask(AdjacentExceptLR[4]),
            MakeMask(AdjacentExceptLR[5])
        };
        static readonly IntVec3[] Adjacent = new IntVec3[]
        {
            new IntVec3(1, 0, 0),
            new IntVec3(-1, 0, 0),
            new IntVec3(1, 0, 1),
            new IntVec3(-1, 0, 1),
            new IntVec3(1, 0, -1),
            new IntVec3(-1, 0, -1),
            new IntVec3(0, 0, 1),
            new IntVec3(0, 0, -1)
        };
        static readonly uint[] Adjacent_Masks = new uint[]
        {
            MakeMask(Adjacent[0]),
            MakeMask(Adjacent[1]),
            MakeMask(Adjacent[2]),
            MakeMask(Adjacent[3]),
            MakeMask(Adjacent[4]),
            MakeMask(Adjacent[5]),
            MakeMask(Adjacent[6]),
            MakeMask(Adjacent[7])
        };

        private static uint MakeMask(in IntVec3 offset) => (uint)1 << (offset.x + 1) + (offset.z + 1) * 3;

        public struct PossibleGrapple
        {
            public Pawn Victim;
            public IntVec3 GrappleEndPos;
        }

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
        private readonly List<PossibleGrapple> tempGrappleStarts = new List<PossibleGrapple>(64);
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

            int currentTick = GenTicks.TicksAbs;
            for (int i = 0; i < max; i++)
            {
                // Profiling.
                if (index == 0)
                {
                    ProcessAverageInterval = sw3.Elapsed.TotalMilliseconds;
                    sw3.Restart();
                }

                Pawn pawn = list[index];
                Process(pawn);

                var data = pawn.GetMeleeData();
                if (data != null)
                    data.lastTickPresentedOptions = currentTick;

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsPlayerControlled(Pawn pawn) => pawn.IsColonistPlayerControlled;

        // TODO add better checks - including mod settings.
        private bool ShouldAutoGrapple(Pawn pawn, PawnMeleeData data) => (!IsPlayerControlled(pawn) || data.ResolvedAutoGrapple) && data.IsGrappleOffCooldown(5f);
        private bool ShouldAutoExecute(Pawn pawn, PawnMeleeData data) => (!IsPlayerControlled(pawn) || data.ResolvedAutoExecute) && data.IsExecutionOffCooldown(5f);

        public void Process(Pawn pawn)
        {
            if (pawn.Dead || pawn.Downed || !pawn.Spawned || pawn.IsInAnimation() || pawn.InMentalState)
                return;

            var data = pawn.GetMeleeData();
            bool exec = ShouldAutoExecute(pawn, data);
            bool grapple = ShouldAutoGrapple(pawn, data);
            if (!exec && !grapple)
            {
                // Should never happen...
                return;
            }

            Map map = pawn.Map;
            IntVec3 pawnPos = pawn.Position;
            IntVec3 left = pawnPos - new IntVec3(1, 0, 0);
            IntVec3 right = pawnPos + new IntVec3(1, 0, 0);
            var meleeWeapon = pawn.GetFirstMeleeWeapon();
            tempAnimationStarts.Clear();
            tempGrappleStarts.Clear();

            var allowedExecutions = AnimDef.GetExecutionAnimationsForWeapon(meleeWeapon.def); // TODO cache.

            ulong spaceMask = 0;
            uint smallSpaceMask = 0;
            if (exec)
            {
                // Make space mask. This is an int where each bit represents the 'un-standability' of a cell in a 7x7 area surrounding the pawn.
                // Each anim def has a corresponding space requirement mask, where each bit is 1 if that cell is required to be standable.
                // By combining the masks (very fast), we check if there is enough space.
                // Also make a smaller mask representing a 3x3 area, used for grappling calculations.
                spaceMask = SpaceChecker.MakeOccupiedMask(map, pawnPos, out smallSpaceMask);
                LastCellsConsideredCount += 49;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void CheckAllAnimations(Pawn victim, bool flipX)
            {
                foreach (var def in allowedExecutions)
                {
                    LastAnimationsConsideredCount++;

                    // Check for space.
                    if ((spaceMask & (flipX ? def.ClearMask : def.FlipClearMask)) != 0)
                        continue;

                    tempAnimationStarts.Add(new AnimationStartParameters(def, pawn, victim) { FlipX = flipX });
                }
            }

            if (exec && !grapple)
            {
                // Can execute, but cannot grapple.
                // We only need to consider the immediate left and right of the pawn.

                // For each target...
                foreach (var target in map.attackTargetsCache.GetPotentialTargetsFor(pawn))
                {
                    if (target.Thing is not Pawn p)
                        continue;

                    LastTargetsConsideredCount++;

                    if (!CanBeExecutedOrGrappled(pawn, p))
                        continue;

                    var pos = p.Position;

                    if (pos == right)
                        CheckAllAnimations(p, false);
                    else if (pos == left)
                        CheckAllAnimations(p, true);
                }

                if (tempAnimationStarts.Count > 0)
                {
                    OnPossibleImmediateExecutions(pawn, data);
                }
            }
            else
            {
                // Must be grappling, might be executing.
                // Check each enemy:
                // Are they in grapple range?
                // if(exec) Are they in exec position?
                // if(inGrappleRange) Do we have LOS from any/all positions?
                // if(hasLOS) Pull in and execute or just pull in.

                // For each target...
                foreach (var target in map.attackTargetsCache.GetPotentialTargetsFor(pawn))
                {
                    if (target.Thing is not Pawn p)
                        continue;

                    LastTargetsConsideredCount++;
                    if (!CanBeExecutedOrGrappled(pawn, p))
                        continue;

                    // Check range and exclude.
                    var pos = p.Position;

                    const float maxGrappleRadiusSqr = 21.4f * 21.4f; // 20 + 1.414 from the corner grapple.
                    float sqrDst = pos.DistanceToSquared(pawnPos);
                    if (sqrDst > maxGrappleRadiusSqr)
                        continue;

                    // If executing, is it in the perfect spot?
                    if (exec)
                    {
                        if (pos == right)
                        {
                            CheckAllAnimations(p, false);
                            continue; // Can't grapple if they are already in target pos...
                        }
                        if (pos == left)
                        {
                            CheckAllAnimations(p, true);
                            continue;
                        }
                    }

                    // Check LOS for all possible spots...
                    byte mode = !exec ? (byte)0 : (byte)1; // Mode 2 would cause them to only be grappled if they can go in the execution spot...
                    var foundPos = GetGrappleTargetPosition(pawnPos, smallSpaceMask, map, p, mode);
                    if (foundPos != null)
                        tempGrappleStarts.Add(new PossibleGrapple() { GrappleEndPos = foundPos.Value, Victim = p });
                }

                // First consider the immediate executions, if any...
                if (tempAnimationStarts.Count > 0)
                {
                    if (OnPossibleImmediateExecutions(pawn, data))
                        return;
                }

                // ... and if none of those are acted upon, consider the possible grapples.
                if (tempGrappleStarts.Count > 0)
                {
                    OnPossibleGrapples(pawn, data);
                }
            }
        }

        private IntVec3? GetGrappleTargetPosition(IntVec3 execPos, uint execMask, Map map, Pawn target, byte mode)
        {
            // TODO figure out fast way to sort grappling target positions based on heuristic offset.
            // Avoid sqrt if possible (so no normalizing vectors for dot product, or arctangent).

            const uint LEFT_AND_RIGHT = ((uint)1 << 3) | ((uint)1 << 5);
            uint leftAndRight = LEFT_AND_RIGHT & ~execMask;
            var left = execPos - new IntVec3(1, 0, 0);
            var right = execPos + new IntVec3(1, 0, 0);

            switch (mode)
            {
                case 0:
                    // Grapple only.
                    // TODO make better algorithm.
                    for (int i = 0; i < Adjacent_Masks.Length; i++)
                    {
                        var pos = Adjacent[i] + execPos;
                        if ((execMask & Adjacent_Masks[i]) == 0 && GenSight.LineOfSightToThing(pos, target, map))
                            return pos;
                    }

                    return null;

                case 1:
                    // Grapple, prefer execution.
                    // Prefer execution spots.
                    switch (leftAndRight)
                    {
                        case 0:
                            // No free spots.
                            break;

                        case 8:

                            // Left is free, right is not.
                            if (GenSight.LineOfSightToThing(left, target, map))
                                return left;
                            break;

                        case 32:
                            // Right is free, left is not.
                            if (GenSight.LineOfSightToThing(right, target, map))
                                return right;
                            break;

                        case 40:
                            // Both are free.
                            bool isToRight = execPos.x < target.Position.x;
                            if (isToRight && GenSight.LineOfSightToThing(right, target, map))
                                return right;
                            if (GenSight.LineOfSightToThing(left, target, map))
                                return left;
                            break;
                    }

                    // Failed to find left and right...
                    // Check the other spots.
                    // TODO make better algorithm.
                    for(int i = 0; i < AdjacentExceptLR_Masks.Length; i++)
                    {
                        var pos = AdjacentExceptLR[i] + execPos;
                        if ((execMask & AdjacentExceptLR_Masks[i]) == 0 && GenSight.LineOfSightToThing(pos, target, map))
                            return pos;
                    }

                    return null;

                case 2:
                    // Only execution spots.

                    switch (leftAndRight)
                    {
                        case 0:
                            // No free spots.
                            return null;

                        case 8:
                            
                            // Left is free, right is not.
                            return GenSight.LineOfSightToThing(left, target, map) ? left : null;

                        case 32:
                            // Right is free, left is not.
                            return GenSight.LineOfSightToThing(right, target, map) ? right : null;

                        case 40:
                            // Both are free.
                            bool isToRight = execPos.x < target.Position.x;
                            if (isToRight && GenSight.LineOfSightToThing(right, target, map))
                                return right;
                            if (GenSight.LineOfSightToThing(left, target, map))
                                return left;
                            return null;
                    }

                    return null;

                default:
                    return null;
            }
        }

        private bool OnPossibleImmediateExecutions(Pawn executioner, PawnMeleeData data)
        {
            int tickDelta = data.lastTickPresentedOptions < 0 ? 1 : (GenTicks.TicksAbs - data.lastTickPresentedOptions);
            if (tickDelta == 0)
                return false;

            // This tick delta can be used to calculate probability independently of time.
            // i.e. 1% chance per tick to execute, goes up the larger the tick interval.

            // TODO this isn't actually time-independent and also isn't very controllable.

            const float chancePerSecond = 0.5f;
            const float chancePerTick = chancePerSecond / 60f;
            float chance = chancePerTick * tickDelta;

            if (!Rand.Chance(chance))
                return false;

            var execution = tempAnimationStarts.RandomElementByWeight(d => d.Animation.relativeProbability);
            bool worked = execution.TryTrigger();
            if (worked)
                data.TimeSinceExecuted = 0;

            return worked;
        }

        private bool OnPossibleGrapples(Pawn executioner, PawnMeleeData data)
        {
            int tickDelta = data.lastTickPresentedOptions < 0 ? 1 : (GenTicks.TicksAbs - data.lastTickPresentedOptions);
            if (tickDelta == 0)
                return false;

            const float chancePerSecond = 0.1f; // 10% chance each second...
            const float chancePerTick = chancePerSecond / 60f;
            float chance = chancePerTick * tickDelta;

            Core.Log($"Final chance: {chance * 100f}% ({tickDelta})");
            if (!Rand.Chance(chance))
                return false;

            var rand = tempGrappleStarts.RandomElement();
            bool worked = JobDriver_GrapplePawn.GiveJob(executioner, rand.Victim, rand.GrappleEndPos);
            if (worked)
                data.TimeSinceGrappled = 0;

            return worked;
        }

        private bool CanBeExecutedOrGrappled(Pawn executioner, Pawn victim)
        {
            // TODO check mod settings...
            return !victim.Dead && !victim.Downed && victim.Spawned && !victim.IsInAnimation() && AttackTargetFinder.IsAutoTargetable(victim);
        }
    }
}
