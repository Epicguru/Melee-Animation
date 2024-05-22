using System;
using RimWorld;
using Verse;

namespace AM;

public static class AudioUtility
{
    /// <summary>
    /// Get a sound for a weapon hitting a pawn.
    /// May return null.
    /// </summary>
    public static SoundDef GetPawnHitSound(Thing attackerWeapon)
    {
        if (attackerWeapon != null && !attackerWeapon.def.meleeHitSound.NullOrUndefined())
        {
            return attackerWeapon.def.meleeHitSound;
        }

        VerbProperties verbProps = attackerWeapon?.def.verbs?.FirstOrFallback();

        if (attackerWeapon is not { Stuff: not null })
        {
            return SoundDefOf.Pawn_Melee_Punch_HitPawn;
        }
        
        if (verbProps != null && verbProps.meleeDamageDef.armorCategory == DamageArmorCategoryDefOf.Sharp)
        {
            if (!attackerWeapon.Stuff.stuffProps.soundMeleeHitSharp.NullOrUndefined())
            {
                return attackerWeapon.Stuff.stuffProps.soundMeleeHitSharp;
            }
        }
        else if (!attackerWeapon.Stuff.stuffProps.soundMeleeHitBlunt.NullOrUndefined())
        {
            return attackerWeapon.Stuff.stuffProps.soundMeleeHitBlunt;
        }

        return SoundDefOf.Pawn_Melee_Punch_HitPawn;
    }

    /// <summary>
    /// Gets the sound of two weapons clashing.
    /// May return null.
    /// </summary>
    public static SoundDef GetWeaponClashSound(Thing weapon1, Thing weapon2)
    {
        var mat1 = GetMaterial(weapon1);
        var mat2 = GetMaterial(weapon2);
        var largest = mat1 > mat2 ? mat1 : mat2;

        return largest switch
        {
            WeaponMaterial.Wood => AM_DefOf.AM_WoodSwordClash,
            WeaponMaterial.Stone => AM_DefOf.AM_StoneSwordClash,
            WeaponMaterial.Metal => AM_DefOf.AM_MetalSwordClash,
            _ => AM_DefOf.AM_MetalSwordClash
        };
    }

    private static WeaponMaterial GetMaterial(Thing weapon)
    {
        if (weapon?.Stuff == null)
        {
            if (IsMadeFromExclusive(weapon.def, thing => thing.IsStuff && IsWoody(thing)))
            {
                return WeaponMaterial.Wood;
            }
            
            if (IsMadeFromExclusive(weapon.def, thing => thing.IsStuff && IsStony(thing)))
            {
                return WeaponMaterial.Stone;
            }
            
            return WeaponMaterial.Metal;
        }

        if (IsWoody(weapon.Stuff))
        {
            return WeaponMaterial.Wood;
        }

        return IsStony(weapon.Stuff) ? WeaponMaterial.Stone : WeaponMaterial.Metal;
    }

    private static bool IsMadeFromExclusive(ThingDef thing, Predicate<ThingDef> isValidMaterial)
    {
        if (thing.AllRecipes == null || thing.AllRecipes.Count == 0)
        {
            return false;
        }

        foreach (var rec in thing.AllRecipes)
        {
            if (!IsValidRecipe(rec, isValidMaterial))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidRecipe(RecipeDef rec, Predicate<ThingDef> isValidMaterial)
    {
        if (rec.ingredients.Count != 1)
        {
            return false;
        }

        var ing = rec.ingredients[0];
        if (ing.IsFixedIngredient || ing.filter == null)
        {
            return isValidMaterial(ing.FixedIngredient);
        }

        foreach (var item in ing.filter.AllowedThingDefs)
        {
            if (!isValidMaterial(item))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsStony(ThingDef material)
    {
        return material.stuffProps.categories.Contains(StuffCategoryDefOf.Stony);
    }

    private static bool IsWoody(ThingDef material)
    {
        return material.stuffProps.categories.Contains(StuffCategoryDefOf.Woody);
    }

    private enum WeaponMaterial
    {
        Wood,
        Stone,
        Metal
    }
}
