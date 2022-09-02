using AAM.Reqs;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using AAM.Sweep;
using Verse;

namespace AAM
{
    public class AnimDef : Def
    {
        #region Static stuff
        public static IReadOnlyList<AnimDef> AllDefs => allDefs;

        private static List<AnimDef> allDefs;
        private static Dictionary<AnimType, List<AnimDef>> defsOfType;

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
                foreach (var item in list)
                    yield return item;            
        }

        public static IEnumerable<AnimDef> GetExecutionAnimationsForWeapon(ThingDef def)
            => GetDefsOfType(AnimType.Execution).Where(d => d.AllowsWeapon(new ReqInput(def)));

        [DebugAction("Advanced Animation Mod", "Reload all animations", actionType = DebugActionType.Action)]
        public static void ReloadAllAnimations()
        {
            foreach (var def in allDefs)
            {
                if(def.resolvedData == null)
                    continue;

                def.resolvedData = AnimData.Load(def.FullDataPath, false);
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
        public float Probability => relativeProbability * (SData?.Probability ?? 1f);
        [XmlIgnore] public SettingsData SData;

        public AnimType type = AnimType.Execution;
        private string data;
        public string jobString;
        public int pawnCount;
        public Req weaponFilter;
        public List<AnimCellData> cellData = new();
        public ISweepProvider sweepProvider;
        private float relativeProbability = 1;

        private AnimData resolvedData, resolvedNonLethalData;

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
