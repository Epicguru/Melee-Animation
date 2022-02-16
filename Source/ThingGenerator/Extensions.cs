using AAM.Events;
using AAM.Events.Workers;
using RimWorld;
using System;
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

        public static Matrix4x4 MakeAnimationMatrix(this in LocalTargetInfo target)
            => Matrix4x4.TRS(new Vector3(target.CenterVector3.x, AltitudeLayer.Pawn.AltitudeFor(), target.CenterVector3.z), Quaternion.identity, Vector3.one);

        public static bool IsInAnimation(this Pawn pawn)
            => AnimRenderer.TryGetAnimator(pawn) != null;

        public static bool IsInAnimation(this Pawn pawn, out AnimRenderer animRenderer)
            => (animRenderer = AnimRenderer.TryGetAnimator(pawn)) != null;

        public static AnimRenderer TryGetAnimator(this Pawn pawn) => AnimRenderer.TryGetAnimator(pawn);

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

        public static Vector3 AngleToWorldDir(this float angleDeg) => -new Vector3(Mathf.Cos(angleDeg * Mathf.Deg2Rad), 0f, Mathf.Sin(angleDeg * Mathf.Deg2Rad));

        [DebugAction("Advanced Animation Mod", "Spawn all melee weapons", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void GimmeMeleeWeapons()
        {
            var pos = Verse.UI.MouseCell();
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.IsMeleeWeapon)
                {
                    try
                    {
                        DebugThingPlaceHelper.DebugSpawn(def, pos, 1, false);
                    }
                    catch (Exception e)
                    {
                        Core.Warn($"Failed to spawn {def}: [{e.GetType().Name}] {e.Message}");
                    }
                }
            }
        }
    }
}
