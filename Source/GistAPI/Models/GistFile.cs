using Newtonsoft.Json;

namespace GistAPI.Models;

public class GistFile
{
    [JsonProperty("filename")]
    public string FileName { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("language")]
    public string Language { get; set; }

    [JsonProperty("size")]
    public int Size { get; set; }

    [JsonProperty("content")]
    public string Content { get; set; }

    [JsonProperty("raw_url")]
    public string RawUrl { get; set; }
}
