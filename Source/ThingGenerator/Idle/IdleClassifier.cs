using AAM.Tweaks;
using System.Linq;
using UnityEngine;
using Verse;

namespace AAM.Idle;

public static class IdleClassifier
{
    [TweakValue("Advanced Melee Animation", 0f, 8f)]
    public static float ColossalSizeThreshold = 1.37f;

    [TweakValue("Advanced Melee Animation", 0f, 5f)]
    public static float TinySizeThreshold = 0.63f;

    public static (WeaponSize size, bool isSharp) Classify(ItemTweakData tweakData)
    {
        if (tweakData == null)
            return default;

        bool HasAnyFlag(MeleeWeaponType flag) => (tweakData.MeleeWeaponType & flag) != 0;

        // Type is as simple:
        // Stab sharp takes priorty over blunt.
        bool isSharp = HasAnyFlag(MeleeWeaponType.Long_Sharp | MeleeWeaponType.Short_Sharp | MeleeWeaponType.Long_Stab | MeleeWeaponType.Short_Stab);

        float length = GetLength(tweakData);
        bool isTiny = length <= TinySizeThreshold;

        if (isTiny)
            return (WeaponSize.Tiny, isSharp);

        bool isColossal = length >= ColossalSizeThreshold;
        return isColossal ? (WeaponSize.Colossal, isSharp) : (WeaponSize.Medium, isSharp);
    }

    private static float GetLength(ItemTweakData tweakData) => Mathf.Max(tweakData.BladeLength, tweakData.MaxDistanceFromHand);

    private struct TableRow
    {
        public ThingDef Def;
        public ItemTweakData Tweak;
        public WeaponSize Size;
        public bool IsSharp;
    }

    [DebugOutput("Advanced Melee Animation")]
    private static void LogTextureCategories()
    {
        var data = from def in DefDatabase<ThingDef>.AllDefsListForReading
           where def.IsMeleeWeapon
           let tweak = TweakDataManager.TryGetTweak(def)
           where tweak != null
           let cat = Classify(tweak)
           select new TableRow
           {
               Def = def,
               Tweak = tweak,
               IsSharp = cat.isSharp,
               Size = cat.size
           };

        TableDataGetter<TableRow>[] table = new TableDataGetter<TableRow>[5];
        table[0] = new TableDataGetter<TableRow>("Def Name", row => row.Def.defName);
        table[1] = new TableDataGetter<TableRow>("Name", row => row.Def.LabelCap);
        table[2] = new TableDataGetter<TableRow>("Size", row => row.Size);
        table[3] = new TableDataGetter<TableRow>("Sharp?", row => row.IsSharp.ToStringCheckBlank());
        table[4] = new TableDataGetter<TableRow>("Length", row => GetLength(row.Tweak).ToString("F3"));

        DebugTables.MakeTablesDialog(data, table);
    }
}
