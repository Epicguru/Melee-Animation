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
        public Material Material;
        public ThingWithComps Item;

        /// <summary>
        /// Return true to skip regular draw.
        /// </summary>
        public abstract bool Draw();

        public virtual Color? GetOverrideTrailTint() => null;
    }
}
