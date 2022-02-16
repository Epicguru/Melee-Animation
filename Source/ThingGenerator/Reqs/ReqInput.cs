using AAM.Tweaks;
using Verse;

namespace AAM.Reqs
{
    public struct ReqInput
    {
        public ThingDef WeaponDef;
        public ItemTweakData TweakData;
        public MeleeWeaponType TypeFlags => _typeFlags ?? TweakData?.MeleeWeaponType ?? 0;

        public MeleeWeaponType? _typeFlags;

        public ReqInput(ThingDef weapon)
        {
            WeaponDef = weapon;
            TweakData = TweakDataManager.TryGetTweak(weapon);
            _typeFlags = null;
        }

        public ReqInput(ItemTweakData td)
        {
            WeaponDef = td.GetDef();
            TweakData = td;
            _typeFlags = null;
        }

        public ReqInput(MeleeWeaponType flags)
        {
            WeaponDef = null;
            TweakData = null;
            _typeFlags = flags;
        }
    }
}
