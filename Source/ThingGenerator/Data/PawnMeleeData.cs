using Verse;

namespace AAM.Data
{
    public class PawnMeleeData : IExposable
    {
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
