using AAM.Idle;
using AAM.Tweaks;
using Verse;

namespace AAM.Reqs;

public struct ReqInput
{
    public ThingDef WeaponDef;
    public ItemTweakData TweakData;
    public MeleeWeaponType TypeFlags;
    public WeaponSize SizeFlags;
    public WeaponCat CategoryFlags;

    public ReqInput(ThingDef weapon) : this(TweakDataManager.TryGetTweak(weapon))
    {
        WeaponDef = weapon;
    }

    public ReqInput(ItemTweakData td)
    {
        WeaponDef = td.GetDef();
        TweakData = td;
        TypeFlags = td.MeleeWeaponType;

        var pair = td.GetCategory();
        SizeFlags = pair.size;
        CategoryFlags = pair.category;
    }
}