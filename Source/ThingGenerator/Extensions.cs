using AAM.Events;
using AAM.Events.Workers;
using AAM.Tweaks;
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
            => Matrix4x4.TRS(pawn.Position.ToVector3ShiftedWithAltitude(pawn.DrawPos.y), Quaternion.identity, Vector3.one);

        public static bool IsInAnimation(this Pawn pawn)
            => AnimRenderer.TryGetAnimator(pawn) != null;

        public static bool IsInAnimation(this Pawn pawn, out AnimRenderer animRenderer)
            => (animRenderer = AnimRenderer.TryGetAnimator(pawn)) != null;

        public static ThingWithComps GetFirstMeleeWeapon(this Pawn pawn)
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

            if (Core.IsSimpleSidearmsActive && pawn.inventory?.innerContainer != null)
            {
                foreach (var item in pawn.inventory.innerContainer)
                {
                    if (item is ThingWithComps twc && item.def.IsMeleeWeapon)
                        return twc;
                }
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

        public static Vector3 ToWorld(this in Vector2 flatVector, float altitude = 0) => new Vector3(flatVector.x, altitude, flatVector.y);

        public static Vector2 ToFlat(this in Vector3 worldVector) => new Vector3(worldVector.x, worldVector.z);

        public static T GetWorker<T>(this EventBase e) where T : EventWorkerBase => EventWorkerBase.GetWorker(e.EventID) as T;

        public static float RandomInRange(this in Vector2 range) => Rand.Range(range.x, range.y);

        public static bool AddPawn(this AnimRenderer renderer, Pawn pawn)
        {
            if (pawn == null)
                return false;

            int index = renderer.Pawns.Count;
            renderer.Pawns.Add(pawn);
            char tagChar = AnimRenderer.Alphabet[index];

            // Held item.
            string itemName = $"Item{tagChar}";
            var weapon = pawn.GetFirstMeleeWeapon();
            var tweak = weapon == null ? null : TweakDataManager.GetOrCreateDefaultTweak(weapon.def);
            var handsMode = tweak?.HandsMode ?? HandsMode.Default;

            // Hands and skin color...
            string mainHandName = $"HandA{(index > 0 ? (index + 1) : "")}";
            string altHandName = $"HandB{(index > 0 ? (index + 1) : "")}";

            Color skinColor = pawn.story?.SkinColor ?? Color.white;
            bool showMain = weapon != null && handsMode != HandsMode.No_Hands;
            bool showAlt = weapon != null && handsMode == HandsMode.Default;

            // Apply weapon.
            var itemPart = renderer.GetPart(itemName);
            if (weapon != null && itemPart != null)
            {
                tweak.Apply(renderer, itemPart);
                var ov = renderer.GetOverride(itemPart);
                ov.Material = weapon.Graphic.MatSingleFor(weapon);
                ov.UseMPB = false; // Do not use the material property block, because it will override the material second color and mask.
            }

            // Apply main hand.
            var mainHandPart = renderer.GetPart(mainHandName);
            if (mainHandPart != null)
            {
                var ov = renderer.GetOverride(mainHandPart);
                ov.PreventDraw = !showMain;
                ov.Texture = AnimationManager.HandTexture;
                ov.ColorOverride = skinColor;
            }

            // Apply alt hand.
            var altHandPart = renderer.GetPart(altHandName);
            if (mainHandPart != null)
            {
                var ov = renderer.GetOverride(altHandPart);
                ov.PreventDraw = !showAlt;
                ov.Texture = AnimationManager.HandTexture;
                ov.ColorOverride = skinColor;
            }

            return true;
        }
    }
}
