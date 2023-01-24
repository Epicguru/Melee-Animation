using Newtonsoft.Json;
using System.Collections.Generic;

namespace GistAPI.Models;

public record UpdateGistRequest
{
    [JsonProperty("description")]
    public string Description { get; set; }

    [JsonProperty("files")]
    public Dictionary<string, GistFile> Files { get; set; }
}