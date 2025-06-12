using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using AM.Controller.Requests;
using Verse;

namespace AM.Controller.Reports;

public struct ExecutionAttemptReport : IDisposable
{
    private static readonly ConcurrentQueue<List<PossibleExecution>> listPool = new ConcurrentQueue<List<PossibleExecution>>();

    private static readonly NamedArgument[] namedArgs = new NamedArgument[4];

    public static List<PossibleExecution> BorrowList() => listPool.TryDequeue(out var found) ? found : new List<PossibleExecution>(32);

    private static void ReturnList(List<PossibleExecution> list) => listPool.Enqueue(list);

    private static NamedArgument[] GetNamedArgs(in ExecutionAttemptRequest request, Pawn target, string intErrorMsg, string additional)
    {
        namedArgs[0] = new NamedArgument(request.Executioner, "Exec");
        namedArgs[1] = new NamedArgument(target, "Target");
        namedArgs[2] = new NamedArgument(intErrorMsg, "Error");
        if (additional != null)
            namedArgs[3] = new NamedArgument(additional, "Additional");
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
    /// If true, indicates that this report means that the executioner cannot perform any execution,
    /// such as because they are not holding a weapon or their execution is on cooldown.
    /// </summary>
    public bool IsFinal => !CanExecute && Target == null;

    public bool IsWalking => CanExecute && (PossibleExecutions == null || !PossibleExecutions.Any());

    /// <summary>
    /// Is any execution possible?
    /// </summary>
    public bool CanExecute;

    /// <summary>
    /// An enumeration of possible executions.
    /// Will be null if <see cref="CanExecute"/> is false.
    /// </summary>
    public List<PossibleExecution> PossibleExecutions;

    /// <summary>
    /// The pawn that could be executed.
    /// </summary>
    public Pawn Target;

    public readonly string ErrorMessage;
    public readonly string ErrorMessageShort;

    public ExecutionAttemptReport(in ExecutionAttemptRequest req, string errorTrsKey, string intErrorMsg = null, string additional = null) : this(req, null, errorTrsKey, intErrorMsg, additional)
    {
    }

    public ExecutionAttemptReport(in ExecutionAttemptRequest req, Pawn target, string errorTrsKey, string intErrorMsg = null, string additional = null)
    {
        CanExecute = false;
        PossibleExecutions = null;
        Target = target;

        if (req.NoErrorMessages)
        {
            ErrorMessage = null;
            ErrorMessageShort = null;
        }
        else
        {
            var args = GetNamedArgs(req, target, intErrorMsg, additional);
            errorTrsKey = $"AM.Error.Exec.{errorTrsKey}";
            ErrorMessage = errorTrsKey.Translate(args);
            ErrorMessageShort = intErrorMsg == null ? GetShortTrs(errorTrsKey, ErrorMessage, args) : ErrorMessage;
        }
    }

    public void Dispose()
    {
        if (PossibleExecutions == null)
            return;

        var temp = PossibleExecutions;
        PossibleExecutions = null;
        ReturnList(temp);
    }
}

public struct PossibleExecution
{
    public bool IsValid => Animation.AnimDef != null;

    /// <summary>
    /// Info about the animation to be started.
    /// </summary>
    public AnimStartData Animation;

    /// <summary>
    /// If not null, the pawn must be lassoed to this position in order for the execution to happen.
    /// </summary>
    public IntVec3? LassoToHere;

    public struct AnimStartData
    {
        public float Probability => AnimDef?.Probability ?? 0;

        /// <summary>
        /// The animation to be played.
        /// </summary>
        public AnimDef AnimDef;

        /// <summary>
        /// Whether the animation needs to be mirrored horizontally.
        /// </summary>
        public bool FlipX;

        /// <summary>
        /// Space bitmask for occupied slots.
        /// </summary>
        public required ulong OccupiedMask;
    }
}
