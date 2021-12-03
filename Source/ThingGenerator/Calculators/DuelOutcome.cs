using Verse;

namespace AAM.Calculators
{
    public struct DuelOutcome
    {
        public DuelOutcomeType Type;
        public Pawn PawnA, PawnB;
        public Pawn Winner;
        public float Certainty;
        public string GenDebug;

        public override string ToString()
        {
            return $"{PawnA?.NameShortColored ?? "nobody"} vs {PawnB?.NameShortColored ?? "nobody"}: {Winner?.NameShortColored ?? "nobody"} wins, with outcome {Type} ({Certainty*100f:F0}% certainty){(GenDebug == null ? "" : $". Debug:\n{GenDebug}")}";
        }
    }
}
