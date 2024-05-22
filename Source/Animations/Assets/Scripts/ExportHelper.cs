using System.IO;
using System.Linq;
using UnityEditor;

public static class ExportHelper
{
    public static string OutputDirectoryPath
    {
        get => EditorPrefs.GetString("MeleeAnimation_ExportFolder", null);
        set => EditorPrefs.SetString("MeleeAnimation_ExportFolder", value);
    }

    [MenuItem("Melee Animation/Set Export Location")]
    public static void ShowChangeExportLocationDialogue()
    {
        string selectedPath = EditorUtility.OpenFolderPanel("Select output folder", "Assets", "");
        if (!Directory.Exists(selectedPath))
        {
            EditorUtility.DisplayDialog("Failed to set output folder", "The selected folder could not be found", "Ok");
            return;
        }

        if (Directory.EnumerateFiles(selectedPath, "*", SearchOption.AllDirectories).Any(file => Path.GetExtension(file) != ".json"))
        {
            EditorUtility.DisplayDialog("Failed to set output folder", "The selected folder contains files that are not animation files, you have probably selected the wrong folder.", "Ok");
            return;
        }

        OutputDirectoryPath = selectedPath;
    }
}
