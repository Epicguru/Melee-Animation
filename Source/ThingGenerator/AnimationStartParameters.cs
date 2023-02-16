using System;
using System.Collections.Generic;
using System.Drawing;
using System.Security.Cryptography;
using UnityEngine;
using Verse;

namespace AAM
{
    public struct AnimationStartParameters : IExposable
    {
        public Pawn MainPawn, SecondPawn;
        public List<Pawn> ExtraPawns;
        public AnimDef Animation;
        public Map Map;
        public Matrix4x4 RootTransform;
        public bool FlipX, FlipY;
        public ExecutionOutcome ExecutionOutcome = ExecutionOutcome.Down;

        public AnimationStartParameters(AnimDef animation, Map map, Matrix4x4 rootTransform)
        {
            MainPawn = null;
            SecondPawn = null;
            ExtraPawns = null;
            Animation = animation;
            Map = map;
            RootTransform = rootTransform;
            FlipX = false;
            FlipY = false;
        }

        public AnimationStartParameters(AnimDef animation, params Pawn[] pawns)
        {
            MainPawn = null; // Assigned at bottom of constructor.
            SecondPawn = pawns.Length >= 2 ? pawns[1] : null;
            ExtraPawns = null;
            if (pawns.Length > 2)
            {
                ExtraPawns = new List<Pawn>();
                for (int i = 2; i < pawns.Length; i++)
                    ExtraPawns.Add(pawns[i]);
            }
            Animation = animation;
            Map = null;
            RootTransform = Matrix4x4.identity;
            FlipX = false;
            FlipY = false;
 
            if(pawns.Length >= 1)
                SetMainPawn(pawns[0]);

        }

        public bool IsValid()
        {
            if (Animation == null || Map == null)
                return false;

            return Animation.pawnCount == PawnCount();
        }

        public int PawnCount() => (MainPawn != null ? 1 : 0) + (SecondPawn != null ? 1 : 0) + (ExtraPawns?.Count ?? 0);

        public bool TryTrigger() => TryTrigger(out _);

        public bool TryTrigger(out AnimRenderer animation)
        {
            animation = null;
            if (!IsValid())
                return false;

            var renderer = new AnimRenderer(Animation, Map)
            {
                RootTransform = RootTransform,
                MirrorHorizontal = FlipX,
                MirrorVertical = FlipY,
                ExecutionOutcome = ExecutionOutcome
            };

            foreach (var pawn in EnumeratePawns())
                renderer.AddPawn(pawn);

            animation = renderer;
            return renderer.Register();
        }

        public IEnumerable<Pawn> EnumeratePawns()
        {
            if(MainPawn != null)
                yield return MainPawn;
            if(SecondPawn != null)
                yield return SecondPawn;

            if (ExtraPawns == null)
                yield break;

            foreach (var pawn in ExtraPawns)
                yield return pawn;
        }

        public void SetMainPawn(Pawn pawn, bool setTransform = true, bool setMap = true)
        {
            MainPawn = pawn ?? throw new ArgumentNullException(nameof(pawn));
            if (setTransform)
                RootTransform = pawn.MakeAnimationMatrix();
            if (setMap)
                Map = pawn.Map;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref MainPawn, "mainPawn");
            Scribe_References.Look(ref MainPawn, "secondPawn");
            Scribe_Collections.Look(ref ExtraPawns, "pawns", LookMode.Reference);
            Scribe_Defs.Look(ref Animation, "animation");
            Scribe_Values.Look(ref FlipX, "flipX");
            Scribe_Values.Look(ref FlipY, "flipY");
            Scribe_References.Look(ref Map, "map");
            Scribe_Values.Look(ref RootTransform, "trs");
        }

        public override string ToString()
        {
            return $"{Animation?.defName} with {PawnCount()} pawns, flipX: {FlipX}.";
        }
    }
}
