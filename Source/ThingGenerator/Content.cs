using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Verse;

namespace AAM
{
    [StaticConstructorOnStartup]
    public static class Content
    {
        public const string WEB_BUNDLE_URL = "https://media.githubusercontent.com/media/Epicguru/AdvancedAnimationMod/develop/BundlesWebOnly/";

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
        [Content("AAM/UI/ExtraGUIWalk")]
        public static Texture2D ExtraGuiWalk;
        [Content("AAM/UI/ExtraGUIForce")]
        public static Texture2D ExtraGuiForce;
        [Content("AAM/UI/ExtraGUIWhy")]
        public static Texture2D ExtraGuiWhy;
        [Content("AAM/UI/BG/Sketch1")]
        public static Texture2D BGSketch1;
        [Content("AAM/UI/Loading")]
        public static Texture2D Loading;

        [BundleContent("Materials/TrailShader.mat")]
        public static Material TrailMaterial;
        [BundleContent("Materials/CutoffCustom.mat")]   
        public static Material CustomCutoffMaterial;

        /// <summary>
        /// A hashset containing all lasso defs, used to check if a pawn has a lasso equipped.
        /// This is automatically populated with all apparel that has the 'Lasso' tag.
        /// </summary>
        public static readonly HashSet<ThingDef> LassoDefs = new HashSet<ThingDef>();

        private static AssetBundle bundle;

        public static void Load()
        {
            try
            {
                LoadBundle();
            }
            catch (Exception e)
            {
                Core.Error("Failed to load asset bundle!", e);
            }

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
                        value = ContentFinder<Texture2D>.Get(path, false);
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

                    var value = bundle.LoadAsset(path, field.FieldType);

                    if (value == null)
                        Log.Error($"Failed to load bundle content: [{field.FieldType.Name}] {path} from any loaded bundle.");

                    field.SetValue(null, value);
                }
            }
        }

        private static void LoadBundle()
        {
            string bundlePlatformName = GetPlatformName();
            if (bundlePlatformName == null)
            {
                Core.Warn($"Platform: {Application.platform}, 64bit: {Environment.Is64BitOperatingSystem} does not have a corresponding asset bundle. Attempting to use StandaloneLinux64...");
                bundlePlatformName = "StandaloneLinux64";
            }

            string bundlePath = Path.Combine(Core.ModContent.RootDir, "Bundles", bundlePlatformName, "animationmod");
            if (!File.Exists(bundlePath))
                throw new FileNotFoundException(bundlePath);

            bundle = AssetBundle.LoadFromFile(new FileInfo(bundlePath).FullName);
            if (bundle == null)
                throw new Exception($"Asset bundle '{bundlePlatformName}' failed to load!");
        }

        public static string GetPlatformName() => Application.platform switch
        {
            RuntimePlatform.WindowsPlayer => "StandaloneWindows",
            RuntimePlatform.OSXPlayer => "StandaloneOSX",
            RuntimePlatform.LinuxPlayer => "StandaloneLinux64",
            _ => null
        };

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
