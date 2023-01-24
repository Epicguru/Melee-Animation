using Newtonsoft.Json;
using System.Collections.Generic;

namespace GistAPI.Models;

public record GetGistResponse
{
    [JsonProperty("id")]
    public string ID { get; set; }

    [JsonProperty("files")]
    public Dictionary<string, GistFile> Files { get; set; } = new Dictionary<string, GistFile>();
}
