using Verse;

namespace AM.Controller.Requests;

public struct DuelAttemptRequest
{
    /// <summary>
    /// The two pawns to be involved in the duel.
    /// Pawn A is the pawn initiating the duel.
    /// </summary>
    public Pawn A, B;

    /// <summary>
    /// If true, error messages are not generated to speed up the process.
    /// </summary>
    public bool NoErrorMessages;

    /// <summary>
    /// Is the duel friendly or not? Affects which cooldown is used.
    /// </summary>
    public bool IsFriendly;
}
