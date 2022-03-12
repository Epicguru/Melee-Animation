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
        public float TimeSinceExecuted;
        public AutoOption AutoGrapple;
        public float TimeSinceGrappled;

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
            Scribe_Values.Look(ref TimeSinceExecuted, "timeSinceExecuted");
            Scribe_Values.Look(ref TimeSinceGrappled, "timeSinceGrappled");
        }

        public float GetExecuteCooldownPct(float cooldownTime) => cooldownTime <= 0 ? 1 : Mathf.Clamp01(TimeSinceExecuted / cooldownTime);
        public bool IsExecutionOffCooldown(float cooldownTime) => cooldownTime <= TimeSinceExecuted;

        public float GetGrappleCooldownPct(float cooldownTime) => cooldownTime <= 0 ? 1 : Mathf.Clamp01(TimeSinceGrappled / cooldownTime);
        public bool IsGrappleOffCooldown(float cooldownTime) => cooldownTime <= TimeSinceGrappled;
    }
}
