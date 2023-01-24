using GistAPI;
using GistAPI.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Verse;

namespace ModRequestAPI;

public class ModRequestClient
{
    #region Static
    private static readonly HashSet<char> invalidFileChars;

    static ModRequestClient()
    {
        invalidFileChars = new HashSet<char>();
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            invalidFileChars.Add(c);
        }
    }

    private static char MakePersistentHashChar(string str, int range)
    {
        ulong sum = 0;
        foreach (char c in str)
        {
            sum += c;
        }

        return (char)('A' + (sum % (ulong)range));
    }

    public static string GetFileName(string modPackageID)
    {
        return $"{MakePersistentHashChar(modPackageID, 26)}.json";
    }
    #endregion

    public readonly string GistID;

    private readonly GistClient<ModRequestContainer> client;

    public ModRequestClient(string gistID)
    {
        GistID = gistID ?? throw new ArgumentNullException(nameof(gistID));
        client = new GistClient<ModRequestContainer>();
    }

    public async Task<List<(string modID, ModData data)>> GetModRequests()
    {
        var list = await client.ReadGist(GistID);
        var results = new List<(string modID, ModData data)>(list.Count);

        foreach (var container in list)
        {
            foreach (var pair in container.Mods)
            {
                results.Add((pair.Key, pair.Value));
            }
        }

        return results;
    }

    public async Task<List<(string modID, ModData data)>> UpdateModRequests(IEnumerable<string> toUpdate, string updateMessage, Func<string, ModData, bool> updateAction)
    {
        if (!toUpdate.Any())
            return new List<(string, ModData)>();

        updateMessage ??= "No description provided";

        var fileNames = new HashSet<string>();
        foreach (var modID in toUpdate)
            fileNames.Add(GetFileName(modID));
        
        bool Filter(GistFile file) => fileNames.Contains(file.FileName);

        // Get the files for the required Ids.
        var containers = new Dictionary<string, ModRequestContainer>();
        var raw = await client.ReadGist(GistID, Filter);
        foreach (var container in raw)
            containers.Add(container.GistFile.FileName, container);

        // Update the containers or add new containers.
        var results = new List<(string, ModData)>();
        foreach (var modID in toUpdate)
        {
            string fileName = GetFileName(modID);
            if (!containers.TryGetValue(fileName, out var container))
            {
                container = new ModRequestContainer
                {
                    GistFile = new GistFile
                    {
                        FileName = fileName
                    },
                    Mods = new Dictionary<string, ModData>()
                };
                containers.Add(fileName, container);
            }

            if (!container.Mods.TryGetValue(modID, out var modData))
            {
                modData = new ModData();
                container.Mods.Add(modID, modData);
            }

            bool keep = updateAction(modID, modData);
            if (!keep)
            {
                container.Mods.Remove(modID);
            }
            else
            {
                results.Add((modID, modData));
            }
        }

        // Make gist files from containers.
        var gistFiles = new Dictionary<string, GistFile>();

        foreach (var pair in containers)
        {
            string content = JsonConvert.SerializeObject(pair.Value);

            gistFiles.Add(pair.Key, new GistFile
            {
                FileName = pair.Key,
                Content = content
            });
        }

        // Send update request.
        await client.UpdateGist(GistID, new UpdateGistRequest
        {
            Description = updateMessage,
            Files = gistFiles
        });

        return results;
    }
}