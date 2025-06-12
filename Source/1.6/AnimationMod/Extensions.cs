using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AM.Events;
using AM.Events.Workers;
using AM.Hands;
using AM.Idle;
using AM.PawnData;
using AM.Tweaks;
using JetBrains.Annotations;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AM;

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

    public static Matrix4x4 MakeAnimationMatrix(this Pawn pawn, float yOffset = 0)
        => Matrix4x4.TRS(pawn.Position.ToVector3ShiftedWithAltitude(pawn.DrawPos.y + yOffset), Quaternion.identity, Vector3.one);

    public static Matrix4x4 MakeAnimationMatrix(this in LocalTargetInfo target)
        => Matrix4x4.TRS(new Vector3(target.CenterVector3.x, AltitudeLayer.Pawn.AltitudeFor(), target.CenterVector3.z), Quaternion.identity, Vector3.one);

    public static bool IsInAnimation(this Pawn pawn)
        => AnimRenderer.TryGetAnimator(pawn) != null;

    public static bool IsInAnimation(this Pawn pawn, out AnimRenderer animRenderer)
        => (animRenderer = AnimRenderer.TryGetAnimator(pawn)) != null;

    [UsedImplicitly] // Used in Fists Of Fury
    public static bool IsInActiveMeleeCombat(this Pawn pawn)
        => pawn.jobs?.curDriver is JobDriver_AttackMelee or JobDriver_AttackStatic;

    public static AnimRenderer TryGetAnimator(this Pawn pawn) => AnimRenderer.TryGetAnimator(pawn);

    public static T AsDefOfType<T>(this string defName, T fallback = null) where T : Def
    {
        if (string.IsNullOrWhiteSpace(defName))
            return fallback;

        return (T)GenGeneric.InvokeStaticMethodOnGenericType(typeof(DefDatabase<>), typeof(T), "GetNamed", defName, true) ?? fallback;
    }

    /// <summary>
    /// Returns true if <see cref="ThingDef.IsMeleeWeapon"/> is true OR this weapon has been manually marked as a melee weapon
    /// by this mod.
    /// </summary>
    public static bool IsMeleeWeapon(this ThingDef def) => def.IsMeleeWeapon || Core.ForceConsiderTheseMeleeWeapons.Contains(def);

    public static float ToAngleFlatNew(this in Vector3 vector) => Mathf.Atan2(vector.z, vector.x) * Mathf.Rad2Deg;

    /// <summary>
    /// Additional checks to see if the pawn is capable of executing using fists.
    /// Only returns true if the pawn has hands, is humanlike, and Fists of Fury is active.
    /// </summary>
    public static bool IsCapableOfFistExecutions(this Pawn pawn, out string reasonWhyNot)
    {
        // Mod active.
        if (!Core.IsFistsOfFuryActive)
        {
            reasonWhyNot = "[Internal Error] Fists of Fury is not active.";
            return false;
        }
        
        // Check if humanlike.
        if (!pawn.RaceProps.Humanlike)
        {
            reasonWhyNot = "AM.FoF.ReasonCantFistExec.NotHumanlike".Trs(pawn.LabelShortCap);
            return false;
        }

        // Check that the pawn has hands...
        Span<HandInfo> hands = stackalloc HandInfo[2];
        int handCount = HandUtility.GetHandData(pawn, hands);
        if (handCount == 0)
        {
            reasonWhyNot = "AM.FoF.ReasonCantFistExec.MissingHands".Trs(pawn.LabelShortCap);
            return false;
        }
        
        reasonWhyNot = null;
        return true;
    }
    
    public static bool Polarity(this float f) => f > 0;

    public static WeaponCat ToCategory(this MeleeWeaponType type)
    {
        WeaponCat cat = 0;

        if (type.HasFlag(MeleeWeaponType.Long_Sharp) || type.HasFlag(MeleeWeaponType.Short_Sharp))
            cat |= WeaponCat.Sharp;

        if (type.HasFlag(MeleeWeaponType.Long_Stab) || type.HasFlag(MeleeWeaponType.Short_Stab))
            cat |= WeaponCat.Stab;

        if (type.HasFlag(MeleeWeaponType.Long_Blunt) || type.HasFlag(MeleeWeaponType.Short_Blunt))
            cat |= WeaponCat.Blunt;

        return cat;
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

    public static bool IsAttack(this IdleType type) => type is IdleType.AttackHorizontal or IdleType.AttackSouth or IdleType.AttackNorth;

    public static bool IsMove(this IdleType type) => type is IdleType.MoveVertical or IdleType.MoveHorizontal;

    public static bool IsIdle(this IdleType type, bool includeFlavour = true) => type is IdleType.Idle || (includeFlavour && type is IdleType.Flavour);

    /// <summary>
    /// Attempts to get the equipped melee weapon of this pawn.
    /// That includes sidearms if SimpleSidearms is installed.
    /// This will only return the melee weapon if said melee weapon is compatible with the animation mod (i.e. it has valid tweak data).
    /// </summary>
    public static ThingWithComps GetFirstMeleeWeapon(this Pawn pawn) => GetFirstMeleeWeapon(pawn, out _);

    /// <summary>
    /// Attempts to get the equipped melee weapon of this pawn.
    /// That includes sidearms if SimpleSidearms is installed.
    /// This will only return the melee weapon if said melee weapon is compatible with the animation mod (i.e. it has valid tweak data).
    /// </summary>
    public static ThingWithComps GetFirstMeleeWeapon(this Pawn pawn, out ItemTweakData tweakData)
    {
        tweakData = null;
        if (pawn?.equipment == null)
            return null;

        if (pawn.equipment.Primary?.def.IsMeleeWeapon() ?? false)
        {
            tweakData = TweakDataManager.TryGetTweak(pawn.equipment.Primary.def);
            if (tweakData != null)
                return pawn.equipment.Primary;
        }

        foreach(var item in pawn.equipment.AllEquipmentListForReading)
        {
            if (item.def.IsMeleeWeapon())
            {
                tweakData = TweakDataManager.TryGetTweak(item.def);
                if (tweakData != null)
                    return item;
            }
        }

        if (Core.IsSimpleSidearmsActive && pawn.inventory?.innerContainer != null)
        {
            foreach (var item in pawn.inventory.innerContainer)
            {
                if (item is ThingWithComps twc && item.def.IsMeleeWeapon())
                {
                    tweakData = TweakDataManager.TryGetTweak(item.def);
                    if (tweakData != null)
                        return twc;
                }
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

    public static ItemTweakData TryGetTweakData(this Thing weapon) => TweakDataManager.TryGetTweak(weapon.def);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GetOccupiedMaskBit(this uint mask, int x, int z) => (((uint)1 << (x + 1) + (z + 1) * 3) & mask) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GetOccupiedMaskBitX(this uint mask, int x) => (((uint)1 << (x + 1) + 3) & mask) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GetOccupiedMaskBitZ(this uint mask, int z) => (((uint)1 << 1 + (z + 1) * 3) & mask) != 0;

    [DebugAction("Melee Animation", "Spawn all melee weapons (tiny)", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void GimmeMeleeWeaponsTiny() => GimmeMeleeWeapons(WeaponSize.Tiny);

    [DebugAction("Melee Animation", "Spawn all melee weapons (medium)", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void GimmeMeleeWeaponsMedium() => GimmeMeleeWeapons(WeaponSize.Medium);

    [DebugAction("Melee Animation", "Spawn all melee weapons (colossal)", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void GimmeMeleeWeaponsColossal() => GimmeMeleeWeapons(WeaponSize.Colossal);

    [DebugAction("Melee Animation", "Spawn all melee weapons", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void GimmeMeleeWeaponsAll() => GimmeMeleeWeapons(null);

    [DebugAction("Melee Animation", "Give all selected melee weapons", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void GiveAllMeleeWeapons()
    {
        var pawns = Find.Selector.SelectedPawns.Where(p => p.equipment.Primary == null);

        IEnumerable<ThingDef> GetWeaponDefs(WeaponSize? onlySize)
        {
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                var tweak = TweakDataManager.TryGetTweak(def);
                if (!def.IsMeleeWeapon() || tweak == null)
                    continue;

                if (onlySize != null)
                {
                    var cat = IdleClassifier.Classify(tweak);
                    if (cat.size != onlySize.Value)
                        continue;
                }

                yield return def;
            }
        }

        foreach (var pawn in pawns)
        {
            var weapon = GetWeaponDefs(null).RandomElementWithFallback();
            if (weapon == null)
                continue;

            var spawned = ThingMaker.MakeThing(weapon) as ThingWithComps;
            if (spawned == null)
                continue;

            pawn.equipment.Primary = spawned;
        }
    }

    private static void GimmeMeleeWeapons(WeaponSize? onlySize)
    {
        var pos = Verse.UI.MouseCell();
        foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            var tweak = TweakDataManager.TryGetTweak(def);
            if (!def.IsMeleeWeapon() || tweak == null)
                continue;

            if (onlySize != null)
            {
                var cat = IdleClassifier.Classify(tweak);
                if (cat.size != onlySize.Value)
                    continue;
            }

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

    [DebugAction("Melee Animation", "Spawn army", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static List<DebugActionNode> SpawnArmy()
    {
        List<DebugActionNode> list = new List<DebugActionNode>();
        foreach (PawnKindDef localKindDef2 in from kd in DefDatabase<PawnKindDef>.AllDefs
                 orderby kd.defName
                 select kd)
        {
            PawnKindDef localKindDef = localKindDef2;
            list.Add(new DebugActionNode(localKindDef.defName, DebugActionType.ToolMap)
            {
                category = DebugToolsSpawning.GetCategoryForPawnKind(localKindDef),
                action = () =>
                {
                    Faction faction = FactionUtility.DefaultFactionFrom(localKindDef.defaultFactionDef);
                    for (int i = 0; i < 50; i++)
                    {
                        Pawn pawn = PawnGenerator.GeneratePawn(localKindDef, faction);
                        GenSpawn.Spawn(pawn, Verse.UI.MouseCell(), Find.CurrentMap, WipeMode.Vanish);
                        DebugToolsSpawning.PostPawnSpawn(pawn);
                    }
                }
            });
        }
        return list;
    }
}