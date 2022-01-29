﻿using AAM.Tweaks;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Verse;

namespace AAM
{
    public class AnimDef : Def
    {
        #region Static stuff
        private static List<AnimDef> allDefs;
        private static Dictionary<AnimType, List<AnimDef>> defsOfType;
        private static List<AnimDef> tempDefs;

        public static void Init()
        {
            allDefs = new List<AnimDef>(DefDatabase<AnimDef>.AllDefs);
            defsOfType = new Dictionary<AnimType, List<AnimDef>>();
            tempDefs = new List<AnimDef>(128);

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

        public static AnimDef TryGetExecutionFor(Pawn executor, Pawn victim)
        {
            if (executor == null)
                return null;

            var weapon = executor.equipment?.Primary;
            if (weapon == null)
            {
                Core.Error($"Cannot get execution def for {executor.NameShortColored} because they are not holding any weapon.");
                return null;
            }

            tempDefs.Clear();
            tempDefs.AddRange(GetDefsOfType(AnimType.Execution).Where(d => d.AllowsWeapon(weapon.def)));

            if (tempDefs.Count == 0)
                return null;
            return tempDefs.RandomElement();
        }

        public static IEnumerable<(IntVec3 pos, bool mirrorX)> GetPossibleExecutionPositions(IntVec3 rootPos, AnimDef def)
        {
            if (def == null)
            {
                Core.Error("Null def");
                yield break;
            }

            if (def.type != AnimType.Execution)
            {
                Core.Warn($"Called {nameof(GetPossibleExecutionPositions)} with a non-execution type animation def ({def})");
            }

            var pos = def.TryGetCell(AnimCellData.Type.PawnStart, false, false, 1);
            if (pos != null)
                yield return (rootPos + new IntVec3(pos.Value.x, 0, pos.Value.z), false);
            if (pos != null && def.direction == AnimDirection.Horizontal)
            {
                pos = def.TryGetCell(AnimCellData.Type.PawnStart, true, false, 1);
                if (pos != null)
                    yield return (rootPos + new IntVec3(pos.Value.x, 0, pos.Value.z), true);
            }
        }

        public static IEnumerable<(AnimDef def, bool mirrorX, IntVec3 victimPosition)> GetPossibleExecutionsNow(Pawn executioner, Pawn victim = null, Thing weapon = null)
        {
            weapon ??= executioner.GetEquippedMeleeWeapon();
            if (weapon == null)
                yield break;

            var execPos = executioner.Position;
            var vicPos = victim?.Position;

            var execs = defsOfType[AnimType.Execution];
            foreach (var def in execs)
            {
                if (!def.AllowsWeapon(weapon.def))
                    continue;

                foreach (var pair in GetPossibleExecutionPositions(execPos, def))
                {
                    if (vicPos != null && pair.pos != vicPos.Value)
                        continue;

                    yield return (def, pair.mirrorX, execPos + pair.pos);
                }
            }
        }

        #endregion

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
        public bool HasJobString => !string.IsNullOrWhiteSpace(jobString);
        public AnimData Data
        {
            get
            {
                resolvedData ??= ResolveData();
                return resolvedData;
            }
        }
        public string DataPath => data;

        public AnimType type = AnimType.Execution;
        public AnimDirection direction = AnimDirection.Horizontal;
        public string data;
        public string jobString;
        public int pawnCount;
        public MeleeWeaponType? allowedWeaponTypes;
        public List<AnimCellData> cellData = new List<AnimCellData>();

        private AnimData resolvedData;

        protected virtual AnimData ResolveData()
        {
            return AnimData.Load(FullDataPath);
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var item in base.ConfigErrors())
                yield return item;

            if (type == AnimType.Execution && pawnCount < 2)
                yield return $"Animation type is Execution, but pawnCount is less than 2! ({pawnCount})";

            if (string.IsNullOrWhiteSpace(data))
                yield return $"Animation has no data path! Please secify the location of the data file using the data tag.";

            if (allowedWeaponTypes != null && allowedWeaponTypes == 0)
                yield return "allowedWeaponTags is empty! Please provide at least 1 allowed weapon type.";

            for (int i = 0; i < cellData.Count; i++)
                foreach (var error in cellData[i].ConfigErrors())
                    yield return $"[CellData, index:{i}] {error}";
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

        private IntVec2 Flip(in IntVec2 input, bool fx, bool fy) => new IntVec2(fx ? -input.x : input.x, fy ? -input.z : input.z);
    
        public bool AllowsWeapon(ThingDef def)
        {
            if (def == null)
                return false;

            if (allowedWeaponTypes == null)
                return true;
            if (allowedWeaponTypes.Value == 0)
                return false;

            var tweak = TweakDataManager.TryGetTweak(def);
            if (tweak == null)
                return false;

            uint result = (uint)(tweak.MeleeWeaponType & allowedWeaponTypes.Value);
            return result != 0;
        }

        public IEnumerable<ThingDef> GetAllAllowedWeapons()
        {
            foreach (var thing in DefDatabase<ThingDef>.AllDefsListForReading)
                if (AllowsWeapon(thing))
                    yield return thing;
        }
    }
}
