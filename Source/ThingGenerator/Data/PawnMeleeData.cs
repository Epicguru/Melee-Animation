using RimWorld;
using System;
using UnityEngine;
using Verse;

namespace AAM.Data
{
    public class PawnMeleeData : IExposable
    {
        public bool ResolvedAutoExecute => AutoExecute switch
        {
            AutoOption.Enabled => true,
            AutoOption.Disabled => false,
            AutoOption.Default => Core.Settings.AutoExecute,
            _ => throw new ArgumentOutOfRangeException()
        };
        public bool ResolvedAutoGrapple => AutoGrapple switch
        {
            AutoOption.Enabled => true,
            AutoOption.Disabled => false,
            AutoOption.Default => Core.Settings.AutoGrapple,
            _ => throw new ArgumentOutOfRangeException()
        };

        public Pawn Pawn;
        public AutoOption AutoExecute;
        public float TimeSinceExecuted = 100;
        public AutoOption AutoGrapple;
        public float TimeSinceGrappled = 100;

        public int lastTickPresentedOptions = -1; // Not saved.

        public bool ShouldSave()
        {
            return Pawn is { Destroyed: false };
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref Pawn, "pawn");
            Scribe_Values.Look(ref AutoExecute, "autoExecute");
            Scribe_Values.Look(ref AutoGrapple, "autoGrapple");
            Scribe_Values.Look(ref TimeSinceExecuted, "timeSinceExecuted", 100);
            Scribe_Values.Look(ref TimeSinceGrappled, "timeSinceGrappled", 100);
        }

        public float GetExecuteCooldownMax() => Pawn.GetStatValue(AAM_DefOf.AAM_ExecutionCooldown);
        public float GetExecuteCooldownPct() => GetExecuteCooldownMax() <= 0 ? 1 : Mathf.Clamp01(TimeSinceExecuted / GetExecuteCooldownMax());
        public bool IsExecutionOffCooldown() => GetExecuteCooldownMax() <= TimeSinceExecuted;

        public float GetGrappleCooldownMax() => Pawn.GetStatValue(AAM_DefOf.AAM_GrappleCooldown);
        public float GetGrappleCooldownPct() => GetGrappleCooldownMax() <= 0 ? 1 : Mathf.Clamp01(TimeSinceGrappled / GetGrappleCooldownMax());
        public bool IsGrappleOffCooldown() => GetGrappleCooldownMax() <= TimeSinceGrappled;
    }
}
