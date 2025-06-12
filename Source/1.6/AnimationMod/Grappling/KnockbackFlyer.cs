using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace AM.Grappling;

public class KnockbackFlyer : PawnFlyer
{
    [DebugAction("Melee Animation", actionType = DebugActionType.ToolMapForPawns)]
    private static void TestKnockback(Pawn victim)
    {
        var end = GetEndCell(victim, IntVec3.East, 20);
        MakeKnockbackFlyer(victim, end);
    }

    public static IntVec3 GetEndCell(Pawn victim, in IntVec3 direction, int maxRange)
    {
        var map = victim.Map;
        IntVec3 end = victim.Position + direction * maxRange;
        IntVec3 ret = victim.Position;
        foreach (var cell in GetCellsFromTo(victim.Position, end))
        {
            ret = cell;
            if (IsSolid(victim, cell, map))
                break;
        }

        return ret;
    }

    public static KnockbackFlyer MakeKnockbackFlyer(Pawn victim, IntVec3 targetPos)
    {
        if (!victim.Spawned)
            return null;

        var map = victim.Map;
        var start = victim.DrawPos;
        var end = targetPos.ToVector3ShiftedWithAltitude(start.y);

        var soundDef = SoundDefOf.Pawn_Melee_Punch_HitBuilding_Generic;

        KnockbackFlyer flyer = MakeFlyer(AM_DefOf.AM_KnockbackFlyer, victim, targetPos, EffecterDefOf.ConstructDirt, soundDef) as KnockbackFlyer;

        if (flyer?.FlyingPawn != null)
        {
            flyer.StartPos = start;
            flyer.EndPos = end;
            victim.Rotation = flyer.GetPawnRotation();

            // Important: clear victim job queue, which is normally the animation job.
            // Failing to do this means that the animation job is referenced when saving
            // but will not be found when loading.
            // This normally hard crashes the game!
            flyer.jobQueue = null;

            var t = GenSpawn.Spawn(flyer, targetPos, map);
            if (t == null)
                flyer.RespawnPawn();
            
            return t != null ? flyer : null;
        }

        return null;
    }

    private static bool IsSolid(Pawn pawn, in IntVec3 cell, Map map)
    {
        if (cell.x < 0 || cell.z < 0 || cell.x >= map.info.Size.x || cell.z >= map.info.Size.z)
            return true;

        var things = map.thingGrid.ThingsListAtFast(cell);
        return Enumerable.Any(things, thing => thing.BlocksPawn(pawn));
    }

    private static IEnumerable<IntVec3> GetCellsFromTo(IntVec3 from, IntVec3 to)
    {
        bool hor = from.x != to.x;
        if (hor)
        {
            int dir = Mathf.Clamp(to.x - from.x, -1, 1);
            for (int x = from.x + dir; x <= to.x; x += dir)
            {
                yield return new IntVec3(x, from.y, from.z);
            }
        }
        else
        {
            int dir = Mathf.Clamp(to.z - from.z, -1, 1);
            for (int z = from.z + dir; z <= to.z; z += dir)
            {
                yield return new IntVec3(from.x, from.y, z);
            }
        }
    }

    public override Vector3 DrawPos => Vector3.Lerp(StartPos, EndPos, (float)ticksFlying / ticksFlightTime);

    public Vector3 StartPos, EndPos;
    public Effecter FlightEffecter;

    public Rot4 GetPawnRotation()
    {
        float dx = EndPos.x - StartPos.x;
        float dz = EndPos.z - StartPos.z;

        // Ungodly switch statement.
        // ReSharper suggested it and I kind of like how it looks.

        return dx switch
        {
            < 0 => Rot4.East,
            > 0 => Rot4.West,
            _ => dz switch
            {
                < 0 => Rot4.North,
                > 0 => Rot4.South,
                _ => Rot4.South
            }
        };
    }

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);

        if (!respawningAfterLoad)
        {
            ticksFlightTime = (int)((StartPos.ToFlat() - EndPos.ToFlat()).magnitude * 3f);
        }
    }

    public override void DrawAt(Vector3 drawLoc, bool flip = false)
    {
        FlyingPawn.DrawAt(drawLoc, flip);
    }

    public override void RespawnPawn()
    {
        var p = FlyingPawn;
        base.RespawnPawn();
        this.LandingEffects();
        p.Rotation = GetPawnRotation();
        p.stances.stunner.StunFor(30, null, false, true);
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref StartPos, "startPos");
        Scribe_Values.Look(ref EndPos, "endPos");
    }

    public override void Tick()
    {
        base.Tick();

        //if (FlightEffecter == null)
        //{
        //    //FlightEffecter = def.pawnFlyer.flightEffecterDef.Spawn();
        //    FlightEffecter = EffecterDefOf..Spawn();
        //    FlightEffecter.Trigger(new TargetInfo(DrawPos.ToIntVec3(), Map), TargetInfo.Invalid);
        //}
        //else
        //{
        //    for (int i = 0; i < 1; i++)
        //    {
        //        FlightEffecter.EffectTick(new TargetInfo(DrawPos.ToIntVec3(), Map), TargetInfo.Invalid);
        //    }
        //}

        //FleckMaker.ThrowDustPuff(DrawPos, Map, 2f);

        var loc = DrawPos;
        loc.z -= 0.4f;
        loc.y = AltitudeLayer.FloorCoverings.AltitudeFor();
        var map = Map;
        float scale = 0.6f;

        if (!loc.ShouldSpawnMotesAt(map))
        {
            return;
        }
        FleckCreationData dataStatic = FleckMaker.GetDataStatic(loc, map, FleckDefOf.DustPuff, 1.9f * scale);
        dataStatic.rotationRate = (float)Rand.Range(-60, 60);
        dataStatic.velocityAngle = (float)Rand.Range(0, 360);
        dataStatic.velocitySpeed = Rand.Range(0.6f, 0.75f);
        map.flecks.CreateFleck(dataStatic);
    }

    private new void LandingEffects()
    {
        soundLanding.PlayOneShot(new TargetInfo(EndPos.ToIntVec3(), Map));

        for (int i = 0; i < 5; i++)
        {
            FleckMaker.ThrowDustPuffThick(EndPos + Gen.RandomHorizontalVector(0.5f), Map, 2f, Color.grey);
            FleckMaker.ThrowDustPuff(EndPos + Gen.RandomHorizontalVector(0.5f), Map, 2f);
        }
    }

    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        FlightEffecter?.Cleanup();
        base.Destroy(mode);
    }
}