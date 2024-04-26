using Newtonsoft.Json;

namespace ModRequestAPI.Models
{
    public class MissingModRequest
    {
        /// <summary>
        /// The ID of the mod that is missing weapons.
        /// </summary>
        [JsonProperty("ModID")]
        public string ModID { get; set; }

        /// <summary>
        /// The user-facing name of the mod.
        /// </summary>
        [JsonProperty("ModName")]
        public string ModName { get; set; }

        /// <summary>
        /// The number of missing weapons.
        /// </summary>
        [JsonProperty("WeaponCount")]
        public int WeaponCount { get; set; }
    }
}
