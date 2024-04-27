using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AM.Events;
using AM.Heads;
using AM.Processing;
using UnityEngine;
using Verse;
using LudeonTK;

namespace AM
{
    public class AnimationManager : MapComponent
    {
        public static ConditionalWeakTable<Pawn, HeadInstance> PawnToHeadInstance { get; } = new ConditionalWeakTable<Pawn, HeadInstance>();
        
        [TweakValue("Melee Animation Mod", 0, 10)]
        public static int CullingPadding = 5;
        public static bool IsDoingMultithreadedSeek { get; private set; }
        public static double MultithreadedSeekTimeMS;

        private static ulong frameLastSeeked;

        public readonly MapPawnProcessor PawnProcessor;

        private readonly List<Action> toDraw = new List<Action>();
        private readonly List<(Pawn pawn, Vector2 position)> labels = new List<(Pawn pawn, Vector2 position)>();
        private readonly List<HeadInstance> heads = new List<HeadInstance>(128);
        private HashSet<AnimRenderer> ioRenderers = new HashSet<AnimRenderer>();

        public AnimationManager(Map map) : base(map)
        {
            PawnProcessor = new MapPawnProcessor(map);
        }

        public override void MapRemoved()
        {
            base.MapRemoved();
            PawnProcessor.Dispose();
            foreach (var head in heads)
            {
                PawnToHeadInstance.Remove(head.Pawn);
            }
            heads.Clear();
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

            RenderHeads(); 
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

        public void AddDroppedHeadFor(Pawn pawn, AnimRenderer animRenderer)
        {
            var head = animRenderer.GetPawnHead(pawn);
            var body = animRenderer.GetPawnBody(pawn);
            if (head == null || body == null)
            {
                Core.Warn("Failed to find head and/or body part to spawn dropped head.");
                return;
            }

            var headSS = animRenderer.GetSnapshot(head);
            var bodySS = animRenderer.GetSnapshot(body);

            var instance = new HeadInstance
            {
                Pawn = pawn,
                Direction = bodySS.GetWorldDirection(),
                Position = headSS.GetWorldPosition(),
                Rotation = headSS.GetWorldRotation(),
                TimeToLive = 120,
                Map = pawn.Map ?? pawn.Corpse?.Map
            };

            heads.Add(instance);

            PawnToHeadInstance.Add(pawn, instance);
        }

        private void RenderHeads()
        {
            for (int i = 0; i < heads.Count; i++)
            {
                var head = heads[i];
                bool stayAlive;
                try
                {
                    stayAlive = head.Render();
                }
                catch (Exception e)
                {
                    Core.Error($"Exception rendering dropped head of {head.Pawn}. Head will be deleted.", e);
                    stayAlive = false;
                }

                if (!stayAlive)
                {
                    PawnToHeadInstance.Remove(head.Pawn);
                    // Remove at swap back, for speed reasons:
                    heads[i] = heads[^1];
                    heads.RemoveAt(heads.Count - 1);
                    i--;
                }
            }
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
