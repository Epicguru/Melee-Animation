using System;

namespace AM.Idle;

[Flags]
public enum WeaponSize
{
    /// <summary>
    /// Daggers, hatchets, small clubs etc.
    /// </summary>
    Tiny = 1,
    /// <summary>
    /// Most swords, lances, maces etc.
    /// </summary>
    Medium = 2,
    /// <summary>
    /// Huge hammers, swords, clubs.
    /// </summary>
    Colossal = 4
}
