using AAM.Processing;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace AAM
{
    public class AnimationManager : MapComponent
    {
        [TweakValue("Melee Animation Mod", 0, 10)]
        public static int CullingPadding = 5;
        public static bool IsDoingMultithreadedSeek { get; private set; }
        public static int MultithreadedThreadsUsed { get; private set; }
        public static double MultithreadedSeekTimeMS;
        public static Texture2D HandTexture;

        private static readonly List<Vector2> seekTimes = new List<Vector2>();
        private static readonly List<Task> tasks = new List<Task>();
        private static ulong frameLastSeeked;

        public static void Init()
        {
            HandTexture = ContentFinder<Texture2D>.Get("AAM/Hand");
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

            AnimRenderer.DrawAll(deltaTime, map, viewBounds, IsDoingMultithreadedSeek ? seekTimes : null, Core.Settings.DrawNamesInAnimation ? DrawLabel : null);
            AnimRenderer.RemoveDestroyed(IsDoingMultithreadedSeek ? seekTimes : null);
        }

        private static void SeekMultithreaded(float dt)
        {
            if (frameLastSeeked == GameComp.FrameCounter)
                return;

            frameLastSeeked = GameComp.FrameCounter;

            var timer = new RefTimer();

            seekTimes.Clear();
            for (int i = 0; i < AnimRenderer.ActiveRenderers.Count; i++)
                seekTimes.Add(new Vector2(-1, -1));

            if (dt <= 0)
            {
                timer.GetElapsedMilliseconds(out MultithreadedSeekTimeMS);
                return;
            }

            // Spawn all tasks.
            tasks.Clear();
            foreach (var slice in MapPawnProcessor.MakeProcessingSlices(AnimRenderer.ActiveRenderers.Count))
            {
                tasks.Add(Task.Run(() =>
                {
                    for (int i = slice.min; i <= slice.max; i++)
                    {
                        var animator = AnimRenderer.ActiveRenderers[i];

                        if (animator.IsDestroyed)
                            continue;

                        seekTimes[i] = animator.Seek(null, dt);
                    }
                }));
            }
            MultithreadedThreadsUsed = tasks.Count;

            // Wait all, the lazy no-allocation way.
            foreach (var task in tasks)
            {
                try
                {
                    task.Wait();
                }
                catch (Exception e)
                {
                    Core.Error("Multithreaded seek error:", e);
                }
            }

            tasks.Clear();

            timer.GetElapsedMilliseconds(out MultithreadedSeekTimeMS);
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
