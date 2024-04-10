using AM.Idle;
using JetBrains.Annotations;
using System.Collections.Generic;
using Verse;

namespace AM;

[UsedImplicitly]
public class MeleeAnimationAdjustmentDef : Def
{
    public static readonly Dictionary<ThingDef, WeaponAdjustment> AllWeaponAdjustments = new Dictionary<ThingDef, WeaponAdjustment>(64);

    /// <summary>
    /// Weapons in this list will be considered melee weapons even if Rimworld
    /// normally wouldn't.
    /// </summary>
    public List<ThingDef> considerMeleeWeapons;

    /// <summary>
    /// A dictionary of specific weapon adjustments,
    /// offering more control than that allowed by regular tweak data.
    /// </summary>
    public Dictionary<ThingDef, WeaponAdjustment> weaponAdjustments;

    public void RegisterData()
    {
        if (considerMeleeWeapons != null)
            RegisterMeleeWeapons();

        if (weaponAdjustments != null)
            RegisterWeaponAdjustments();
    }

    private void RegisterMeleeWeapons()
    {
        foreach (var def in considerMeleeWeapons)
        {
            if (def == null)
            {
                Core.Error($"Null weapon def in {defName}'s <{nameof(considerMeleeWeapons)}> list, reference failed to resolve.");
                continue;
            }

            Core.ForceConsiderTheseMeleeWeapons.Add(def);
        }
    }

    private void RegisterWeaponAdjustments()
    {
        foreach (var pair in weaponAdjustments)
        {
            AllWeaponAdjustments[pair.Key] = pair.Value;
        }
    }

    public class WeaponAdjustment
    {
        /// <summary>
        /// Forcibly makes a weapon be considered to be of this
        /// particular size, bypassing the regular automatic classification.
        /// </summary>
        public WeaponSize? overrideSize = null;
    }
}
