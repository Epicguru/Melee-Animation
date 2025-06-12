using HarmonyLib;
using JetBrains.Annotations;
using Verse;

namespace AM.PerformanceOptimizerPatch
{
    [HotSwapAll, UsedImplicitly]
    public class PatchCore : Mod
    {
        public static Harmony HarmonyInstance { get; private set; }

        public static void Log(string msg)
        {
            Core.Log($"<color=#ffa8fc>[Perf.Opt]</color> {msg}");
        }

        public static void Error(string msg)
        {
            Core.Error($"<color=#ffa8fc>[Perf.Opt]</color> {msg}");
        }

        public PatchCore(ModContentPack content) : base(content)
        {
            Log("Loaded Performance Optimizer patch!");

            // Patch all.
            HarmonyInstance = new Harmony(content.PackageId);
            HarmonyInstance.PatchAll();

            Log("Successfully patched and disabled buggy optimizations.");
        }
    }
}