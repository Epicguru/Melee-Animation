using AM.Controller.Requests;
using System;
using Verse;

namespace AM.Controller.Reports;

public readonly struct DuelAttemptReport
{
    private static readonly NamedArgument[] namedArgs = new NamedArgument[4];

    private static NamedArgument[] GetNamedArgs(in DuelAttemptRequest request, string intErrorMsg, bool pawnIsA)
    {
        namedArgs[0] = new NamedArgument(request.A, "A");
        namedArgs[1] = new NamedArgument(request.B, "B");
        namedArgs[2] = new NamedArgument(intErrorMsg, "Error");
        namedArgs[3] = new NamedArgument(pawnIsA ? request.A : request.B, "Pawn");
        return namedArgs;
    }

    private static string GetShortTrs(string trsKey, string trs, NamedArgument[] args)
    {
        string shortKey = $"{trsKey}.Short";
        if (shortKey.CanTranslate())
            return shortKey.Translate(args);
        return trs;
    }

    /// <summary>
    /// Can the pawns start a duel?
    /// </summary>
    public bool CanStartDuel { get; init; }

    /// <summary>
    /// Must the pawn(s) walk to the target to start a duel?
    /// If false, then the duel can be started immediately.
    /// </summary>
    public bool MustWalk { get; init; }

    /// <summary>
    /// The duel animation to be used. Will be null if <see cref="CanStartDuel"/> is false
    /// or <see cref="MustWalk"/> is true.
    /// </summary>
    public AnimDef DuelAnimation { get; init; }

    /// <summary>
    /// Should the <see cref="DuelAnimation"/> be started flipped?
    /// If null, a random flip can be chosen.
    /// </summary>
    public bool? AnimFlipX { get; init; }

    /// <summary>
    /// If <see cref="CanStartDuel"/> is false this is the reason why.
    /// </summary>
    public string ErrorMessage { get; }

    /// <summary>
    /// If <see cref="CanStartDuel"/> is false this is the reason why (short version).
    /// </summary>
    public string ErrorMessageShort { get; }

    public DuelAttemptReport(in DuelAttemptRequest req, string errorTrsKey, string intErrorMsg = null, bool pawnA = true)
    {
        if (errorTrsKey == null)
            throw new ArgumentNullException(nameof(errorTrsKey));

        if (req.NoErrorMessages)
        {
            ErrorMessage = null;
            ErrorMessageShort = null;
        }
        else
        {
            var args = GetNamedArgs(req, intErrorMsg, pawnA);
            errorTrsKey = $"AM.Error.Duel.{errorTrsKey}";
            ErrorMessage = errorTrsKey.Translate(args);
            ErrorMessageShort = intErrorMsg == null ? GetShortTrs(errorTrsKey, ErrorMessage, args) : ErrorMessage;
        }
    }
}
