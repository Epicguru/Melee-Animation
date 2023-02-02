using AAM.Data;
using AAM.Events;
using AAM.Events.Workers;
using AAM.Tweaks;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace AAM;

public static class Extensions
{
    /// <summary>
    /// Shorthand for <paramref name="str"/>.Translate().
    /// </summary>
    public static TaggedString Trs(this string str) => str.Translate();

    /// <summary>
    /// Shorthand for <paramref name="str"/>.Translate().
    /// </summary>
    public static TaggedString Trs(this string str, params NamedArgument[] args) => TranslatorFormattedStringExtensions.Translate(str, args);

    public static AnimationManager GetAnimManager(this Map map)
        => map?.GetComponent<AnimationManager>();

    public static AnimationManager GetAnimManager(this Pawn pawn)
        => pawn?.Map?.GetComponent<AnimationManager>();

    public static Matrix4x4 MakeAnimationMatrix(this Pawn pawn)
        => Matrix4x4.TRS(pawn.Position.ToVector3ShiftedWithAltitude(pawn.DrawPos.y), Quaternion.identity, Vector3.one);

    public static Matrix4x4 MakeAnimationMatrix(this in LocalTargetInfo target)
        => Matrix4x4.TRS(new Vector3(target.CenterVector3.x, AltitudeLayer.Pawn.AltitudeFor(), target.CenterVector3.z), Quaternion.identity, Vector3.one);

    public static bool IsInAnimation(this Pawn pawn)
        => AnimRenderer.TryGetAnimator(pawn) != null;

    public static bool IsInAnimation(this Pawn pawn, out AnimRenderer animRenderer)
        => (animRenderer = AnimRenderer.TryGetAnimator(pawn)) != null;

    public static bool IsInActiveMeleeCombat(this Pawn pawn)
        => pawn.jobs?.curDriver is JobDriver_AttackMelee or JobDriver_AttackStatic;

    public static AnimRenderer TryGetAnimator(this Pawn pawn) => AnimRenderer.TryGetAnimator(pawn);

    public static T AsDefOfType<T>(this string defName, T fallback = null) where T : Def
    {
        if (string.IsNullOrWhiteSpace(defName))
            return fallback;

        return (T)GenGeneric.InvokeStaticMethodOnGenericType(typeof(DefDatabase<>), typeof(T), "GetNamed", defName, true) ?? fallback;
    }

    public static BodyPartRecord TryGetPartFromDef(this Pawn pawn, BodyPartDef def)
    {
        if (def == null)
            return null;
        if (pawn?.health?.hediffSet == null)
            return null;

        foreach (BodyPartRecord bodyPartRecord in pawn.health.hediffSet.GetNotMissingParts())
        {
            if (bodyPartRecord.def == def)
                return bodyPartRecord;
        }
        return null;
    }

    /// <summary>
    /// Attempts to get the equipped melee weapon of this pawn.
    /// That includes sidearms if SimpleSidearms is installed.
    /// This will only return the melee weapon if said melee weapon is compatible with the animation mod (i.e. it has valid tweak data).
    /// </summary>
    public static ThingWithComps GetFirstMeleeWeapon(this Pawn pawn)
    {
        if (pawn?.equipment == null)
            return null;

        if ((pawn.equipment.Primary?.def.IsMeleeWeapon ?? false) && TweakDataManager.TryGetTweak(pawn.equipment.Primary.def) != null)
            return pawn.equipment.Primary;

        foreach(var item in pawn.equipment.AllEquipmentListForReading)
        {
            if (item.def.IsMeleeWeapon && TweakDataManager.TryGetTweak(item.def) != null)
                return item;
        }

        if (Core.IsSimpleSidearmsActive && pawn.inventory?.innerContainer != null)
        {
            foreach (var item in pawn.inventory.innerContainer)
            {
                if (item is ThingWithComps twc && item.def.IsMeleeWeapon && TweakDataManager.TryGetTweak(item.def) != null)
                    return twc;
            }
        }

        return null;
    }

    public static ThingWithComps TryGetLasso(this Pawn pawn)
    {
        if (pawn?.apparel == null)
            return null;

        foreach (var item in pawn.apparel.WornApparel)
        {
            if (item.def.IsApparel && Content.LassoDefs.Contains(item.def))
            {
                return item;
            }
        }

        return null;
    }

