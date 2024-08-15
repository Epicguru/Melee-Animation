using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using AM.AMSettings;
using AM.Idle;
using AM.RendererWorkers;
using AM.Reqs;
using AM.Sweep;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
using Verse;
using LudeonTK;

namespace AM;

[UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public class AnimDef : Def
{
    #region Static stuff
    public static IReadOnlyList<AnimDef> AllDefs => allDefs;

    private static List<AnimDef> allDefs;
    private static Dictionary<AnimType, List<AnimDef>> defsOfType;
    private static readonly HandsVisibilityData defaultHandsVisibilityData = new HandsVisibilityData();

    public static void Init()
    {
        allDefs = new List<AnimDef>(DefDatabase<AnimDef>.AllDefs);
        defsOfType = new Dictionary<AnimType, List<AnimDef>>();

        foreach(var def in allDefs)
        {
            var t = def.type;
            if(!defsOfType.TryGetValue(t, out var list))
            {
                list = new List<AnimDef>();
                defsOfType.Add(t, list);
            }
            list.Add(def);
        }
    }

    public static IEnumerable<AnimDef> GetDefsOfType(AnimType type)
    {
        if (defsOfType.TryGetValue(type, out var list))
            return list;
        return Array.Empty<AnimDef>();
    }

    public static IEnumerable<AnimDef> GetExecutionAnimationsForPawnAndWeapon(Pawn pawn, ThingDef weaponDef, int? meleeLevel = null)
    {
        int meleeSkill = meleeLevel ?? pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;

        // TODO maybe cached based on requirement?
        return GetDefsOfType(AnimType.Execution).Where(d =>
            d.Allows(new ReqInput(weaponDef)) &&
            (d.minMeleeSkill ?? 0) <= meleeSkill && 
            d.Probability > 0);
    }

    [DebugAction("Melee Animation", "Reload all animations", actionType = DebugActionType.Action)]
    public static void ReloadAllAnimations()
    {
        foreach (var def in allDefs)
        {
            if (def.resolvedData == null)
                continue;

            def.resolvedData = AnimData.Load(def.FullDataPath, false);
            def.resolvedNonLethalData = File.Exists(def.FullNonLethalDataPath) ? AnimData.Load(def.FullNonLethalDataPath, false) : def.resolvedData;
        }
    }

    #endregion

    public class SettingsData : IExposable, ISettingsEqualityChecker
    {
        public bool Enabled = true;
        public float Probability = 1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Enabled, "Enabled", true);
            Scribe_Values.Look(ref Probability, "Probability", 1f);
        }

        public bool IsEqualForSettings(object other)
        {
            return other is SettingsData osd && osd.Enabled == Enabled && Math.Abs(osd.Probability - Probability) < 0.0001f;
        }
    }

    public string LabelOrFallback => string.IsNullOrEmpty(label) ? defName : LabelCap;
    public virtual string FullDataPath
    {
        get
        {
            var mod = modContentPack;
            if (mod == null)
            {
                Core.Error($"This def '{defName}' has no modContentPack, so FullDataPath cannot be resolved! Returning relative path instead...");
                return data;
            }

            string relative = data.Trim();
            if (string.IsNullOrWhiteSpace(new FileInfo(relative).Extension))
                relative += ".json";

            return Path.Combine(mod.RootDir, "Animations", relative);
        }
    }
    public virtual string FullNonLethalDataPath => FullDataPath.Replace(".json", "_NL.json");
    public AnimData Data
    {
        get
        {
            if (resolvedData == null)
            {
                ResolveData();
            }

            return resolvedData;
        }
    }
    public AnimData DataNonLethal
    {
        get
        {
            if (resolvedData == null)
            {
                ResolveData();
            }

            return resolvedNonLethalData ?? resolvedData;
        }
    }
    public string DataPath => data;
    /// <summary>
    /// A mask where a high bit means that the spot must be clear (standable)
    /// in a 7x7 cell grid around the animation root cell.
    /// </summary>
    public ulong ClearMask, FlipClearMask;
    public float Probability => relativeProbability * UserEditedProbability;
    public float UserEditedProbability => (SData?.Enabled ?? true) ? (SData?.Probability ?? 1f) : 0f;
    [XmlIgnore] public SettingsData SData;

    public AnimType type = AnimType.Execution;
    public string jobString;
    public Type rendererWorker;
    public int pawnCount;
    /// <summary>
    /// The main and normally only weapon filter.
    /// </summary>
    public Req weaponFilter;
    /// <summary>
    /// The optional secondary weapon filter.
    /// Currently only used in some duel animations.
    /// Allows filtering out the 'second' pawn based on weapon.
    /// For example, if a duel animation only works for knife vs spear, you would have to use both filters.
    /// </summary>
    public Req weaponFilterSecond;
    public List<AnimCellData> cellData = new List<AnimCellData>();
    public ISweepProvider sweepProvider;
    public bool drawDisabledPawns;
    public bool shadowDrawFromData;
    public int? minMeleeSkill;
    public bool canEditProbability = true;
    public IdleType idleType;
    public bool pointAtTarget;
    public float pointAtTargetAngleOffset;
    public int returnToIdleStart, returnToIdleEnd;
    public int idleFrame;
    public ExecutionOutcome? fixedOutcome;
    public List<HandsVisibilityData> handsVisibility = new List<HandsVisibilityData>();
    public PromotionData promotionRules;
    public List<int> spawnDroppedHeadsForPawnIndices = new List<int>();

