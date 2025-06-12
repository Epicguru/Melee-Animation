using AM.Idle;
using AM.Tweaks;
using Verse;

namespace AM.Reqs;

public struct ReqInput
{
    public bool IsFists => WeaponDef == null;
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
        WeaponDef = td?.GetDef();
        TweakData = td;
        TypeFlags = td?.MeleeWeaponType ?? 0;

        var pair = td?.GetCategory() ?? (default, default);
        SizeFlags = pair.size;
        CategoryFlags = pair.category;
    }
}