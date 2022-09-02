using AAM.Tweaks;
using EpicUtils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace AAM.UI
{
    public class Dialog_AnimationDebugger : Window
    {

        public static bool IsInRehearsalMode => startRehearsal && IsStarterOpen;
        public static float trailMinSpeed = 0f, trailMaxSpeed = 25f;

        private static bool IsStarterOpen => Mathf.Abs(lastOpenStarterTime - Time.realtimeSinceStartup) < 0.25f;
        private static MaterialPropertyBlock mpb;
        private static readonly Material mat = MaterialPool.MatFrom(GenDraw.LineTexPath, ShaderDatabase.Transparent, Color.white);
        private static AnimRenderer selectedRenderer;
        private static AnimPartData selectedPart;
        private static SweepPointCollection selectedSweepPath;
        private static float currentUp, currentDown;
        private static Pawn[] startPawns = new Pawn[8];
        private static AnimDef startDef;
        private static bool startMX, startMY;
        private static bool startRehearsal = true;
        private static LocalTargetInfo startTarget = LocalTargetInfo.Invalid;
        private static float lastOpenStarterTime;
        private static int trailSegments = 3;
        private static float trailTime = 0.1f;
        private static ExecutionOutcome executionOutcome = ExecutionOutcome.Kill;

        [DebugAction("Advanced Animation Mod", "Open Debugger", actionType = DebugActionType.Action)]
        private static void OpenInt()
        {
            Open();
        }

        public static Dialog_AnimationDebugger Open()
        {
            var instance = new Dialog_AnimationDebugger();
            Find.WindowStack?.Add(instance);
            return instance;
        }

        public int SelectedIndex;
        public List<(string name, Action<Listing_Standard> tab)> Tabs = new();

        private Vector2[] scrolls = new Vector2[32];
        private int scrollIndex;
        private AnimDef spaceCheckDef;
        private bool spaceCheckMX, spaceCheckMY;
        private bool autoSelectRenderer = true;
        private Queue<Action> toDraw = new();
        private List<AnimationManager> allManagers = new();

        public Dialog_AnimationDebugger()
        {
            closeOnClickedOutside = false;
            doCloseX = true;
            doCloseButton = false;
            preventCameraMotion = false;
            resizeable = true;
            draggable = true;
            closeOnCancel = false;
            closeOnAccept = false;
            onlyOneOfTypeAllowed = false;

            Tabs.Add(("Active Animation Inspector", DrawAllAnimators));
            Tabs.Add(("Animation Starter", DrawAnimationStarter));
            Tabs.Add(("Hierarchy", DrawHierarchy));
            Tabs.Add(("Inspector", DrawInspector));
            Tabs.Add(("Performance Analyzer", DrawPerformanceAnalyzer));
            Tabs.Add(("Animation Space Checker", DrawSpaceChecker));
            Tabs.Add(("List All Active", DrawAllLists));

            if (mpb == null)
            {
                mpb = new MaterialPropertyBlock();
                mpb.SetTexture("_MainTex", mat.mainTexture);
            }
        }

        private ref Vector2 GetScroll()
        {
            return ref scrolls[scrollIndex++];
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (selectedRenderer != null && selectedRenderer.IsDestroyed)
                selectedRenderer = null;
            if (autoSelectRenderer && selectedRenderer == null)
                selectedRenderer = AnimRenderer.ActiveRenderers.FirstOrDefault(r => !r.IsDestroyed && r.Map == Find.CurrentMap);

            scrollIndex = 0;

            var ui = new Listing_Standard();
            ui.Begin(inRect);

            var rect = ui.GetRect(32);
            if (Widgets.ButtonText(rect.LeftHalf(), $"View: {Tabs[SelectedIndex].name}"))
            {
                FloatMenuUtility.MakeMenu(Tabs, p => p.name, p => () =>
                {
                    SelectedIndex = Tabs.IndexOf(p);
                });
            }
            if (Widgets.ButtonText(rect.RightHalf(), "Open new debugger"))
                Open();
            
            ui.GapLine();

            try
            {
                Tabs[SelectedIndex].tab(ui);
            }
            catch (Exception e)
            {
                ui.Label($"<color=red>EXCEPTION DRAWING WINDOW:\n{e}</color>");
            }

            ui.End();
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();

            while (toDraw.TryDequeue(out var action))
                action();
        }

        private void DrawAllAnimators(Listing_Standard ui)
        {
            var area = ui.GetRect(300);
            var oldUI = ui;
            ui = new Listing_Standard();
            int toDraw = AnimRenderer.ActiveRenderers.Count;
            int toDraw2 = AnimRenderer.TotalCapturedPawnCount;
            Widgets.BeginScrollView(area, ref GetScroll(), new Rect(0, 0, area.width - 20, 110 * toDraw + 40 * toDraw2));
            ui.Begin(new Rect(0, 0, area.width - 20, area.height));

            AnimRenderer toDestroy = null;
            foreach(var renderer in AnimRenderer.ActiveRenderers)
            {
                var rect = ui.GetRect(130);

                if (renderer == selectedRenderer)
                {
                    var c = Color.cyan;
                    c.a = 0.3f;
                    Widgets.DrawBoxSolid(rect, c);
                }
                else if (Widgets.ButtonInvisible(rect))
                {
                    selectedRenderer = renderer;
                }

                Widgets.DrawBox(rect);

                string title = $"[{renderer.CurrentTime:F2} / {renderer.Data.Duration:F2}] {renderer.Data.Name}";
                if (renderer.IsDestroyed)
                    title += " <color=red>[DESTROYED]</color>";
                Widgets.Label(rect.ExpandedBy(-4, -4), title);
                var bar = rect.ExpandedBy(-4);
                bar.yMin += 22;
                bar.height = 14;
                float lerp = Mathf.Clamp01(renderer.CurrentTime / renderer.Data.Duration);
                var fillBar = bar;
                fillBar.width = bar.width * lerp;
                Widgets.DrawBoxSolid(bar, Color.grey);
                Widgets.DrawBoxSolid(fillBar, Color.green);
                foreach (var e in renderer.GetEventsInPeriod(new Vector2(0, renderer.Duration + 1f)))
                {
                    float p = e.Time / renderer.Duration;
                    Widgets.DrawBoxSolid(new Rect(bar.x + p * bar.width, bar.y, 3, bar.height), Color.red);
                }

                bar.y += 18;
                Widgets.DrawBoxSolid(bar, Color.white * 0.45f);
                float newLerp = Widgets.HorizontalSlider(bar.ExpandedBy(0, -2), lerp, 0, 1);
                if (newLerp != lerp)
                    renderer.Seek(newLerp * renderer.Data.Duration, null);

                rect.y += 20;

                int i = 0;
                foreach(var pawn in renderer.Pawns)
                {
                    if (pawn == null)
                        continue;

                    bool hasJob = pawn?.CurJobDef == AAM_DefOf.AAM_InAnimation;

                    string label = pawn.LabelShort;
                    Color col = Color.white;
                    if (pawn.Dead)
                    {
                        label += " [DEAD]";
                        col = Color.red;
                    }
                    else if (pawn.Destroyed)
                    {
                        label += " [DESTROYED]";
                        col = Color.red;
                    }
                    else if (!pawn.Spawned)
                    {
                        label += " [NOT_SPAWNED]";
                        col = Color.red;
                    }
                    else if (!hasJob)
                    {
                        label += $"[BADJOB:{pawn?.CurJobDef.defName ?? "null"}]";
                        col = Color.red;
                    }

                    Rect b = rect.ExpandedBy(-4, -4);
                    b.yMin += 40;
                    b.width = 100;
                    b.height = 28;
                    b.x += i * 110;

                    Widgets.DrawBox(b);
                    GUI.color = col;
                    Widgets.LabelFit(b.ExpandedBy(-4, -2), label);
                    GUI.color = Color.white;

                    i++;
                }

                Rect stop = rect.ExpandedBy(-4);
                stop.y += 72;
                stop.size = new Vector2(100, 28);
                if(Widgets.ButtonText(stop, "Stop"))
                {
                    toDestroy = renderer;
                }

                ui.GapLine(10);
            }
            foreach(var pawn in AnimRenderer.CapturedPawns)
            {
                Widgets.Label(ui.GetRect(30).ExpandedBy(-4, -4), pawn.NameFullColored);
            }

            if (toDestroy != null)
                toDestroy.Destroy();

            ui.End();
            Widgets.EndScrollView();
            ui = oldUI;
            ui.GapLine();
        }

        private void DrawSpaceChecker(Listing_Standard ui)
        {
            if (Event.current.type == EventType.Repaint)
            {
                toDraw.Enqueue(() =>
                {
                    IntVec3 mp = Verse.UI.MouseCell();
                    foreach (var cell in spaceCheckDef.GetMustBeClearCells(spaceCheckMX, spaceCheckMY, mp))
                    {
                        GenDraw.DrawTargetHighlight(new LocalTargetInfo(cell));
                    }
                });
            }

            if (ui.ButtonText(spaceCheckDef?.LabelCap.ToString() ?? "<Select Def>"))
            {
                var items = BetterFloatMenu.MakeItems(AnimDef.AllDefs, d => new MenuItemText(d, d.LabelCap, tooltip: d.defName));
                BetterFloatMenu.Open(items, i =>
                {
                    spaceCheckDef = i.GetPayload<AnimDef>();
                });
            }

            ui.CheckboxLabeled("Mirror X", ref spaceCheckMX);
            ui.CheckboxLabeled("Mirror Y", ref spaceCheckMY);
        }

        private void DrawAllLists(Listing_Standard ui)
        {
            ui.maxOneColumn = false;
            ui.ColumnWidth = 300;

            ui.Label("<b>ALL RENDERERS</b>");
            ui.GapLine();
            foreach (var renderer in AnimRenderer.ActiveRenderers)
            {
                ui.Label($"Dest.: {renderer.IsDestroyed}, Map: {renderer.Map?.ToString() ?? "<null>"}, Invalid map: {renderer.Map == null || renderer.Map.Index < 0}, PawnCount: {renderer.PawnCount}, Animation: {renderer.Def?.LabelCap ?? "<null>"}");
            }

            ui.NewColumn();
            ui.Gap(45);
            ui.Label("<b>PAWN TO RENDERER</b>");
            ui.GapLine();
            foreach (var pawn in AnimRenderer.CapturedPawns)
            {
                var renderer = AnimRenderer.TryGetAnimator(pawn);
                ui.Label($"{pawn.LabelShort} -> {renderer.Def} @ {renderer.CurrentTime} on {renderer.Map}");
            }
        }

        private void DrawHierarchy(Listing_Standard ui)
        {
            if (selectedRenderer == null)
            {
                ui.Label("No animator selected. Select an animator using the Active Animation Inspector window.");
                return;
            }

            #region Build Hierarchy
            var roots = new List<Node>();
            var toAdd = new List<AnimPartData>(selectedRenderer.Def.Data.Parts);

            bool Insert(AnimPartData part)
            {
                if (part.Parent == null)
                {
                    roots.Add(new Node(part));
                    return true;
                }
                foreach (var node in roots)
                {
                    if (node.Insert(part))
                        return true;
                }
                return false;
            }

            int max = 1000;
            while (toAdd.Count > 0)
            {
                toAdd.RemoveAll(Insert);

                max--;
                if (max == 0)
                {
                    Core.Error("MAX HIT! Bugged animation?");
                    break;
                }
            }
            #endregion

            void DrawNode(Node node)
            {
                var rect = ui.GetRect(28);
                Widgets.DrawHighlightIfMouseover(rect);
                if (Widgets.ButtonInvisible(rect))
                {
                    selectedPart = node.Part;
                }
                if (selectedPart == node.Part)
                {
                    var c = Color.cyan;
                    c.a = 0.25f;
                    Widgets.DrawBoxSolid(rect, c);
                }

                bool active = selectedRenderer.GetSnapshot(node.Part).Active;
                GUI.color = !active ? Color.Lerp(Color.red, Color.grey, 0.5f) : Color.white;
                Widgets.Label(rect, node.Part.Name);
                GUI.color = Color.white;

                ui.Indent(20);
                foreach (var child in node.Children)
                    DrawNode(child);
                ui.Outdent(20);
            }

            foreach (var node in roots)
                DrawNode(node);
        }

        private void DrawInspector(Listing_Standard ui)
        {
            if (selectedRenderer == null || selectedPart == null)
            {
                ui.Label($"No animator {selectedRenderer} or part {selectedPart} selected. Select an animator using the Active Animation Inspector window, and part using the Hierarchy window.");
                return;
            }

            var ss = selectedRenderer.GetSnapshot(selectedPart);
            if (ss.Part == null)
            {
                Core.Error("Bad snapshot, part is probably outdated.");
                selectedPart = null;
                return;
            }

            ui.Label($"Part: {selectedPart.Name} ({selectedPart.Path})");
            ui.Label($"Tex: '{selectedPart.TexturePath}'");
            ui.GapLine();
            ui.Label("<color=cyan>CURRENT INFO</color>");
            ui.Gap(6);
            ui.Label($"Active: {ss.Active} (self: {selectedPart.Active.Evaluate(selectedRenderer.CurrentTime)})");
            ui.Label($"Local pos: {ss.LocalPosition}");
            ui.Label($"World pos: {ss.GetWorldPosition()}");
            ui.Label($"Local rot: {ss.LocalRotation}");
            ui.Label($"World rot: {ss.GetWorldRotation()}");
            ui.Label($"World dir: {ss.Direction}");

            ui.GapLine();

            if (Event.current.type == EventType.Repaint)
            {
                toDraw.Enqueue(() =>
                {
                    var pos = ss.GetWorldPosition();
                    var dir = ss.GetWorldRotation().AngleToWorldDir();
                    GenDraw.DrawLineBetween(pos, pos + dir, SimpleColor.Blue);
                });
            }
        }

        private void DrawAnimationStarter(Listing_Standard ui)
        {
            ui.Label("Here you can make your pawns perform a specific animation for cinematic purposes, or just to inspect an animation.");
            ui.GapLine();

            if (Find.CurrentMap == null)
            {
                ui.Label("You must be on a map to use this tool.");
                return;
            }

            // Animation selection.
            if (ui.ButtonText($"Animation: {startDef?.LabelCap ?? "<None>"}"))
            {
                var items = BetterFloatMenu.MakeItems(AnimDef.AllDefs, d => new MenuItemText(d, d.LabelCap, tooltip: $"[{d.type}]\nData: {d.DataPath}\nPawns: {d.pawnCount}\n{d.description}".TrimEnd()));
                BetterFloatMenu.Open(items, i =>
                {
                    startDef = i.GetPayload<AnimDef>();
                });
            }
            if (startDef == null)
                return;

            int validSteps = 0;

            // Pawn selector list.
            if (startDef.pawnCount > 0)
            {
                int validPawns = 0;
                ui.Label($"{startDef.LabelCap} requires {startDef.pawnCount} pawns:");
                ui.GapLine();
                for (int i = 0; i < startPawns.Length; i++)
                {
                    if (i >= startDef.pawnCount)
                    {
                        startPawns[i] = null;
                        continue;
                    }

                    var pawn = startPawns[i];
                    if (pawn != null && !pawn.Spawned)
                        pawn = null;

                    string name = pawn != null ? pawn.NameShortColored : "<Missing>";

                    var rect = ui.GetRect(28);
                    if (pawn != null)
                    {
                        Widgets.ThingIcon(rect.LeftPartPixels(30), pawn);
                        validPawns++;
                    }

                    if (Widgets.ButtonText(rect.RightPartPixels(rect.width - 30), name))
                    {
                        var items = BetterFloatMenu.MakeItems(Find.CurrentMap.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.Humanlike), p2 => new PawnMenuItem(p2, $"[{p2.Faction?.Name ?? "NoFac"}] {p2.NameFullColored}\n{p2.def.LabelCap}".TrimEnd()));
                        int i1 = i;
                        BetterFloatMenu.Open(items, m =>
                        {
                            startPawns[i1] = m.GetPayload<Pawn>();
                        });
                    }
                }
                ui.GapLine();
                if (validPawns != startDef.pawnCount)
                {
                    ui.Label($"<color=red><b>Wrong number of pawns! The animation expects {startDef.pawnCount}, you have selected {validPawns}</b></color>");
                }
                else
                {
                    validSteps++;
                }
            }

            // Option: Start position.
            if (!startTarget.IsValid || startTarget.ThingDestroyed)
                startTarget = IntVec3.Invalid;

            if (!startTarget.IsValid)
            {
                if (startDef.pawnCount > 0 && startPawns[0] != null && startPawns[0].Spawned)
                    startTarget = startPawns[0];
            }
            else
            {
                validSteps++;
            }
            ui.Label($"Start position: {(startTarget.IsValid ? startTarget.Thing is Pawn p ? p.NameShortColored : startTarget.ToString() : "<color=red><b><NONE></b></color>")}");
            var selRect = ui.GetRect(28);
            if (Mouse.IsOver(selRect) && startTarget.IsValid)
                GenDraw.DrawTargetHighlight(startTarget);
            
            if (Widgets.ButtonText(selRect, "Select position or pawn..."))
            {
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(null);
                var args = new TargetingParameters()
                {
                    canTargetLocations = true,
                    canTargetAnimals = false,
                    canTargetBuildings = false,
                    canTargetMechs = false,

                };
                Find.Targeter.BeginTargeting(args, t =>
                {
                    startTarget = t;
                });
            }

            // Option: Rehearsal.
            ui.CheckboxLabeled("Rehearsal mode", ref startRehearsal, "When in rehearsal mode, pawns are not killed or injured, and blood splatters do not spawn.");
            if (startRehearsal)
                lastOpenStarterTime = Time.realtimeSinceStartup;

            if (ui.ButtonTextLabeled("Execution outcome: ", executionOutcome.ToString()))
            {
                var options = new ExecutionOutcome[]
                {
                    ExecutionOutcome.Nothing,
                    ExecutionOutcome.Damage,
                    ExecutionOutcome.Down,
                    ExecutionOutcome.Kill
                };
                BetterFloatMenu.Open(BetterFloatMenu.MakeItems(options, i => new MenuItemText(i, i.ToString())), i =>
                {
                    executionOutcome = i.GetPayload<ExecutionOutcome>();
                });
            }

            ui.Gap();

            // Options: flip X & Y
            ui.CheckboxLabeled("Mirror Horizontally", ref startMX, "Should the animation be mirrored horizontally? The default direction is facing east.");
            ui.CheckboxLabeled("Mirror Vertically", ref startMY, "Should the animation be mirrored vertically?");

            // Start button.
            ui.Gap();
            GUI.color = validSteps == 2 ? Color.white : Color.red;
            if (ui.ButtonText("<color=white>Start Now</color>"))
            {
                // Remove existing animations from the pawns involved.
                Pawn[] pawns = new Pawn[startDef.pawnCount];
                for (int i = 0; i < startDef.pawnCount; i++)
                {
                    var pawn = startPawns[i];
                    if (pawn == null || !pawn.Spawned)
                        continue;

                    if (pawn.IsInAnimation(out var anim))
                        anim.Destroy();

                    pawns[i] = pawn;
                }

                var sp = new AnimationStartParameters(startDef, pawns)
                {
                    Animation = startDef,
                    FlipX = startMX,
                    FlipY = startMY,
                    Map = Find.CurrentMap,
                    RootTransform = startTarget.MakeAnimationMatrix(),
                    ExecutionOutcome = executionOutcome
                };

                if (!sp.TryTrigger())
                    Messages.Message("Animation failed to start! Check debug log for details.", LookTargets.Invalid, MessageTypeDefOf.RejectInput, false);
            }
            GUI.color = Color.white;
        }

        private static void DrawLineBetween(in Vector3 A, in Vector3 B, float len, in Color color, float yOff, float width = 0.2f)
        {
            mpb.SetFloat("_Alpha", color.r);

            Vector3 mid = (A + B) * 0.5f;
            mid.y += yOff;
            var rot = Quaternion.Euler(0, (B - A).ToAngleFlat(), 0);
            var trs = Matrix4x4.TRS(mid, rot, new Vector3(len, 1, width));
            Graphics.DrawMesh(MeshPool.plane10, trs, Content.TrailMaterial, 0, null, 0, mpb);
        }

        private void DrawPerformanceAnalyzer(Listing_Standard ui)
        {
            allManagers.Clear();
            allManagers.AddRange(Find.Maps.Select(m => m.GetAnimManager()));

            ui.Label("<b><color=cyan>SCAN TIMES</color></b>");

            double totalScanTime = 0;
            double totalProcessTime = 0;
            foreach (var manager in allManagers)
            {
                var pp = manager.PawnProcessor;
                totalScanTime += pp.LastListUpdateTimeMS;
                totalProcessTime += pp.LastProcessTimeMS;
                ui.Label($"<b>Map: {manager.map}</b>");
                ui.Indent();
                ui.Label($"Last scan time: {pp.LastListUpdateTimeMS:F2} ms");

                string extra = null;
                if (pp.LastProcessTimeMS >= Core.Settings.MaxCPUTimePerTick)
                    extra = "<color=red>[!!!]</color>";
                ui.Label($"Last process time: {pp.LastProcessTimeMS:F2} ms  {extra}");
                ui.Label($"Process average interval per pawn: {pp.ProcessAverageInterval:F3} ms ({pp.ProcessAverageInterval / (100f / 6f):F0} ticks)");
                ui.Label("Last process pass involved:");
                ui.Indent();
                ui.Label($"Processed {pp.LastProcessedPawnCount} out of {pp.TargetProcessedPawnCount} ({(float)pp.LastProcessedPawnCount / pp.TargetProcessedPawnCount * 100f:F0}%) pawns.");
                ui.Label($"Checked {pp.LastAnimationsConsideredCount * 2} animations against {pp.LastTargetsConsideredCount} targets.");
                ui.Label($"This caused {pp.LastCellsConsideredCount} cells to be checked.");
                ui.Outdent();
                ui.Outdent();
            }

            ui.Gap();
            ui.Label($"Total scan time: {totalScanTime:F3} ms");
            ui.Label($"Total process time: {totalProcessTime:F3} ms ({totalProcessTime / Core.Settings.MaxCPUTimePerTick * 100f:F0}% of limit) {(totalProcessTime >= Core.Settings.MaxCPUTimePerTick ? "  <color=red>[!!!]</color>" : "")}");
            ui.GapLine();

            ui.Label("<b><color=cyan>RENDERING</color></b>");
            ui.Label($"Active: {AnimRenderer.ActiveRenderers.Count} renderers.");
            ui.Label("<b>Total times:</b>");
            ui.Indent();
            ui.Label($"Events: {AnimRenderer.EventsTimer.Elapsed.TotalMilliseconds:F3} ms");
            ui.Label($"Seek: {AnimRenderer.SeekTimer.Elapsed.TotalMilliseconds:F3} ms");
            ui.Label($"Draw: {AnimRenderer.DrawTimer.Elapsed.TotalMilliseconds:F3} ms");
            ui.Outdent();

            var curve = new SimpleCurve();
            for (int i = 0; i < 100; i++)
            {
                float x = i;
                float y = Mathf.Sin(x * Mathf.Deg2Rad);
                curve.Add(x, y, false);
            }

            var drawInfo = new SimpleCurveDrawInfo()
            {
                color = Color.green,
                curve = curve,
                label = "My curve",
                valueFormat = "F2"
            };

            SimpleCurveDrawer.DrawCurveLines(ui.GetRect(200), drawInfo, false, new Rect(0, 0, 100, 1), true, true);

            // Lazy :)
            if(Event.current.type == EventType.Repaint)
                AnimRenderer.ResetTimers();
        }

        private IEnumerable<(Vector3 start, Vector3 end, float speed)> MakeSegments(Vector3 down, Vector3 up, int segmentCount, float bottomVel, float topVel)
        {
            for (int i = 0; i < segmentCount; i++)
            {
                float st = ((float)i / segmentCount);
                float et = ((float)(i + 1) / segmentCount);

                yield return (Vector3.Lerp(down, up, st), Vector3.Lerp(down, up, et), Mathf.Lerp(bottomVel, topVel, et));
            }
        }

        private struct Node
        {
            public AnimPartData Part;
            public List<Node> Children;
            public bool Expanded;

            public Node(AnimPartData part)
            {
                Part = part;
                Children = new List<Node>(8);
                Expanded = true;
            }

            public void SetExpanded(bool value, bool withChildren)
            {
                Expanded = value;

                if (!withChildren)
                    return;

                foreach (var node in Children)
                    node.SetExpanded(value, true);
            }

            public bool Insert(AnimPartData part)
            {
                if (part.Parent == Part)
                {
                    Children.Add(new Node(part));
                    return true;
                }
                foreach (var child in Children)
                {
                    if (child.Insert(part))
                        return true;
                }
                return false;
            }
        }

        private class PawnMenuItem : MenuItemBase
        {
            public Pawn Pawn
            {
                get
                {
                    return GetPayload<Pawn>();
                }
                set
                {
                    Payload = value;
                }
            }
            public float CamDistance
            {
                get
                {
                    if (_camDst == null)
                    {
                        var camPos = Find.CameraDriver.MapPosition.ToVector3().ToFlat();
                        var pawnPos = Pawn.DrawPos.ToFlat();
                        _camDst = (camPos - pawnPos).magnitude;
                    }

                    return _camDst.Value;
                }
            }
            public Vector2 Size = new(212f, 28f);
            public string Tooltip;
            public string CustomName;

            private float? _camDst;
            private string searchLabel = "";
            private bool consumedSearch;

            public PawnMenuItem(Pawn pawn, string tooltip = null)
            {
                this.Pawn = pawn;
                this.Tooltip = tooltip;
            }

            public override bool MatchesSearch(string search)
            {
                searchLabel = BetterFloatMenu.SearchMatch(CustomName ?? Pawn.Name.ToStringFull, search);
                consumedSearch = false;
                return searchLabel != null;
            }

            public override int CompareTo(MenuItemBase other)
            {
                if (other is not PawnMenuItem pm)
                    return 0;

                return CamDistance.CompareTo(pm.CamDistance);
            }

            public override Vector2 Draw(Vector2 pos)
            {
                Rect area = new(pos, Size);
                var iconRect = area.LeftPartPixels(32);
                var textRect = area.RightPartPixels(area.width - 32);

                if (Pawn == null || Pawn.Destroyed)
                {
                    Widgets.Label(textRect, CustomName ?? "<i><None></i>");
                    return Size;
                }

                string ovLabel = !consumedSearch ? searchLabel : null;
                consumedSearch = true;

                Widgets.ThingIcon(iconRect, Pawn);
                Widgets.Label(textRect, ovLabel ?? CustomName ?? Pawn.Name.ToStringFull);

                consumedSearch = true;

                if(Tooltip != null)
                    TooltipHandler.TipRegion(area, Tooltip);

                return Size;
            }
        }
    }
}
