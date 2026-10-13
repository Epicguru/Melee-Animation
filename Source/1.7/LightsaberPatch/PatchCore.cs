using HarmonyLib;
using JetBrains.Annotations;
using Verse;

namespace AM.LightsaberPatch;

[HotSwapAll, UsedImplicitly]
public class PatchCore : Mod
{
    public static Harmony HarmonyInstance { get; private set; }
    
    public static void Log(string msg)
    {
        Core.Log($"<color=#ffa8fc>[Lightsaber Patch]</color> {msg}");
    }

    public PatchCore(ModContentPack content) : base(content)
    {
        HarmonyInstance = new Harmony(content.PackageId);
        HarmonyInstance.PatchAll();
        Log("Loaded lightsaber patch!");
    }
}