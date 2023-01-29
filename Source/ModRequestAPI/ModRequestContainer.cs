using GistAPI.Models;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace ModRequestAPI;

public record ModRequestContainer : IGistFileContents
{
    [JsonIgnore]
    public GistFile GistFile { get; set; }

    [JsonProperty("mods")]
    public Dictionary<string, ModData> Mods { get; set; } = new Dictionary<string, ModData>();
}

public record ModData
{
    [JsonProperty("rc")]
    public int RequestCount { get; set; }

    [JsonProperty("mwc")]
    public int MissingWeaponCount { get; set; }
}