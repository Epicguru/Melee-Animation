using System;
using System.Linq;
using AAM.Patches;
using RimWorld;
using UnityEngine;
using Verse;

namespace AAM
{
    public class GameComp : GameComponent
    {
        [TweakValue("__AAM", 0, 20)]
        private static float Y = 0;

        private Vector3 position;

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

            if (Current.ProgramState != ProgramState.Playing)
                return;

            if (Input.GetKey(KeyCode.L))
                position = Verse.UI.MouseMapPosition();

            position.y = Y;

            var mat = ThingDefOf.AIPersonaCore.graphic.MatSingle;
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(position, Quaternion.identity, Vector3.one), mat, 0);
        }

        public override void GameComponentOnGUI()
        {
            GUILayout.Space(30);
            GUILayout.Label($"Y: {Y}");
            GUILayout.Label($"Clothes: {AltitudeLayer.Pawn.AltitudeFor() + 0.023166021f + 0.0028957527f}");
            GUILayout.Space(30);
            for(int i = 0; i < 36; i++)
            {
                var alt = (AltitudeLayer)(byte)i;
                if (GUILayout.Button($"#{i} {alt} --> {alt.AltitudeFor()}"))
                    Y = alt.AltitudeFor();
            }

            base.GameComponentOnGUI();
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            Core.Warn("Loaded game");
        }
    }
}
