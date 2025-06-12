using AM.Controller;
using JetBrains.Annotations;
using Vehicles;
using Verse;

namespace AM.VehiclesPatch;

[HotSwapAll]
[UsedImplicitly]
public class PatchCore : Mod
{
    public static void Log(string msg)
    {
        Core.Log($"<color=#ffa8fc>[Vehicle Framework Patch]</color> {msg}");
    }

    public PatchCore(ModContentPack content) : base(content)
    {
        Log("Loaded vehicle framework patch!");
        ActionController.CanBeExecutedPredicates.Add(IsValidExecuteTarget);
    }

    public static bool IsValidExecuteTarget(Pawn pawn)
    {
        // If the pawn is a vehicle, don't allow execution.
        return pawn is not VehiclePawn;
    }
}