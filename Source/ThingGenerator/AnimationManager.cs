using AAM.Tweaks;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AAM
{
    public class AnimationManager : MapComponent
    {
        public static Texture2D HandTexture;

        private static int lastTickedFrame = -1;

        public static void Init()
        {
            HandTexture = ContentFinder<Texture2D>.Get("AAM/Hand");
        }

        private List<(Pawn pawn, Vector2 position)> labels = new List<(Pawn pawn, Vector2 position)>();

        public AnimationManager(Map map) : base(map)
        {

        }

        public AnimRenderer StartAnimation(AnimDef def, Matrix4x4 rootTransform, params Pawn[] pawns)
        {
            return StartAnimation(def, rootTransform, false, false, pawns);
        }

        public AnimRenderer StartAnimation(AnimDef def, Matrix4x4 rootTransform, bool mirrorX, params Pawn[] pawns)
        {
            return StartAnimation(def, rootTransform, mirrorX, false, pawns);
        }

        public virtual AnimRenderer StartAnimation(AnimDef def, Matrix4x4 rootTransform, bool mirrorX, bool mirrorY, params Pawn[] pawns)
        {
            if (def?.Data == null)
                return null;

            var renderer = new AnimRenderer(def, map);
            renderer.RootTransform = rootTransform;
            renderer.MirrorHorizontal = mirrorX;
            renderer.MirrorVertical = mirrorY;

            int i = 0;
            foreach(var pawn in pawns)            
                i += AddPawn(renderer, i, pawn) ? 1 : 0;            

            renderer.Register();

            return renderer;
        }

        public void StopAnimation(Pawn pawn)
        {
            if (pawn == null)
                return;

            var renderer = AnimRenderer.TryGetAnimator(pawn);
            StopAnimation(renderer);
        }

        public void StopAnimation(AnimRenderer renderer)
        {
            if (renderer != null && !renderer.Destroyed)
                renderer.Destroy();
        }

        protected virtual bool AddPawn(AnimRenderer renderer, int index, Pawn pawn)
        {
            if (pawn == null)
                return false;

            renderer.Pawns[index] = pawn;
            char tagChar = AnimRenderer.Alphabet[index];

            // Held item.
            string itemName = $"Item{tagChar}";
            var weapon = pawn.GetEquippedMeleeWeapon();
            var tweak = weapon == null ? null : TweakDataManager.GetOrCreateDefaultTweak(weapon.def);
            var handsMode = tweak?.HandsMode ?? HandsMode.Default;

            // Hands and skin color...
            string mainHandName = $"HandA{(index > 0 ? (index + 1) : "")}";
            string altHandName  = $"HandB{(index > 0 ? (index + 1) : "")}";

            Color skinColor = pawn.story?.SkinColor ?? Color.white;
            bool showMain = weapon != null && handsMode != HandsMode.No_Hands;
            bool showAlt  = weapon != null && handsMode == HandsMode.Default;

            // Apply weapon.
            var itemPart = renderer.GetPart(itemName);
            if(weapon != null && itemPart != null)
            {
                tweak.Apply(renderer, itemPart);
                var ov = renderer.GetOverride(itemPart);
                ov.Material = weapon.Graphic.MatSingleFor(weapon);
                ov.UseMPB = false; // Do not use the material property block, because it will override the material second color and mask.
            }

            // Apply main hand.
            var mainHandPart = renderer.GetPart(mainHandName);
            if(mainHandPart != null)
            {
                var ov = renderer.GetOverride(mainHandPart);
                ov.PreventDraw = !showMain;
                ov.Texture = HandTexture;
                ov.ColorOverride = skinColor;
            }

            // Apply alt hand.
            var altHandPart = renderer.GetPart(altHandName);
            if (mainHandPart != null)
            {
                var ov = renderer.GetOverride(altHandPart);
                ov.PreventDraw = !showAlt;
                ov.Texture = HandTexture;
                ov.ColorOverride = skinColor;
            }

            return true;
        }

        public override void MapComponentUpdate()
        {
            base.MapComponentUpdate();

            float dt = Time.deltaTime * Find.TickManager.TickRateMultiplier * (Input.GetKey(KeyCode.LeftShift) ? 0.1f : 1f);
            Draw(dt);
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            if (GenTicks.TicksAbs == lastTickedFrame)
                return;
            lastTickedFrame = GenTicks.TicksAbs;

            AnimRenderer.UpdateAll();
        }

        public void Draw(float deltaTime)
        {
            labels.Clear();
            AnimRenderer.DrawAll(deltaTime, map, DrawLabel);
        }

        private void DrawLabel(Pawn pawn, Vector2 position)
        {
            labels.Add((pawn, position));
        }

        public override void MapComponentOnGUI()
        {
            base.MapComponentOnGUI();

            foreach (var pair in labels)            
                GenMapUI.DrawPawnLabel(pair.pawn, pair.position, 0.5f, 9999f, null, GameFont.Tiny, true, true);            
        }
    }
}
