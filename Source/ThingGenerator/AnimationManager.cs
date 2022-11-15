using AAM.Processing;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AAM
{
    public class AnimationManager : MapComponent
    {
        public static Texture2D HandTexture;

        public static void Init()
        {
            HandTexture = ContentFinder<Texture2D>.Get("AAM/Hand");
        }

        public MapPawnProcessor PawnProcessor;

        private readonly List<Action> toDraw = new();
        private readonly List<(Pawn pawn, Vector2 position)> labels = new();
        private HashSet<AnimRenderer> ioRenderers = new();

        public AnimationManager(Map map) : base(map)
        {
            PawnProcessor = new MapPawnProcessor(map);
        }

        /// <summary>
        /// Attempts to start an animation given an animation def, and a position (<paramref name="rootTransform"/>).
        /// You may also optionally supply one or more pawns to participate in the animation, if the animation requires them.
        /// The caller should pass in as many pawns as the animation is designed for. Passing in too few or too many pawns may lead
        /// to undefined behaviour.
        /// Passing in an 'invalid' pawn (one that is dead, downed, or otherwise inadequate for the animation) will result in the animation
        /// being immediately cancelled and never started. Check the <see cref="AnimRenderer.IsDestroyed"/> field of the return value to check that
        /// the animation started successfully.
        /// </summary>
        /// <returns>The created animation renderer, or null if the <paramref name="def"/> was null.</returns>
        public AnimRenderer StartAnimation(AnimDef def, Matrix4x4 rootTransform, bool mirrorX, bool mirrorY, IEnumerable<Pawn> pawns)
        {
            if (def?.Data == null)
                return null;

            var renderer = new AnimRenderer(def, map);
            renderer.RootTransform = rootTransform;
            renderer.MirrorHorizontal = mirrorX;
            renderer.MirrorVertical = mirrorY;

            if (pawns != null)
                foreach (var pawn in pawns)
                    renderer.AddPawn(pawn);

            renderer.Register();

            return renderer;
        }

        public void AddPostDraw(Action draw)
        {
            if (draw != null)
                toDraw.Add(draw);
        }

        public override void MapComponentUpdate()
        {
            base.MapComponentUpdate();

            float dt = Time.deltaTime * Find.TickManager.TickRateMultiplier * (Input.GetKey(KeyCode.LeftShift) ? 0.1f : 1f);
            Draw(dt);

            foreach (var action in toDraw)
                action?.Invoke();
            toDraw.Clear();
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            PawnProcessor.Tick();
        }

        public void Draw(float deltaTime)
        {
            labels.Clear();
            AnimRenderer.DrawAll(deltaTime, map, DrawLabel);
        }

        private void DrawLabel(Pawn pawn, Vector2 position)
        {
            labels.Add((pawn, position));
        }

        public override void ExposeData()
        {
            base.ExposeData();

            ioRenderers ??= new HashSet<AnimRenderer>();

            switch (Scribe.mode)
            {
                case LoadSaveMode.Saving:
                    ioRenderers.Clear();

                    // Collect the active renderers that are on this map.
                    foreach (var renderer in AnimRenderer.ActiveRenderers)
                    {
                        if (!renderer.IsDestroyed && renderer.Map == this.map)
                        {
                            if (!ioRenderers.Add(renderer))
                                Core.Error("There was a duplicate renderer in the list!");
                        }
                    }

                    // Save.
                    Scribe_Collections.Look(ref ioRenderers, "animationRenderers", LookMode.Deep);
                    break;

                case LoadSaveMode.LoadingVars:
                    AnimRenderer.ClearAll(); // Remove all previous renderers from the system.
                    ioRenderers.Clear();
                    Scribe_Collections.Look(ref ioRenderers, "animationRenderers", LookMode.Deep);
                    break;
                case LoadSaveMode.ResolvingCrossRefs:
                    Scribe_Collections.Look(ref ioRenderers, "animationRenderers", LookMode.Deep);
                    break;
                case LoadSaveMode.PostLoadInit:
                    Scribe_Collections.Look(ref ioRenderers, "animationRenderers", LookMode.Deep);

                    // Write back to general system.
                    AnimRenderer.PostLoadPendingAnimators.AddRange(ioRenderers);

                    Core.Log($"Loaded {ioRenderers.Count} animation renderers on map {map}");
                    ioRenderers.Clear();
                    break;
            }
        }
    }
}
