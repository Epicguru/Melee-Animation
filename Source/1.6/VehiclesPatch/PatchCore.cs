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
        // If the pawn is a vehicle, don't allow execution.
        ActionController.CanExecutePredicates.Add(NotVehiclePawn);
        ActionController.CanBeExecutedPredicates.Add(NotVehiclePawn);
    }

    public static bool NotVehiclePawn(Pawn pawn)
    {
        return pawn is not VehiclePawn;
    }
}