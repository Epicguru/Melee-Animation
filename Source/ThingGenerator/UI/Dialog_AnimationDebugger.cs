using AAM.Calculators;
using RimWorld;
using System.Collections.Generic;
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

        public Pawn PawnA, PawnB;

        private Vector2[] scrolls = new Vector2[32];
        private int scrollIndex;
        private DuelOutcome outcome;

        public Dialog_AnimationDebugger()
        {
            closeOnClickedOutside = false;
            doCloseX = true;
            doCloseButton = false;
            preventCameraMotion = false;
            resizeable = true;
            draggable = true;
        }

        private ref Vector2 GetScroll()
        {
            return ref scrolls[scrollIndex++];
        }

        public override void DoWindowContents(Rect inRect)
        {
            scrollIndex = 0;

            var ui = new Listing_Standard();
            ui.Begin(inRect);
                
            DrawAllAnimators(ui);
            //DrawPawnInputs(ui);
            //DrawDuelOutcome(ui);

            var def = DefDatabase<AnimDef>.GetNamed("AMM_Execution_Passover");
            if (def != null)
            {
                int count = 0;
                foreach (var weapon in def.GetAllAllowedWeapons())
                {
                    count++;
                    ui.Label(weapon.LabelCap);
                }
                ui.Label($"Those are the {count} weapons allowed for {def.LabelCap} ({def.allowedWeaponTypes})");
            }
            else
            {
                ui.Label($"Failed to find def.");
            }

            ui.End();
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

        private void DrawPawnInputs(Listing_Standard ui)
        {
            if(ui.ButtonText($"Pawn A: {PawnA?.NameShortColored ?? "---"}"))
            {
                FloatMenuUtility.MakeMenu(GetAllPawns(), p => p.NameFullColored, p => () =>
                {
                    PawnA = p;
                });
            }
            if (ui.ButtonText($"Pawn B: {PawnB?.NameShortColored ?? "---"}"))
            {
                FloatMenuUtility.MakeMenu(GetAllPawns(), p => p.NameFullColored, p => () =>
                {
                    PawnB = p;
                });
            }
            ui.GapLine();
        }

        private void DrawDuelOutcome(Listing_Standard ui)
        {
            if (PawnA == null || PawnB == null)
                return;

            if(outcome.PawnA == null || ui.ButtonText("Re-roll duel outcome"))            
                outcome = DuelUtility.MakeOutcome(PawnA, PawnB, true);            

            var area = ui.GetRect(200);
            Widgets.LabelScrollable(area, outcome.GenDebug, ref GetScroll());

            ui.GapLine();
        }

        private void DrawAllAnimators(Listing_Standard ui)
        {
            var area = ui.GetRect(300);
            var oldUI = ui;
            ui = new Listing_Standard();
            int toDraw = AnimRenderer.ActiveRenderers.Count;
            int toDraw2 = AnimRenderer.CapturedPawnCount;
            Widgets.BeginScrollView(area, ref GetScroll(), new Rect(0, 0, area.width - 20, 110 * toDraw + 40 * toDraw2));
            ui.Begin(new Rect(0, 0, area.width - 20, area.height));

            AnimRenderer toDestroy = null;
            foreach(var renderer in AnimRenderer.ActiveRenderers)
            {
                var rect = ui.GetRect(110);

                Widgets.DrawBox(rect);
                string title = $"[{renderer.CurrentTime:F2} / {renderer.Data.Duration:F2}] {renderer.Data.Name}";
                if (renderer.Destroyed)
                    title += " <color=red>[DESTROYED]</color>";
                Widgets.Label(rect.ExpandedBy(-4, -4), title);
                var bar = rect.ExpandedBy(-4);
                bar.yMin += 22;
                bar.height = 10;
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
    }
}
