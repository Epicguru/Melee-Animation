using HarmonyLib;
using JetBrains.Annotations;
using Verse;

namespace AM.FacialAnimationPatch;

[UsedImplicitly]
[HotSwapAll]
public sealed class PatchCore : Mod
{
    public static void Log(string msg)
    {
        Core.Log($"[<color=#63e0ff>Facial Animation Patch</color>] {msg}");
    }

    public PatchCore(ModContentPack content) : base(content)
    {
        var harmony = new Harmony(content.Name);
        harmony.PatchAll();

        Log("Successfully loaded Facial Animation patch.");
    }
}