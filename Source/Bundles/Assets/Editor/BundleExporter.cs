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

    private static void PostProcess(BuildTarget target, string destinationFolder, AssetBundleManifest manifest)
    {
        var bundle = manifest.GetAllAssetBundles().First();
        
        string path = Path.Combine(destinationFolder, bundle);
        string path2 = Path.Combine(destinationFolder, $"{bundle}.manifest");

        string dir = Path.Combine(destinationFolder, target.ToString());
        CreateDeep(new DirectoryInfo(dir));

        if (File.Exists(Path.Combine(dir, bundle)))
            File.Delete(Path.Combine(dir, bundle));
        File.Move(path, Path.Combine(dir, bundle));

        if (File.Exists(Path.Combine(dir,  $"{bundle}.manifest")))
            File.Delete(Path.Combine(dir,  $"{bundle}.manifest"));
        File.Move(path2, Path.Combine(dir, $"{bundle}.manifest"));
    }
}
