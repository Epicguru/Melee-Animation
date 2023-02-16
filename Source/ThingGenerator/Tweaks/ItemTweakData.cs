using AAM.Retexture;
using AAM.Sweep;
using AAM.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using Verse;

namespace AAM.Tweaks
{
    public class ItemTweakData
    {
        public static string MakeModID(ModContentPack mcp)
        {
            if (mcp == null)
                return null;

            string s = mcp.PackageId;

            foreach (char c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');

            s = s.Replace("_copy", "");
            s = s.Replace("_Copy", "");
            s = s.Replace("_localcopy", "");
            s = s.Replace("_LocalCopy", "");
            s = s.Replace("_local", "");
            s = s.Replace("_Local", "");

            return s;
        }

        public static string MakeFileName(ThingDef weaponDef, ModContentPack weaponTextureMod)
            => $"{weaponDef.defName}_{MakeModID(weaponTextureMod)}.json";

        public static ItemTweakData LoadFrom(string filePath)
        {
            return JsonConvert.DeserializeObject<ItemTweakData>(File.ReadAllText(filePath), new JsonSerializerSettings
            {
                Converters = new List<JsonConverter>()
                {
                    new ColorConverter()
                }
            });
        }

        [JsonIgnore]
        public string FileName => $"{TextureModID}_{ItemDefName}.json";

        public string TextureModID;
        public string ItemDefName;
        public string ItemType;
        public string ItemTypeNamespace;
        public float OffX, OffY;
        public float Rotation;
        [System.ComponentModel.DefaultValue(1f)]
        public float ScaleX = 1;
        [System.ComponentModel.DefaultValue(1f)]
        public float ScaleY = 1;
        public bool FlipX, FlipY;
        public bool UseDefaultTransparentMaterial;
        public HandsMode HandsMode = HandsMode.Default;
        public float BladeStart;
        [System.ComponentModel.DefaultValue(0.5f)]
        public float BladeEnd = 0.5f;
        public MeleeWeaponType MeleeWeaponType = MeleeWeaponType.Long_Stab | MeleeWeaponType.Long_Sharp;
        [System.ComponentModel.DefaultValue("")]
        public string CustomRendererClass;
        public string SweepProviderClass;
        public Color? TrailTint = null;

        private ThingDef cachedDef;
        private Texture2D cachedTex;
        private ModContentPack cachedMod;
        private ISweepProvider cachedSweepProvider;

        public ItemTweakData() { }

        public ItemTweakData(ThingDef def, ModContentPack textureMod)
        {
            if (def == null)
                return;

            cachedDef = def;

            ItemDefName = def.defName;
            ItemType = def.GetType().Name;
            ItemTypeNamespace = def.GetType().Namespace;

            var report = RetextureUtility.GetTextureReport(def);
            if (report.HasError)
                throw new Exception($"Retexture utility had error: {report.ErrorMessage}");
            var mod = textureMod ?? report.ActiveRetextureMod;

            TextureModID = MakeModID(mod);

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
            TrailTint = data.TrailTint;
        }

        public Texture2D GetTexture(bool allowFromCache = true, bool saveToCache = true)
        {
            if (!allowFromCache || cachedTex == null)
            {
                var report = RetextureUtility.GetTextureReport(GetDef());
                if (report.HasError)
                    throw new Exception($"Retexture utility had error: {report.ErrorMessage}");

                var found = report.AllRetextures.FirstOrDefault(p => MakeModID(p.mod) == TextureModID).texture;
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

        public ModContentPack GetMod()
        {
            if (cachedMod != null)
                return cachedMod;

            foreach (var mcp in LoadedModManager.RunningModsListForReading)
            {
                if (MakeModID(mcp) == TextureModID)
                {
                    cachedMod = mcp;
                    break;
                }
            }

            return cachedMod;
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

        public void SaveTo(string filePath)
        {
            var settings = new JsonSerializerSettings()
            {
                Formatting = Formatting.Indented,
                DefaultValueHandling = DefaultValueHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                Error = (e, t) =>
                {
                    Core.Error($"Json saving error: {t.ErrorContext.Error.Message}");
                },
                Converters = new List<JsonConverter>()
                {
                    new ColorConverter()
                }
            };
            string json = JsonConvert.SerializeObject(this, Formatting.Indented, settings);
            File.WriteAllText(filePath, json);
        }
    }
}
