
using Verse;

namespace AAM.LightsaberPatch
{
    [HotSwapAll]
    public class PatchCore : Mod
    {
        public static void Log(string msg)
        {
            Core.Log($"<color=#ffa8fc>[Lightsaber Patch]</color> {msg}");
        }

        public PatchCore(ModContentPack content) : base(content)
        {
            Log("Loaded lightsaber patch!");
        }
    }
}
