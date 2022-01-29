using RimWorld;
using UnityEngine;
using Verse;

namespace AAM
{
    public static class Extensions
    {
        public static AnimationManager GetAnimManager(this Map map)
            => map?.GetComponent<AnimationManager>();

        public static AnimationManager GetAnimManager(this Pawn pawn)
            => pawn?.Map?.GetComponent<AnimationManager>();

        public static Matrix4x4 MakeAnimationMatrix(this Pawn pawn)
            => Matrix4x4.TRS(pawn.Position.ToVector3ShiftedWithAltitude(pawn.DrawPos.y), Quaternion.identity, new Vector3(1f, 0.1f, 1f));

        public static bool IsInAnimation(this Pawn pawn)
            => AnimRenderer.TryGetAnimator(pawn) != null;

        public static bool IsInAnimation(this Pawn pawn, out AnimRenderer animRenderer)
            => (animRenderer = AnimRenderer.TryGetAnimator(pawn)) != null;

        public static ThingWithComps GetEquippedMeleeWeapon(this Pawn pawn)
        {
            if (pawn?.equipment == null)
                return null;

            if (pawn.equipment.Primary?.def.IsMeleeWeapon ?? false)
                return pawn.equipment.Primary;

            foreach(var item in pawn.equipment.AllEquipmentListForReading)
            {
                if (item.def.IsMeleeWeapon)
                    return item;
            }
            return null;
        }

        public static PawnType GetPawnType(this Pawn pawn)
        {
            if (pawn.IsColonist)
                return PawnType.Colonist;

            if (pawn.IsPrisonerOfColony)
                return PawnType.Prisoner;

            if (pawn.HostileTo(Faction.OfPlayerSilentFail))
                return PawnType.Enemy;

            return PawnType.Friendly;
        }
    }
}
