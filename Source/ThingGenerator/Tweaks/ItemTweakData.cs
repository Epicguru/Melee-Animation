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

        private ThingDef cachedDef;
        private Texture2D cachedTex;

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

                var obj = GenGeneric.InvokeStaticMethodOnGenericType(typeof(DefDatabase<>), type, "GetNamedSilentFail", ItemDefName);

                if (saveToCache)
                    cachedDef = obj as ThingDef;
                else
                    return obj as ThingDef;
            }
            return cachedDef;
        }

        public virtual void Apply(AnimRenderer renderer, AnimPartData part)
        {
            if (part == null)
                return;

            var ov = renderer.GetOverride(part);
            ov.Texture = GetTexture();
            ov.LocalScaleFactor = new Vector2(ScaleX, ScaleY);
            ov.LocalRotation = Rotation;
            ov.LocalOffset = new Vector2(OffX, OffY);
            ov.FlipX = FlipX;
            ov.FlipY = FlipY;
            ov.UseDefaultTransparentMaterial = UseDefaultTransparentMaterial;
        }

        public void ExposeData()
        {
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

            int flag = (int)MeleeWeaponType;
            Scribe_Values.Look(ref flag, "tag", (int)(MeleeWeaponType.Long_Stab | MeleeWeaponType.Long_Sharp));
            MeleeWeaponType = (MeleeWeaponType)flag;
        }
    }
}
