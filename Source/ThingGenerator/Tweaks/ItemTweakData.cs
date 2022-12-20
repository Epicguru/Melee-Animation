using AAM.Sweep;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Verse;

namespace AAM.Tweaks
{
    public class ItemTweakData : IExposable
    {
        public static string MakeModID(ModContentPack mcp)
        {
            if (mcp == null)
                return null;

            string s = mcp.PackageId;

            foreach (char c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');

            return s;
        }

        public string ItemDefName;
        public string ItemType;
        public string ItemTypeNamespace;
        public string TexturePath;
        public float OffX, OffY;
        public float Rotation;
        public float ScaleX = 1, ScaleY = 1;
        public bool FlipX, FlipY;
        public bool UseDefaultTransparentMaterial;
        public HandsMode HandsMode = HandsMode.Default;
        public float BladeStart, BladeEnd = 0.5f;
        public MeleeWeaponType MeleeWeaponType = MeleeWeaponType.Long_Stab | MeleeWeaponType.Long_Sharp;
        public string CustomRendererClass;
        public string SweepProviderClass;

        private ThingDef cachedDef;
        private Texture2D cachedTex;
        private ISweepProvider cachedSweepProvider;
        private Dictionary<float, Vector2> widthAtPosition;

        public ItemTweakData() { }

        public ItemTweakData(ThingDef def)
        {
            if (def == null)
                return;

            cachedDef = def;

            ItemDefName = def.defName;
            ItemType = def.GetType().Name;
            ItemTypeNamespace = def.GetType().Namespace;

            TexturePath = def.graphicData.texPath;
            if (GetTexture(false, false) == null)
            {
                var replacement = ContentFinder<Texture2D>.GetAllInFolder(TexturePath).FirstOrFallback();
                if (replacement != null)
                {
                    TexturePath = TexturePath + '/' + replacement.name;
                }
            }

            Rotation = 0f;
            ScaleX = def.graphic.drawSize.x;
            ScaleY = def.graphic.drawSize.y;
            HandsMode = HandsMode.Default;
        }

        public Vector2? TryGetWidthAtPosition(float distance)
        {
            var tex = GetTexture();
            if (tex == null)
                return null;

            if (!tex.isReadable)
            {
                Core.Warn($"Texture '{tex}' is not readable so some effects will not work.");
                return null;
            }

            return null;
        }

        public void CopyTransformFrom(ItemTweakData data)
        {
            if (data == null || data == this)
                return;

            OffX = data.OffX;
            OffY = data.OffY;
            ScaleX = data.ScaleX;
            ScaleY = data.ScaleY;
            Rotation = data.Rotation;
            HandsMode = data.HandsMode;
            MeleeWeaponType = data.MeleeWeaponType;
            FlipX = data.FlipX;
            FlipY = data.FlipY;
            BladeStart = data.BladeStart;
            BladeEnd = data.BladeEnd;
            CustomRendererClass = data.CustomRendererClass;
            SweepProviderClass = data.SweepProviderClass;
        }

        public Texture2D GetTexture(bool allowFromCache = true, bool saveToCache = true)
        {
            if (!allowFromCache || cachedTex == null)
            {
                var found = ContentFinder<Texture2D>.Get(TexturePath, false);
                if (saveToCache)
                    cachedTex = found;
                else
                    return found;
            }
            return cachedTex;
        }

        public ThingDef GetDef(bool allowFromCache = true, bool saveToCache = true)
        {
            if (!allowFromCache || cachedDef == null)
            {
                var type = GenTypes.GetTypeInAnyAssembly(ItemType, ItemTypeNamespace);
                if (type == null)
                    return null;

                var obj = GenGeneric.InvokeStaticMethodOnGenericType(typeof(DefDatabase<>), type, "GetNamedSilentFail", ItemDefName) as ThingDef;
                if (obj == null)
                    Core.Warn($"Failed to find item def '{ItemDefName}' of type {type}. The item was probably removed from the target mod.");

                if (saveToCache)
                    cachedDef = obj;
                else
                    return obj;
            }
            return cachedDef;
        }

        public ISweepProvider GetSweepProvider()
        {
            if (string.IsNullOrEmpty(SweepProviderClass))
                return null;

            if (cachedSweepProvider != null)
                return cachedSweepProvider;

            Type klass = GenTypes.GetTypeInAnyAssembly(SweepProviderClass);
            if (klass == null)
            {
                Core.Warn($"Failed to find any class called '{SweepProviderClass}' as a sweep provider.");
                return null;
            }

            if (!typeof(ISweepProvider).IsAssignableFrom(klass))
            {
                Core.Error($"{klass.FullName} does not implement {nameof(ISweepProvider)}");
                return null;
            }

            ISweepProvider instance;
            try
            {
                instance = Activator.CreateInstance(klass, this) as ISweepProvider;
            }
            catch
            {
                try
                {
                    instance = Activator.CreateInstance(klass) as ISweepProvider;
                }
                catch (Exception e)
                {
                    Core.Error($"Failed to create instance of ISweepProvider '{klass.FullName}':", e);
                    return null;
;               }
            }

            cachedSweepProvider = instance;
            return cachedSweepProvider;
        }

        public virtual AnimPartOverrideData Apply(AnimRenderer renderer, AnimPartData part)
        {
            if (part == null)
                return null;

            var ov = renderer.GetOverride(part);
            ov.Texture = GetTexture();
            ov.LocalScaleFactor = new Vector2(ScaleX, ScaleY);
            ov.LocalRotation = Rotation;
            ov.LocalOffset = new Vector2(OffX, OffY);
            ov.FlipX = FlipX;
            ov.FlipY = FlipY;
            ov.UseDefaultTransparentMaterial = UseDefaultTransparentMaterial;
            ov.TweakData = this;

            if (!string.IsNullOrWhiteSpace(CustomRendererClass))
            {
                // TODO cache.
                // TODO handle errors.
                Type rendererClass = GenTypes.GetTypeInAnyAssembly(CustomRendererClass);
                var instance = Activator.CreateInstance(rendererClass) as PartRenderer;
                instance.TweakData = this;
                ov.CustomRenderer = instance;
            }

            return ov;
        }

        public void ExposeData()
        {
            if (string.IsNullOrWhiteSpace(CustomRendererClass))
                CustomRendererClass = null;

            Scribe_Values.Look(ref ItemDefName, "dId");
            Scribe_Values.Look(ref ItemType, "typ");
            Scribe_Values.Look(ref ItemTypeNamespace, "nsp");
            Scribe_Values.Look(ref TexturePath, "tex");
            Scribe_Values.Look(ref OffX, "ofX");
            Scribe_Values.Look(ref OffY, "ofY");
            Scribe_Values.Look(ref Rotation, "rot");
            Scribe_Values.Look(ref ScaleX, "scX", 1);
            Scribe_Values.Look(ref ScaleY, "scY", 1);
            Scribe_Values.Look(ref FlipX, "flX");
            Scribe_Values.Look(ref FlipY, "flY");
            Scribe_Values.Look(ref HandsMode, "hnd");
            Scribe_Values.Look(ref UseDefaultTransparentMaterial, "trs");
            Scribe_Values.Look(ref BladeStart, "blS", 0);
            Scribe_Values.Look(ref BladeEnd, "blE", 0.5f);
            Scribe_Values.Look(ref CustomRendererClass, "crc", null);
            Scribe_Values.Look(ref SweepProviderClass, "spc", null);

            int flag = (int)MeleeWeaponType;
            Scribe_Values.Look(ref flag, "tag", (int)(MeleeWeaponType.Long_Stab | MeleeWeaponType.Long_Sharp));
            MeleeWeaponType = (MeleeWeaponType)flag;
        }
    }
}
