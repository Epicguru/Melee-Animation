using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using EpicUtils;
using UnityEngine;
using Verse;

namespace AAM.UI
{
    public class Dialog_AnimationDebugger : Window
    {
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
        public List<(string name, Action<Listing_Standard> tab)> Tabs = new List<(string name, Action<Listing_Standard> tab)>();

        private Vector2[] scrolls = new Vector2[32];
        private int scrollIndex;
        private AnimDef spaceCheckDef;
        private bool spaceCheckMX, spaceCheckMY;
        private bool drawSpaceCheck;

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
            Tabs.Add(("Layer Debugger", DrawLayerDebugger));
            Tabs.Add(("Animation Space Checker", DrawSpaceChecker));
            Tabs.Add(("List All Active", DrawAllLists));
        }

        private ref Vector2 GetScroll()
        {
            return ref scrolls[scrollIndex++];
        }

        public override void DoWindowContents(Rect inRect)
        {
            scrollIndex = 0;
            drawSpaceCheck = false;

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

            // Space check draw.
            if (drawSpaceCheck && spaceCheckDef != null)
            {
                IntVec3 mp = Verse.UI.MouseCell();
                foreach (var cell in spaceCheckDef.GetMustBeClearCells(spaceCheckMX, spaceCheckMY, mp))
                {
                    GenDraw.DrawTargetHighlight(new LocalTargetInfo(cell));
                }
            }
        }

        private static IEnumerable<Pawn> GetAllPawns()
        {
            if (Find.CurrentMap == null)
                yield break;

            foreach(var pawn in Find.CurrentMap.mapPawns.AllPawnsSpawned)
            {
                if (pawn.Destroyed || pawn.Dead || pawn.Downed)
                    continue;

                if (pawn.RaceProps.Animal)
                    continue;

                yield return pawn;
            }
        }

        private void DrawLayerDebugger(Listing_Standard ui)
        {
            var anim = AnimRenderer.ActiveRenderers.FirstOrFallback();
            if (anim == null)
                return;

            var list = anim.Def.Data.Parts.Select(part => anim.GetSnapshot(part)).ToList();
            list.SortBy(ss => ss.GetWorldPosition().y);

            float bodyBase = anim.GetPart("BodyA")?.GetSnapshot(anim).GetWorldPosition().y ?? AltitudeLayer.Pawn.AltitudeFor();
            float clothesTop = bodyBase + 0.023166021f + 0.0028957527f;
            bool drawnClothes = false;

            foreach (var item in list)
            {
                float y = item.GetWorldPosition().y;
                ui.Label($"<b>{item.PartName}:</b> {y}");

                if (y >= clothesTop && !drawnClothes)
                {
                    drawnClothes = true;
                    ui.Label($"<b><color=cyan>Clothes Top Layer</color>:</b> {clothesTop}");
                }
            }
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
            drawSpaceCheck = true;
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
    }
}
