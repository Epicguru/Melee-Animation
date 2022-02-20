using System;
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
        public AutoOption AutoGrapple;

        public bool ShouldSave()
        {
            return Pawn is { Destroyed: false };
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref Pawn, "pawn");
            Scribe_Values.Look(ref AutoExecute, "autoExecute");
            Scribe_Values.Look(ref AutoGrapple, "autoGrapple");
        }
    }
}
