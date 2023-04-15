using JetBrains.Annotations;
using Verse;

namespace AM.CombatExtendedPatch;

[UsedImplicitly]
[HotSwapAll]
public class PatchCore : Mod
{
    public static void Log(string msg)
    {
        Core.Log($"[<color=#63e0ff>CE Patch</color>] {msg}");
    }

    public PatchCore(ModContentPack content) : base(content)
    {
        // Replace the vanilla outcome worker with the combat extended one,
        // which uses the combat extended armor system.
        OutcomeUtility.OutcomeWorker = new CombatExtendedOutcomeWorker();

        Log("Loaded and applied CE patch.");
    }
}