using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace AAM
{
    [StaticConstructorOnStartup]
    public static class Content
    {
        [Content("AAM/Rope/Rope")]
        public static Texture2D Rope;
        [Content("AAM/Rope/End")]
        public static Texture2D RopeEnd;
        [Content("AAM/Rope/Coiled")]
        public static Texture2D RopeCoil;
        [Content("AAM/BoundPawns/Male")]
        public static Texture2D BoundMaleRope;
        [Content("AAM/Shadow")]
        public static Texture2D Shadow;

        // UI
        [Content("AAM/UI/IconBG")]
        public static Texture2D IconBG;
        [Content("AAM/UI/IconLongBG")]
        public static Texture2D IconLongBG;
        [Content("AAM/UI/IconExecute")]
        public static Texture2D IconExecute;
        [Content("AAM/UI/IconGrapple")]
        public static Texture2D IconGrapple;
        [Content("AAM/UI/IconInfo")]
        public static Texture2D IconInfo;
        [Content("AAM/UI/IconSkill")]
        public static Texture2D IconSkill;

        // TEMP: Trail shader.
        [BundleContent("Materials/TrailShader.mat")]
        public static Material TrailMaterial;
        [BundleContent("CutoffCustom.mat")]
        public static Material CustomCutoffMaterial;

        /// <summary>
        /// A hashset containing all lasso defs, used to check if a pawn has a lasso equipped.
        /// This is automatically populated with all apparel that has the 'Lasso' tag.
        /// </summary>
        public static HashSet<ThingDef> LassoDefs = new();

        public static void Load()
        {
            foreach (var field in typeof(Content).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var type = field.FieldType;

                if (field.TryGetAttribute<ContentAttribute>(out var attr))
                {
                    string path = attr.Path;
                    object value = null;
                    bool validType = true;

                    if (type == typeof(Texture2D))
                    {
                        value = ContentFinder<Texture2D>.Get(path);
                    }
                    else
                    {
                        validType = false;
                        Log.Error($"Unknown content type '{type.FullName}' for field {field.Name}, path '{path}'");
                    }

                    if (validType && value == null)
                        Log.Error($"Failed to load content: [{type.Name}] {path}");

                    field.SetValue(null, value);
                }
                else if (field.TryGetAttribute<BundleContentAttribute>(out var attr2))
                {
                    string path = attr2.Path;
                    if (!path.StartsWith("Assets/"))
                        path = "Assets/" + path;

                    var value = Core.ModContent.assetBundles.loadedAssetBundles.Select(b => b.LoadAsset(path, field.FieldType)).FirstOrDefault(a => a != null);

                    if (value == null)
                        Log.Error($"Failed to load bundle content: [{field.FieldType.Name}] {path} from any loaded bundle.");

                    field.SetValue(null, value);
                }
            }
        }

        public static void FindAllLassos()
        {
            foreach (var ap in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (ap.IsApparel && (ap.apparel.tags?.Contains("Lasso") ?? false))
                {
                    LassoDefs.Add(ap);
                }
            }

            Core.Log($"Found {LassoDefs.Count} lassos!");
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class ContentAttribute : Attribute
    {
        public readonly string Path;

        public ContentAttribute(string path)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class BundleContentAttribute : Attribute
    {
        public readonly string Path;

        public BundleContentAttribute(string path)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }
    }
}
