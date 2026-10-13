using System;

namespace AM.Tweaks
{
    [Flags]
    public enum MeleeWeaponType : uint
    {
        Long_Blunt  = 1,
        Long_Sharp  = 2,
        Long_Stab   = 4,
        Short_Blunt = 8,
        Short_Sharp = 16,
        Short_Stab  = 32
    }
}
