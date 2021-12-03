namespace AAM.Calculators
{
    public enum DuelOutcomeType
    {
        /// <summary>
        /// The dual conditions or pawns are invalid. There is no outcome.
        /// </summary>
        Invalid,

        /// <summary>
        /// The duel concludes in a standstill. The pawns will resume fighting in regular combat.
        /// </summary>
        Nothing,

        /// <summary>
        /// The duel concludes with one pawn injuring the other, using their current weapon.
        /// </summary>
        Hurt,

        /// <summary>
        /// The duel concludes with one pawn seriously injuring, stunning or downing the other pawn.
        /// </summary>
        MaimOrDown,

        /// <summary>
        /// The duel concludes with the victorious pawn executing the other. Blood for the blood god!
        /// </summary>
        Execute
    }
}
