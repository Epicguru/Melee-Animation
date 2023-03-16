using RimWorld;
using Verse;

namespace AM.Stats;

public class StatWorker_ExecCooldown : StatWorker
{
    public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
    {
        // Get base value, just from def.
        var baseValue = base.GetValueUnfinalized(req, applyPostProcess);
        var pawn = req.Pawn ?? req.Thing as Pawn;
        if (pawn == null)
            return baseValue;

        // Modify based on mod settings.
        bool isFriendly = !pawn.HostileTo(Faction.OfPlayerSilentFail);
        baseValue *= isFriendly ? Core.Settings.FriendlyExecCooldownFactor : Core.Settings.EnemyExecCooldownFactor;

        return baseValue;
    }

    public override string GetExplanationUnfinalized(StatRequest req, ToStringNumberSense numberSense)
    {
        string baseExp = base.GetExplanationUnfinalized(req, numberSense);
        var pawn = req.Pawn ?? req.Thing as Pawn;
        if (pawn == null)
            return baseExp;

        // Modify based on mod settings.
        bool isFriendly = !pawn.HostileTo(Faction.OfPlayerSilentFail);
        float coef = isFriendly ? Core.Settings.FriendlyExecCooldownFactor : Core.Settings.EnemyExecCooldownFactor;

        baseExp = $"{baseExp.TrimEnd()}\n" + $"AM.Stats.{(isFriendly ? "Friendly" : "Enemy")}Coef".Trs(coef.ToString("F2"));
        return baseExp;
    }
}