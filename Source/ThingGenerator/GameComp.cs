using AAM.Data;
using AAM.Grappling;
using AAM.Patches;
using AAM.UI;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Verse;
using Object = UnityEngine.Object;

namespace AAM
{
    public class GameComp : GameComponent
    {
        public static GameComp Current;

        public readonly Game Game;
        [TweakValue("__AAM")]
        private static bool drawTextureExtractor;

        private string texPath;
        private Dictionary<Pawn, PawnMeleeData> pawnMeleeData = new();
        private List<PawnMeleeData> allMeleeData = new();

        public GameComp(Game game)
        {
            this.Game = game;
            Current = this;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                allMeleeData.RemoveAll(d => !d.ShouldSave());
            }

            Scribe_Collections.Look(ref allMeleeData, "pawnMeleeData", LookMode.Deep);
            allMeleeData ??= new List<PawnMeleeData>();

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pawnMeleeData.Clear();
                Core.Log($"Loading {allMeleeData.Count} pawn melee data.");
                foreach (var data in allMeleeData)
                {
                    if (data.ShouldSave())
                        pawnMeleeData.Add(data.Pawn, data);
                }
            }
        }

        public PawnMeleeData GetOrCreateData(Pawn pawn)
        {
            if (pawn == null || pawn.Destroyed)
                return null;

            if (pawnMeleeData.TryGetValue(pawn, out var found))
                return found;

            var created = new PawnMeleeData();
            created.Pawn = pawn;
            allMeleeData.Add(created);
            pawnMeleeData.Add(pawn, created);
            return created;
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            AnimRenderer.TickAll();
            GrabUtility.Tick();

            Patch_Corpse_DrawAt.Tick();
            Patch_PawnRenderer_LayingFacing.Tick();

            foreach (var data in allMeleeData)
            {
                data.TimeSinceExecuted += 1 / 60f;
                data.TimeSinceGrappled += 1 / 60f;
            }
        }

        public override void GameComponentOnGUI()
        {
            if (Prefs.DevMode && Dialog_AnimationDebugger.IsInRehearsalMode)
            {
                GUILayout.Space(100);
                GUILayout.Label("<b><color=green>IN REHEARSAL MODE!</color></b>");
            }

            if (!drawTextureExtractor)
                return;

            GUILayout.Space(100);


            texPath ??= "";
            texPath = GUILayout.TextField(texPath);
            var tex = ContentFinder<Texture2D>.Get(texPath, false);

            if (tex != null)
            {
                GUILayout.Box(tex);

                if (GUILayout.Button("Save"))
                {
                    RenderTexture renderTex = RenderTexture.GetTemporary(
                        tex.width,
                        tex.height,
                        0,
                        RenderTextureFormat.Default,
                        RenderTextureReadWrite.Linear);

                    Graphics.Blit(tex, renderTex);
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = renderTex;
                    Texture2D readableText = new(tex.width, tex.height);
                    readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
                    readableText.Apply();
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(renderTex);

                    var pngBytes = readableText.EncodeToPNG();
                    ;
                    Log.Message($"Writing {pngBytes.Length} bytes of {texPath} to Desktop ...");
                    File.WriteAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), $"{tex.name ?? "grab"}.png"), pngBytes);

                    Object.Destroy(readableText);
                    Object.Destroy(renderTex);
                }
            }
        }
    }
}
