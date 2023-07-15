using System.Text.Json.Serialization;

namespace ModRequestAPI.Backend.Models
{
    public class ModRequestRow
    {
        [JsonPropertyName("pk")]
        public string Pk => ModID;

        [JsonPropertyName("sk")]
        public string Sk => ModID;

        public string ModID { get; set; }

        public int WeaponCount { get; set; }
    }
}
