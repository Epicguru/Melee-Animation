using AAM.Patches;
using System;
using System.IO;
using AAM.UI;
using UnityEngine;
using Verse;
using Object = UnityEngine.Object;

namespace AAM
{
    public class GameComp : GameComponent
    {
        [TweakValue("__AAM", 0, 20)]
        private static float Y = 0;
        [TweakValue("__AAM")]
        private static bool drawTextureExtractor;

        private string texPath;

        public GameComp(Game _) { }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            Patch_Corpse_DrawAt.Tick();
            Patch_PawnRenderer_LayingFacing.Tick();
        }

        public override void GameComponentUpdate()
        {
            base.GameComponentUpdate();
            SweepPathRenderer.Update();

            if (Input.GetKeyDown(KeyCode.L))
                Dialog_TweakEditor.Open(); 
                //Dialog_AnimationDebugger.Open();
        }

        public override void GameComponentOnGUI()
        {
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
                    Texture2D readableText = new Texture2D(tex.width, tex.height);
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
