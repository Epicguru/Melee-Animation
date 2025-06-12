using System.Collections.Generic;
using System.IO;
using Verse;

namespace AM.Data;

public static class AnimDataSourceManager
{
    private static readonly Dictionary<string, string> dataNameToFilePath = new Dictionary<string, string>();

    public static void ScanForDataFiles()
    {
        dataNameToFilePath.Clear();
        
        var activeMods = LoadedModManager.RunningModsListForReading;
        int modsWithDataCount = 0;
        Core.Log($"Scanning {activeMods.Count} mods for animation data...");
        
        foreach (var mod in activeMods)
        {
            string dir = mod.RootDir;
            
            string expectedPath = Path.Combine(dir, "Animations");
            if (!Directory.Exists(expectedPath))
            {
                continue;
            }
            
            string animFolderAbsolute = new FileInfo(expectedPath).FullName;
            bool any = false;
            
            foreach (string filePath in Directory.GetFiles(expectedPath, "*.json", SearchOption.AllDirectories))
            {
                string absolute = new FileInfo(filePath).FullName;
                string relative = absolute[(animFolderAbsolute.Length + 1)..];
                string relativeWithoutExtension = Path.GetFileNameWithoutExtension(relative);

                dataNameToFilePath[relativeWithoutExtension] = absolute;
                any = true;
            }

            if (any)
            {
                modsWithDataCount++;
            }
        }
        
        Core.Log($"Found {modsWithDataCount} mod folders with animation data, total {dataNameToFilePath.Count} loadable files.");
    }

    public static string TryGetDataFilePath(string dataName) => dataNameToFilePath.GetValueOrDefault(dataName);
}
