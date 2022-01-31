using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AAM
{
    public class AnimationStartParameters : IExposable
    {
        public List<Pawn> Pawns = new List<Pawn>();
        public AnimDef Animation;
        public Map Map;
        public Matrix4x4 RootTransform;
        public bool FlipX, FlipY;

        public AnimationStartParameters()
        {

        }

        public AnimationStartParameters(AnimDef animation, Map map, Matrix4x4 rootTransform)
        {
            Animation = animation;
            Map = map;
            RootTransform = rootTransform;
        }

        public AnimationStartParameters(AnimDef animation, params Pawn[] pawns)
        {
            Animation = animation;
            if (pawns != null)
            {
                SetMainPawn(pawns[0]);
                for (int i = 1; i < pawns.Length; i++)
                    Pawns.Add(pawns[i]);
            }
        }

        public bool IsValid()
        {
            if (Animation == null || Map == null)
                return false;

            return Animation.pawnCount == Pawns.Count;
        }

        public bool TryTrigger()
        {
            if (!IsValid())
                return false;

            var renderer = Map.GetAnimManager().StartAnimation(Animation, RootTransform, FlipX, FlipY, Pawns.ToArray());
            return renderer != null && !renderer.IsDestroyed;
        }

        public void SetMainPawn(Pawn pawn, bool setTransform = true, bool setMap = true)
        {
            if (pawn == null)
                throw new System.ArgumentNullException(nameof(pawn));

            Pawns.Insert(0, pawn);
            if (setTransform)
                RootTransform = pawn.MakeAnimationMatrix();
            if (setMap)
                Map = pawn.Map;
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref Pawns, "pawns", LookMode.Reference);
            Scribe_Defs.Look(ref Animation, "animation");
            Scribe_Values.Look(ref FlipX, "flipX");
            Scribe_Values.Look(ref FlipY, "flipY");
            Scribe_References.Look(ref Map, "map");
            Scribe_Values.Look(ref RootTransform, "trs");
        }
    }
}
