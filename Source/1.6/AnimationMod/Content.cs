﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Verse;

namespace AM
{
    [StaticConstructorOnStartup]
    public static class Content
    {
        [Content("AM/Rope/Rope")]
        public static Texture2D Rope;
        [Content("AM/Rope/End")]
        public static Texture2D RopeEnd;
        [Content("AM/Rope/Coiled")]
        public static Texture2D RopeCoil;
        [Content("AM/BoundPawns/Male")]
        public static Texture2D BoundMaleRope;
        [Content("AM/Shadow")]
        public static Texture2D Shadow;
        [Content("AM/Hand")]
        public static Texture2D Hand;
        [Content("AM/HandClothed")]
        public static Texture2D HandClothed;

        // UI
        [Content("AM/UI/IconExecute")]
        public static Texture2D IconExecute;
        [Content("AM/UI/IconGrapple")]
        public static Texture2D IconGrapple;
        [Content("AM/UI/ExtraGUIWalk")]
        public static Texture2D ExtraGuiWalk;
        [Content("AM/UI/ExtraGUIForce")]
        public static Texture2D ExtraGuiForce;
        [Content("AM/UI/ExtraGUIWhy")]
        public static Texture2D ExtraGuiWhy;
        [Content("AM/UI/BG/Combined")]
        public static Texture2D BGCombined;
        [Content("AM/UI/DuelIcon")]
        public static Texture2D DuelIcon;
        [Content("AM/UI/HIDE")]
        public static Texture2D ToggleVisibilityIcon;

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
