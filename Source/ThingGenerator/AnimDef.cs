using AAM.Idle;
using AAM.RendererWorkers;
using AAM.Reqs;
using AAM.Sweep;
using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using UnityEngine;
using Verse;

namespace AAM
{
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

        public static AnimDef GetMainIdleAnim(WeaponSize weaponSize, bool sharp)
        {
            // TODO optimize.
            foreach (var def in defsOfType[AnimType.Idle])
            {
                if (def.idleType == IdleType.Idle && def.weaponSize == weaponSize && (def.forSharpWeapons == null || def.forSharpWeapons.Value == sharp))
                    return def;
            }
            return null;
        }

        public static AnimDef GetMoveIdleAnim(WeaponSize weaponSize, bool sharp, bool horizontal)
        {
            // TODO optimize.
            var type = horizontal ? IdleType.MoveHorizontal : IdleType.MoveVertical;
            foreach (var def in defsOfType[AnimType.Idle])
            {
                if (def.idleType == type && def.weaponSize == weaponSize && (def.forSharpWeapons == null || def.forSharpWeapons.Value == sharp))
                    return def;
            }
            return null;
        }

        public static IEnumerable<AnimDef> GetIdleFlavours(WeaponSize weaponSize, bool sharp)
        {
            // TODO optimize.
            foreach (var def in defsOfType[AnimType.Idle])
            {
                if (def.idleType == IdleType.Flavour && def.weaponSize == weaponSize && (def.forSharpWeapons == null || def.forSharpWeapons.Value == sharp))
                    yield return def;
            }
        }

        public static IEnumerable<AnimDef> GetDefsOfType(AnimType type)
        {
            if (defsOfType.TryGetValue(type, out var list))
                return list;
            return Array.Empty<AnimDef>();
        }

        public static IEnumerable<AnimDef> GetExecutionAnimationsForPawnAndWeapon(Pawn pawn, ThingDef weaponDef)
        {
            int meleeSkill = pawn.skills.GetSkill(SkillDefOf.Melee).Level;

            return GetDefsOfType(AnimType.Execution)
                .Where(d => d.AllowsWeapon(new ReqInput(weaponDef)))
                .Where(d => (d.minMeleeSkill ?? 0) <= meleeSkill);
        }

        [DebugAction("Advanced Melee Animation", "Reload all animations", actionType = DebugActionType.Action)]
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

        public class SettingsData : IExposable
        {
            public bool Enabled = true;
            public float Probability = 1;

            public void ExposeData()
            {
                Scribe_Values.Look(ref Enabled, "Enabled", true);
                Scribe_Values.Look(ref Probability, "Probability", 1f);
            }
        }

        public virtual string FullDataPath
        {
            get
            {
                var mod = base.modContentPack;
                if (mod == null)
                {
                    Core.Error($"This def '{defName}' has no modContentPack, so FullDataPath cannot be resolved! Returning relative path instead...");
                    return data;
                }

                string relative = data.Trim();
                if (string.IsNullOrWhiteSpace(new FileInfo(relative).Extension))
                    relative += ".anim";

                return Path.Combine(mod.RootDir, "Animations", relative);
            }
        }
        public virtual string FullNonLethalDataPath => FullDataPath.Replace(".anim", "_NL.anim");
        public AnimData Data
        {
            get
            {
                if (resolvedData == null)
                    ResolveData();

                return resolvedData;
            }
        }
        public AnimData DataNonLethal
        {
            get
            {
                if (resolvedData == null)
                    ResolveData();

                return resolvedNonLethalData ?? resolvedData;
            }
        }
        public string DataPath => data;
        public ulong ClearMask, FlipClearMask;
        public float Probability => relativeProbability * ((SData?.Enabled ?? true) ? (SData?.Probability ?? 1f) : 0f);
        [XmlIgnore] public SettingsData SData;

        public AnimType type = AnimType.Execution;
        private string data;
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
        public List<AnimCellData> cellData = new();
        public ISweepProvider sweepProvider;
        public bool drawDisabledPawns;
        public bool shadowDrawFromData;
        public int? minMeleeSkill = null;
        public bool canEditProbability = true;
        public WeaponSize weaponSize;
        public IdleType idleType;
        public bool? forSharpWeapons;

        public List<HandsVisibilityData> handsVisibility = new List<HandsVisibilityData>();

        public class HandsVisibilityData
        {
            public int pawnIndex = -1;
            public bool? showMainHand = null;
            public bool? showAltHand = null;
        }

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

            if (weaponFilter == null)
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
        }

        public IntVec2? TryGetCell(AnimCellData.Type type, bool flipX, bool flipY, int? pawnIndex = null)
        {
            foreach(var data in cellData)            
                if (data.type == type && data.pawnIndex == pawnIndex)
                    return Flip(data.GetCell(), flipX, flipY);
            
            return null;
        }

        public IEnumerable<IntVec2> GetCells(AnimCellData.Type type, bool flipX, bool flipY, int? pawnIndex = null)
        {
            foreach (var data in cellData)            
                if (data.type == type && data.pawnIndex == pawnIndex)                
                    foreach (var cell in data.GetCells())
                        yield return Flip(cell, flipX, flipY);
        }

        public IEnumerable<IntVec3> GetMustBeClearCells(bool flipX, bool flipY, IntVec3 offset)
        {
            foreach (var data in cellData)
            {
                foreach (var cell in data.GetCells())
                {
                    yield return Flip(cell, flipX, flipY).ToIntVec3 + offset;
                }
            }
        }

        private IntVec2 Flip(in IntVec2 input, bool fx, bool fy) => new(fx ? -input.x : input.x, fy ? -input.z : input.z);

        public bool AllowsWeapon(ReqInput input)
        {
            return weaponFilter != null && weaponFilter.Evaluate(input);
        }

        public IEnumerable<ThingDef> GetAllAllowedWeapons()
        {
            foreach (var thing in DefDatabase<ThingDef>.AllDefsListForReading)
                if (thing.IsMeleeWeapon && AllowsWeapon(new ReqInput(thing)))
                    yield return thing;
        }
    }
}
