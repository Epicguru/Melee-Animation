using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class BundleExporter
{
    public static string Destination => "../../Bundles";
    public static BuildAssetBundleOptions Options => BuildAssetBundleOptions.None;
    public static IEnumerable<BuildTarget> Targets => new[]
    {
        BuildTarget.StandaloneWindows,
        BuildTarget.StandaloneLinux64,
        BuildTarget.StandaloneOSX,
    };

    [MenuItem("Bundles/Build")]
    public static void ExportBundles()
    {
        try
        {
            string dest = new DirectoryInfo(Destination).FullName;
            Debug.Log($"Building bundles to {dest}");

            int i = 0;
            foreach (var target in Targets)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Exporting bundles", $"Platform: {target}", (i + 1f) / Targets.Count()))
                {
                    break;
                }

                Debug.Log($"Building bundles for {target} ...");
                var bundle = BuildPipeline.BuildAssetBundles(dest, Options, target);
                PostProcess(target, dest, bundle);
            }

            string toDelete = Path.Combine(dest, "Bundles");
            string toDelete2 = Path.Combine(dest, "Bundles.manifest");

            File.Delete(toDelete);
            File.Delete(toDelete2);

            // Build web bundles.
            foreach (var target in Targets)
            {
                foreach (var vid in Directory.EnumerateFiles(Path.Combine(Application.dataPath, "Web"), "*", SearchOption.AllDirectories).Where(p => p.EndsWith(".png") || p.EndsWith(".mp4")))
                {
                    string resourcePath = vid.Substring(vid.IndexOf("Assets", StringComparison.Ordinal));
                    Debug.Log($"Asset: {resourcePath}");
                    var fileName = new FileInfo(vid).Name;
                    fileName = fileName.Substring(0, fileName.Length - 4);
                    string path = Path.Combine(dest, target.ToString());
                    CreateDeep(new DirectoryInfo(path));
                    AssetBundleBuild[] assets = 
                    {
                        new AssetBundleBuild
                        {
                            assetBundleName = fileName,
                            assetNames = new[]
                            {
                                resourcePath
                            }
                        }
                    };

                    BuildPipeline.BuildAssetBundles(path, assets, BuildAssetBundleOptions.None, target);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void CreateDeep(DirectoryInfo dir)
    {
        if (dir == null || dir.Exists)
            return;

        CreateDeep(dir.Parent);
        dir.Create();
    }

    private static void PostProcess(BuildTarget target, string dest,  AssetBundleManifest manifest)
    {
        foreach (var bundle in manifest.GetAllAssetBundles())
        {
            foreach (var fn in new[] {bundle, $"{bundle}.manifest"})
            {
                // The path of the actual file that was generated.
                string path = Path.Combine(dest, fn);

                // The target directory.
                string dir = Path.Combine(dest, target.ToString());
                CreateDeep(new DirectoryInfo(dir));

                // Move the file from existing dir to target.
                if (File.Exists(Path.Combine(dir, fn)))
                    File.Delete(Path.Combine(dir, fn));
                File.Move(path, Path.Combine(dir, fn));
            }
        }
    }
}