#pragma warning disable CS0649 // Field 'AnimDef.data' is never assigned to, and will always have its default value null
    private string data;
#pragma warning restore CS0649 // Field 'AnimDef.data' is never assigned to, and will always have its default value null

    private Dictionary<string, string> additionalData = new Dictionary<string, string>();
    private float relativeProbability = 1;

    private AnimData resolvedData, resolvedNonLethalData;

    public HandsVisibilityData GetHandsVisibility(int pawnIndex)
    {
        if (pawnIndex < 0)
            return defaultHandsVisibilityData;

        foreach (var d in handsVisibility)
        {
            if (d.pawnIndex == pawnIndex)
                return d;
        }

        return defaultHandsVisibilityData;
    }

    public void SetDefaultSData()
    {
        SData = new SettingsData()
        {
            Enabled = true,
            Probability = 1f,
        };
    }

    protected virtual void ResolveData()
    {
        if (File.Exists(FullDataPath))
            resolvedData = AnimData.Load(FullDataPath);

        if (File.Exists(FullNonLethalDataPath))
            resolvedNonLethalData = AnimData.Load(FullNonLethalDataPath);
    }

    public T TryGetAdditionalData<T>(string id, T defaultValue = default)
    {
        string value = additionalData.TryGetValue(id);
        if (value == null)
            return defaultValue;

        // Here, enjoy some hacky shit.
        return defaultValue switch
        {
            string => (T)(object)value,
            int => int.TryParse(value, out var i) ? (T)(object)i : defaultValue,
            float => float.TryParse(value, out var f) ? (T)(object)f : defaultValue,
            bool => bool.TryParse(value, out var b) ? (T)(object)b : defaultValue,
            _ => throw new NotSupportedException($"Additional data of type '{typeof(T)}' is not supported.")
        };
    }

    public virtual AnimationRendererWorker TryMakeRendererWorker()
    {
        if (rendererWorker == null)
            return null;

        try
        {
            var instance = Activator.CreateInstance(rendererWorker);
            return instance as AnimationRendererWorker;
        }
        catch (Exception e)
        {
            Core.Error($"Failed to create instance of SetupWorker class '{rendererWorker}'", e);
            return null;
        }
    }

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (var item in base.ConfigErrors())
            yield return item;

        if (type == AnimType.Execution && pawnCount < 2)
            yield return $"Animation type is Execution, but pawnCount is less than 2! ({pawnCount})";

        if (string.IsNullOrWhiteSpace(data))
            yield return "Animation has no data path! Please specify the location of the data file using the data tag.";
        else if (!File.Exists(FullDataPath))
            yield return $"Failed to find animation file at '{FullDataPath}'!";

        if (type is AnimType.Execution or AnimType.Duel or AnimType.Idle && weaponFilter == null)
            yield return "weaponFilter is not assigned.";

        var p1StartCell = TryGetCell(AnimCellData.Type.PawnStart, false, false, 1);
        if (type == AnimType.Execution && (p1StartCell == null || p1StartCell != new IntVec2(1, 0)))
            yield return $"This execution animation should have pawn 1 starting at offset (1, 0), but instead they are starting at {p1StartCell}. Change this in the <cellData> tag.";

        for (int i = 0; i < cellData.Count; i++)
            foreach (var error in cellData[i].ConfigErrors())
                yield return $"[CellData, index:{i}] {error}";

        var indexes = new HashSet<int>();
        foreach (var d in handsVisibility)
        {
            if (d.pawnIndex < 0)
            {
                yield return $"There is an item in <handsVisibility> that has <pawnIndex> of {d.pawnIndex} which is invalid. <pawnIndex> should be at least 0.";
            }
            else if (!indexes.Add(d.pawnIndex))
            {
                yield return $"There is an item in <handsVisibility> that has duplicate <pawnIndex> of {d.pawnIndex}.";
            }
        }
    }

    public override void PostLoad()
    {
        base.PostLoad();
        ClearMask = SpaceChecker.MakeClearMask(this, false);
        FlipClearMask = SpaceChecker.MakeClearMask(this, true);

        if (promotionRules == null)
            return;

        if (relativeProbability != 0)
            Core.Warn($"{this} has relativeProbability of {relativeProbability:P1} but it also uses Promotion Rules. This might be a mistake.");

        promotionRules.Def = this;
        promotionRules.PostLoad();
    }

    public IntVec2? TryGetCell(AnimCellData.Type type, bool flipX, bool flipY, int? pawnIndex = null)
    {
        foreach(var cell in cellData)            
            if (cell.type == type && cell.pawnIndex == pawnIndex)
                return Flip(cell.GetCell(), flipX, flipY);
            
        return null;
    }

    public IEnumerable<IntVec2> GetCells(AnimCellData.Type type, bool flipX, bool flipY, int? pawnIndex = null)
    {
        foreach (var cData in cellData)            
            if (cData.type == type && cData.pawnIndex == pawnIndex)                
                foreach (var cell in cData.GetCells())
                    yield return Flip(cell, flipX, flipY);
    }

    public IEnumerable<IntVec3> GetMustBeClearCells(bool flipX, bool flipY, IntVec3 offset)
    {
        foreach (var cells in cellData)
        {
            foreach (var cell in cells.GetCells())
            {
                yield return Flip(cell, flipX, flipY).ToIntVec3 + offset;
            }
        }
    }

    private IntVec2 Flip(in IntVec2 input, bool fx, bool fy) => new IntVec2(fx ? -input.x : input.x, fy ? -input.z : input.z);

    public bool Allows(in ReqInput input)
    {
        return weaponFilter != null && weaponFilter.Evaluate(input);
    }

    public IEnumerable<ThingDef> GetAllAllowedWeapons()
    {
        foreach (var thing in DefDatabase<ThingDef>.AllDefsListForReading)
            if (thing.IsMeleeWeapon() && Allows(new ReqInput(thing)))
                yield return thing;
    }

    /*
     * Animation promotion is where a selected animation is swapped out for a different animation just before it starts.
     * The reason this is done is that certain conditions need to be met for some animations,
     * and the inputs for those conditions may not be known until after the animation selection calculations 
     * have already been done, hence the need to 'promote' an already selected animation to a different one.
     */
    public AnimDef TryGetPromotionDef(PromotionInput input)
    {
        if (input.OriginalAnim != this)
            Core.Error($"Original anim should be this one: {this}. Instead got {input.OriginalAnim}");

        var allPossibles = from def in GetDefsOfType(AnimType.Execution)
                           where def.promotionRules != null
                           let chance = def.promotionRules.GetRelativePromotionChanceFor(input)
                           where chance > 0
                           select (def, chance);

        int possibleCount = allPossibles.Count();

        // Chance to promote at all:
        // Needs to be thought out... A future task.
        float promotionChanceForAny = Mathf.Min(possibleCount * 0.5f, 0.65f);
        if (!Rand.Chance(promotionChanceForAny))       
            return null;        

        var selected = allPossibles.RandomElementByWeightWithFallback(pair => pair.chance);

        if (selected.def != null)
        {
            Core.Log($"Promoted animation: {input.OriginalAnim} -> {selected.def} ({promotionChanceForAny:P0} general promotion chance, {selected.chance:P0} relative chance)");
        }

        return selected.def;
    }

    public class HandsVisibilityData
    {
        public int pawnIndex = -1;
        public bool? showMainHand = null;
        public bool? showAltHand = null;
    }

    public class PromotionData
    {
        public AnimDef Def { get; set; }

        public ExecutionOutcome onlyWhenOutcomeIs = ExecutionOutcome.Nothing;
        public float basePromotionRelativeChance = 1f;
        public List<Type> promotionWorkers = new List<Type>();

        private readonly List<IPromotionWorker> workers = new List<IPromotionWorker>();

        public float GetRelativePromotionChanceFor(in PromotionInput input)
        {
            // Outcome must be in the allowed range.
            if (!onlyWhenOutcomeIs.HasFlag(input.Outcome))
                return 0;

            // Must allow the used weapon.
            if (!Def.Allows(input.ReqInput))
                return 0f;

            // Base chance from def and user settings.
            float chance = basePromotionRelativeChance * Def.UserEditedProbability;
            if (chance <= 0f)
                return 0f;

            // Must have enough space.
            if (!CheckMasks(input))
                return 0f;

            // Do all the specific workers.
            foreach (var worker in workers)
            {
                chance *= worker.GetPromotionRelativeChanceFor(input);
            }

            return chance;
        }

        private bool CheckMasks(in PromotionInput input)
        {
            ulong mask = input.FlipX ? Def.FlipClearMask : Def.ClearMask;
            return (mask & input.OccupiedMask) == 0;
        }

        public void PostLoad()
        {
            foreach (var type in promotionWorkers)
            {
                workers.Add(Activator.CreateInstance(type) as IPromotionWorker);
            }
        }
    }

    public interface IPromotionWorker
    {
        float GetPromotionRelativeChanceFor(in PromotionInput input);
    }

    public struct PromotionInput
    {
        public required Pawn Attacker;
        public required Pawn Victim;
        public required ReqInput ReqInput;
        public required AnimDef OriginalAnim;
        public required ExecutionOutcome Outcome;
        public required ulong OccupiedMask;
        public required bool FlipX;
    }
}