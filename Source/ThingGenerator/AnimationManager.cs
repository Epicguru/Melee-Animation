using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AM.Events;
using AM.Processing;
using UnityEngine;
using Verse;

namespace AM
{
    public class AnimationManager : MapComponent
    {
        [TweakValue("Melee Animation Mod", 0, 10)]
        public static int CullingPadding = 5;
        public static bool IsDoingMultithreadedSeek { get; private set; }
        public static double MultithreadedSeekTimeMS;
        public static Texture2D HandTexture;

        private static ulong frameLastSeeked;

        public static void Init()
        {
            HandTexture = ContentFinder<Texture2D>.Get("AM/Hand");
        }

        public readonly MapPawnProcessor PawnProcessor;

        private readonly List<Action> toDraw = new List<Action>();
        private readonly List<(Pawn pawn, Vector2 position)> labels = new List<(Pawn pawn, Vector2 position)>();
        private HashSet<AnimRenderer> ioRenderers = new HashSet<AnimRenderer>();

        public AnimationManager(Map map) : base(map)
        {
            PawnProcessor = new MapPawnProcessor(map);
        }

        public override void MapRemoved()
        {
            base.MapRemoved();
            PawnProcessor.Dispose();
        }

        public void AddPostDraw(Action draw)
        {
            if (draw != null)
                toDraw.Add(draw);
        }

        public override void MapComponentUpdate()
        {
            base.MapComponentUpdate();

            float dt = Time.deltaTime * Find.TickManager.TickRateMultiplier;
            if (Find.TickManager.Paused)
                dt = 0f;

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

        public override void MapComponentOnGUI()
        {
            base.MapComponentOnGUI();

            AnimRenderer.DrawAllGUI(map);

            foreach (var pair in labels)
            {
                GenMapUI.DrawPawnLabel(pair.pawn, pair.position);
            }
        }

        public void Draw(float deltaTime)
        {
            labels.Clear();
            IsDoingMultithreadedSeek = Core.Settings.MultithreadedMatrixCalculations && AnimRenderer.ActiveRenderers.Count >= 10 && Core.Settings.MaxProcessingThreads != 1;
            if (IsDoingMultithreadedSeek)
                SeekMultithreaded(deltaTime);

            var viewBounds = Find.CameraDriver.CurrentViewRect.ExpandedBy(CullingPadding);

            Action<AnimRenderer, EventBase> onEvent = null;
            if (!IsDoingMultithreadedSeek)
                onEvent = DoEvent;

            AnimRenderer.DrawAll(deltaTime, map, onEvent, viewBounds, Core.Settings.DrawNamesInAnimation ? DrawLabel : null);
            AnimRenderer.RemoveDestroyed();
        }

        private static readonly List<(AnimRenderer, EventBase)> eventsToDo = new List<(AnimRenderer, EventBase)>(128);
        
        private static void SeekMultithreaded(float dt)
        {
            if (frameLastSeeked == GameComp.FrameCounter)
                return;

            frameLastSeeked = GameComp.FrameCounter;

            var timer = new RefTimer();

            if (dt <= 0)
            {
                timer.GetElapsedMilliseconds(out MultithreadedSeekTimeMS);
                return;
            }

            // Processing:
            eventsToDo.Clear();
            Parallel.For(0, AnimRenderer.ActiveRenderers.Count, i =>
            {
                var animator = AnimRenderer.ActiveRenderers[i];

                if (animator.IsDestroyed)
                    return;

                animator.Seek(null, dt, e => eventsToDo.Add((animator, e)), true);
            });

            // Events:
            foreach (var pair in eventsToDo)
                DoEvent(pair.Item1, pair.Item2);

            timer.GetElapsedMilliseconds(out MultithreadedSeekTimeMS);
        }

        private static void DoEvent(AnimRenderer r, EventBase ev)
        {
            try
            {
                EventHelper.Handle(ev, r);
                Core.Log($"Did event {ev} for {r}");
            }
            catch (Exception e)
            {
                Core.Error($"Exception handling event (mt) for {r} ({ev})", e);
            }
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
                        if (renderer.ShouldSave && renderer.Map == this.map)
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
