using HarmonyLib;
using JetBrains.Annotations;
using Verse;

namespace AM.FacialAnimationPatch;

/// <summary>
/// Note to future maintainers: This patch assembly currently does nothing, it
/// used to have some Harmony patches, now they are no longer required.
/// Removing the patch csproj, and doing the build pipeline changes required would be annoying, so I'm keeping it.
/// </summary>
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