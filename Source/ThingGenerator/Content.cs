using System;
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

        [BundleContent("Materials/TrailShader.mat")]
        public static Material TrailMaterial;
        [BundleContent("Materials/BlitMaterial.mat")]
        public static Material BlitMaterial;

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

                    var value = Core.ModContent.assetBundles.loadedAssetBundles[0].LoadAsset(path, field.FieldType);

                    if (value == null)
                        Log.Error($"Failed to load bundle content: [{field.FieldType.Name}] {path}");

                    field.SetValue(null, value);
                }
            }
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
