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
}
