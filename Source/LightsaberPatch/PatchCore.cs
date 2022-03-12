
using Verse;

namespace AAM.LightsaberPatch
{
    public class PatchCore : Mod
    {
        public static void Log(string msg)
        {
            Core.Log($"[Lightsaber Patch] {msg}");
        }

        public PatchCore(ModContentPack content) : base(content)
        {
            Log("Loaded lightsaber patch!");
        }
    }
}
