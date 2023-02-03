using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BundleExporter
{
    public static string Destination => "../../AssetBundles";
    public static BuildAssetBundleOptions Options => BuildAssetBundleOptions.None;
    public static IEnumerable<BuildTarget> Targets => new BuildTarget[]
    {
        BuildTarget.StandaloneWindows,
        BuildTarget.StandaloneLinux64,
        BuildTarget.StandaloneOSX,
    };

    [MenuItem("Bundles/Build")]
    public static void ExportBundles()
    {
        string dest = new FileInfo(Destination).FullName;
        Debug.Log($"Building bundles to {dest}");

        foreach (var target in Targets)
        {
            Debug.Log($"Building bundles for {target} ...");
            BuildPipeline.BuildAssetBundles(dest, Options, target);
        }
    }
}