    public static PawnMeleeData GetMeleeData(this Pawn pawn) => GameComp.Current?.GetOrCreateData(pawn);

    public static Vector3 ToWorld(this in Vector2 flatVector, float altitude = 0) => new(flatVector.x, altitude, flatVector.y);

    public static Vector2 ToFlat(this in Vector3 worldVector) => new Vector3(worldVector.x, worldVector.z);

    public static T GetWorker<T>(this EventBase e) where T : EventWorkerBase => EventWorkerBase.GetWorker(e.EventID) as T;

    public static float RandomInRange(this in Vector2 range) => Rand.Range(range.x, range.y);

    public static T RandomElementByWeightExcept<T>(this IEnumerable<T> items, Func<T, float> weight, ICollection<T> except) where T : class
    {
        if (items.Count() == except.Count)
            return null;

        for (int i = 0; i < 1000; i++)
        {
            var selected = items.RandomElementByWeightWithFallback(weight);
            if (selected == null)
                return null;

            if (except.Contains(selected))
                continue;

            return selected;
        }

        Core.Error("Ran out of iterations selecting random.");
        return null;
    }

    public static Vector3 AngleToWorldDir(this float angleDeg) => -new Vector3(Mathf.Cos(angleDeg * Mathf.Deg2Rad), 0f, Mathf.Sin(angleDeg * Mathf.Deg2Rad));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GetOccupiedMaskBit(this uint mask, int x, int z) => (((uint)1 << (x + 1) + (z + 1) * 3) & mask) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GetOccupiedMaskBitX(this uint mask, int x) => (((uint)1 << (x + 1) + 3) & mask) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GetOccupiedMaskBitZ(this uint mask, int z) => (((uint)1 << 1 + (z + 1) * 3) & mask) != 0;

    [DebugAction("Advanced Animation Mod", "Spawn all melee weapons", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void GimmeMeleeWeapons()
    {
        var pos = Verse.UI.MouseCell();
        foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            if (def.IsMeleeWeapon)
            {
                try
                {
                    DebugThingPlaceHelper.DebugSpawn(def, pos, 1, false);
                }
                catch (Exception e)
                {
                    Core.Warn($"Failed to spawn {def}: [{e.GetType().Name}] {e.Message}");
                }
            }
        }
    }

    [DebugAction("Advanced Animation Mod", "Spawn army", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnArmy()
    {
        List<DebugMenuOption> list = new();
        foreach (PawnKindDef localKindDef2 in from kd in DefDatabase<PawnKindDef>.AllDefs
                 orderby kd.defName
                 select kd)
        {
            PawnKindDef localKindDef = localKindDef2;
            list.Add(new DebugMenuOption(localKindDef.defName, DebugMenuOptionMode.Tool, delegate()
            {
                var pos = Verse.UI.MouseCell();
                Faction faction = FactionUtility.DefaultFactionFrom(localKindDef.defaultFactionType);

                for (int i = 0; i < 50; i++)
                {
                    Pawn newPawn = PawnGenerator.GeneratePawn(localKindDef, faction);
                    GenSpawn.Spawn(newPawn, Verse.UI.MouseCell(), Find.CurrentMap, WipeMode.Vanish);
                    if (faction != null && faction != Faction.OfPlayer)
                    {
                        Lord lord = null;
                        if (newPawn.Map.mapPawns.SpawnedPawnsInFaction(faction).Any((Pawn p) => p != newPawn))
                        {
                            lord = ((Pawn)GenClosest.ClosestThing_Global(newPawn.Position, newPawn.Map.mapPawns.SpawnedPawnsInFaction(faction), 99999f, (Thing p) => p != newPawn && ((Pawn)p).GetLord() != null, null)).GetLord();
                        }
                        if (lord == null)
                        {
                            LordJob_DefendPoint lordJob = new(newPawn.Position, null, false, true);
                            lord = LordMaker.MakeNewLord(faction, lordJob, Find.CurrentMap, null);
                        }
                        lord.AddPawn(newPawn);
                    }
                }
            }));
        }
        Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
    }
}