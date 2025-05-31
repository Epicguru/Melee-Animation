using AM.Tweaks;
using UnityEngine;
using Verse;

namespace AM
{
    public abstract class PartRenderer
    {
        public Mesh Mesh;
        public ItemTweakData TweakData;
        public AnimPartData Part;
        public AnimRenderer Renderer;
        public Matrix4x4 TRS;
        public AnimPartOverrideData OverrideData;
        public AnimPartSnapshot Snapshot;
        public Material Material; // The material used for rendering by the default renderer, which may changed based on the SplitDrawMode.
        public Material MaterialWithoutSplitMode; // The material used for rendering by the default renderer, which is always the same regardless of SplitDrawMode.
        public ThingWithComps Item;

        /// <summary>
        /// Return true to skip regular draw.
        /// </summary>
        public abstract bool Draw();

        public virtual Color? GetOverrideTrailTint() => null;
    }
}
