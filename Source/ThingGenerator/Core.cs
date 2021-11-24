using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AAM
{
    [HotSwappable]
    public class Core : Mod
    {
        public static ModContentPack ModContent;

        public static void Log(string msg)
        {
            Verse.Log.Message($"<color=green>[ThingGenerator]</color> {msg}");
        }

        public static void Warn(string msg)
        {
            Verse.Log.Warning($"<color=green>[ThingGenerator]</color> {msg}");
        }

        public static void Error(string msg, Exception e = null)
        {
            Verse.Log.Error($"<color=green>[ThingGenerator]</color> {msg}");
            if (e != null)
                Verse.Log.Error(e.ToString());
        }

        public Core(ModContentPack content) : base(content)
        {
            Log("Hello, world!");
            new Harmony("co.uk.epicguru.animations").PatchAll();
            ModContent = content;
        }
    }

    public class HotSwappableAttribute : Attribute
    {

    }

    public class TempComp : MapComponent
    {
        [TweakValue("__ANIM", 0f, 10f)]
        public static float _Time;

        [TweakValue("__ANIM", 0f, 100)]
        public static float OffX = 82, OffZ = 44;
        [TweakValue("__ANIM", -10, 20f)]
        public static float OffY;
        [TweakValue("__ANIM", 0f, 1f)]
        public static float ScaleY = 0.1f;
        [TweakValue("__ANIM", 0f, 1f)]
        public static bool MirrorHorizontal;

        private List<(Vector3 pos, Pawn pawn)> labelsToDraw = new List<(Vector3 pos, Pawn pawn)>();
        private AnimRenderer renderer;

        public TempComp(Map map) : base(map)
        {
        }

        private void Init()
        {
            string[] names = new string[]
            {
                "Execution_Passover",
                "Execution_Stab",
                "Duel_Swords"

            };
            string path = @$"C:\Users\spain\Desktop\{names.RandomElement()}.anim";
            var data = AnimData.Load(path);
            renderer?.Destroy();

            renderer = new AnimRenderer(data);
            Pawn p = Find.CurrentMap?.mapPawns?.AllPawns.Where(p => !p.Dead && p.IsColonist).FirstOrDefault();
            Pawn p2 = Find.CurrentMap?.mapPawns?.AllPawns.Where(p => !p.Dead && !p.IsColonist && !p.RaceProps.Animal).RandomElement();
            renderer.Pawns[0] = p;
            renderer.Pawns[1] = p2;
            OffX = p.Position.x + 0.5f;
            OffZ = p.Position.z + 0.5f;
            renderer.Register();

            AnimRenderer.DefaultCutout = new Material(ThingDefOf.AIPersonaCore.graphic.Shader);
            AnimRenderer.DefaultTransparent = new Material(FleckDefOf.AirPuff.GetGraphicData(0).shaderType.Shader);

            //var swordMat = new Material(Core.Content.assetBundles.loadedAssetBundles[0].LoadAsset<Material>("assets/dissolvematerial.mat"));

            var handTex = ContentFinder<Texture2D>.Get("AnimHand");
            var skinColor = p.story.SkinColor;
            foreach (var part in data.GetPartsRegex("^Hand"))
            {
                Core.Log(part.Name);
                part.OverrideData.Texture = handTex;
                part.OverrideData.ColorOverride = skinColor;
            }

            // Weapon specifics
            var weapon = data.GetPart("ItemA");
            var vt = ThingDef.Named("RF_VirtuousTreaty"); // RF_VirtuousTreaty
            weapon.OverrideData.Texture = vt.graphic.MatEast.mainTexture as Texture2D;
            weapon.OverrideData.LocalScaleFactor = new Vector2(vt.graphic.drawSize.x, vt.graphic.drawSize.y);
            weapon.OverrideData.LocalRotation = 45f;
            weapon.OverrideData.LocalOffset = new Vector2(0.82f, 0.02f);

            Core.Log("Created animator");
        }

        private void Draw()
        {
            labelsToDraw.Clear();

            // Make world matrix.
            Matrix4x4 world = Matrix4x4.TRS(new Vector3(OffX, OffY, OffZ), Quaternion.identity, new Vector3(1, ScaleY, 1));
            float dt = Time.deltaTime * Find.TickManager.TickRateMultiplier * (Input.GetKey(KeyCode.LeftShift) ? 0.1f : 1f);

            foreach (var renderer in AnimRenderer.ActiveRenderers)
            {
                if (renderer.Destroyed)
                    continue;

                renderer.MirrorHorizontal = MirrorHorizontal;
                renderer.RootTransform = world;

                // Draw and handle events.
                var timePeriod = renderer.Draw(null, dt);
                foreach (var e in renderer.GetEventsInPeriod(timePeriod))
                {
                    try
                    {
                        EventHelper.Handle(e, renderer);
                    }
                    catch (Exception ex)
                    {
                        Core.Error($"{ex.GetType().Name} when handling animation event '{e}':", ex);
                    }
                }

                int madeJobCount = 0;

                foreach (var pawn in renderer.Pawns)
                {
                    if (pawn == null)
                        continue;

                    var pos = renderer.GetPawnBody(pawn).CurrentSnapshot.GetWorldPosition(world);

                    // Render pawn in custom position using patches.
                    PreventDrawPatch.AllowNext = true;
                    MakePawnConsideredInvisible.IsRendering = true;
                    pawn.Drawer.renderer.RenderPawnAt(pos, Rot4.West, true);
                    MakePawnConsideredInvisible.IsRendering = false;

                    // Render shadow.
                    AccessTools.Method(typeof(PawnRenderer), "DrawInvisibleShadow").Invoke(pawn.Drawer.renderer, new object[] { pos });

                    Vector3 drawPos = pos;
                    drawPos.z -= 0.6f;
                    Vector2 vector = Find.Camera.WorldToScreenPoint(drawPos) / Prefs.UIScale;
                    vector.y = UI.screenHeight - vector.y;
                    labelsToDraw.Add((vector, pawn));

                    if (pawn.jobs != null && pawn.jobs.curJob != null && pawn.jobs.curJob.def != AAM_DefOf.AAM_InAnimation && renderer.Data.CurrentTime < renderer.Duration * 0.95f)
                    {
                        var newJob = JobMaker.MakeJob(AAM_DefOf.AAM_InAnimation);
                        newJob.collideWithPawns = true;
                        newJob.playerForced = true;
                        if (pawn.verbTracker?.AllVerbs != null)
                            foreach (var verb in pawn.verbTracker.AllVerbs)
                                verb.Reset();


                        if (pawn.equipment?.AllEquipmentVerbs != null)
                            foreach (var verb in pawn.equipment.AllEquipmentVerbs)
                                verb.Reset();

                        pawn.jobs.StartJob(newJob, JobCondition.InterruptForced, null, false, true, null);
                        madeJobCount++;
                    }
                }

                if (madeJobCount > 0)
                {
                    renderer.OnStart();
                }
            }
        }

        public override void MapComponentOnGUI()
        {
            base.MapComponentOnGUI();

            foreach (var pair in labelsToDraw)
            {
                GenMapUI.DrawPawnLabel(pair.pawn, pair.pos, 0.5f, 9999f, null, GameFont.Tiny, true, true);
            }
        }

        public override void MapComponentUpdate()
        {
            base.MapComponentUpdate();

            if (renderer == null || Input.GetKeyDown(KeyCode.P))
                Init();

            Draw();
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            if (Input.GetKeyDown(KeyCode.E))
                _Time = 0;

            AnimRenderer.Update();
        }
    }
}
