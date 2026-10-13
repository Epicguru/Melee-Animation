using System;
using AM.Controller.Requests;
using Verse;

namespace AM.Controller.Reports;

public struct GrappleAttemptReport
{
    private static readonly NamedArgument[] namedArgs = new NamedArgument[4];

    public static GrappleAttemptReport Success(IntVec3 destinationCell) => new GrappleAttemptReport
    {
        CanGrapple = true,
        DestinationCell = destinationCell
    };

    private static NamedArgument[] GetNamedArgs(in GrappleAttemptRequest request, string intErrorMsg)
    {
        namedArgs[0] = new NamedArgument(request.Grappler, "Grappler");
        namedArgs[1] = new NamedArgument(request.Target, "Target");
        namedArgs[2] = new NamedArgument(intErrorMsg, "Error");
        namedArgs[3] = new NamedArgument(Core.Settings.MinMeleeSkillToLasso, "MeleeSkill");
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
    /// Can the grapple be started?
    /// </summary>
    public bool CanGrapple;

    /// <summary>
    /// The cell that the target must be grappled to.
    /// If <see cref="CanGrapple"/> is false, this value holds no meaning.
    /// </summary>
    public IntVec3 DestinationCell;

    /// <summary>
    /// If <see cref="CanGrapple"/> is false this is the reason why.
    /// </summary>
    public readonly string ErrorMessage;

    /// <summary>
    /// If <see cref="CanGrapple"/> is false this is the reason why (short version).
    /// </summary>
    public readonly string ErrorMessageShort;

    public GrappleAttemptReport(in GrappleAttemptRequest req, string errorTrsKey, string intErrorMsg = null)
    {
        if (errorTrsKey == null)
            throw new ArgumentNullException(nameof(errorTrsKey));

        CanGrapple = false;
        DestinationCell = default;

        if (req.NoErrorMessages)
        {
            ErrorMessage = null;
            ErrorMessageShort = null;
        }
        else
        {
            var args = GetNamedArgs(req, intErrorMsg);
            errorTrsKey = $"AM.Error.Grapple.{errorTrsKey}";
            ErrorMessage = errorTrsKey.Translate(args);
            ErrorMessageShort = intErrorMsg == null ? GetShortTrs(errorTrsKey, ErrorMessage, args) : ErrorMessage;
        }
    }
}
