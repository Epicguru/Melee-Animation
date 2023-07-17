using System.Collections.Generic;
using Verse;

namespace AM.Controller.Requests;

public struct ExecutionAttemptRequest
{
    /// <summary>
    /// The executioner pawn.
    /// </summary>
    public Pawn Executioner;
    /// <summary>
    /// The occupied mask for the <see cref="Executioner"/>.
    /// </summary>
    public ulong OccupiedMask;
    /// <summary>
    /// The small occupied mask for the executioner.
    /// </summary>
    public uint SmallOccupiedMask;
    /// <summary>
    /// Should the west cell be checked?
    /// </summary>
    public bool WestCell;
    /// <summary>
    /// Should the east cell be checked?
    /// </summary>
    public bool EastCell;
    /// <summary>
    /// Is the <see cref="Executioner"/> allowed to use their lasso (if any)?
    /// </summary>
    public bool CanUseLasso;
    /// <summary>
    /// Is the executioner allowed to walk to the target(s) to execute them?
    /// </summary>
    public bool CanWalk;
    /// <summary>
    /// If true, most lasso checks are skipped (such as melee skill, manipulation etc.)
    /// Only valid if <see cref="CanUseLasso"/> is true.
    /// </summary>
    public bool TrustLassoUsability;

    /// <summary>
    /// Allows the specification of lasso range. If null, it is extracted from the <see cref="Executioner"/>'s stats.
    /// </summary>
    public float? LassoRange;

    /// <summary>
    /// The main target.
    /// </summary>
    public Pawn Target;

    /// <summary>
    /// Optional additional targets.
    /// </summary>
    public IEnumerable<Pawn> Targets;

    /// <summary>
    /// If true no error messages will be translated to save time.
    /// </summary>
    public bool NoErrorMessages;

    /// <summary>
    /// If true, skips the execution cooldown check.
    /// </summary>
    public bool IgnoreCooldown;

    /// <summary>
    /// If not null, only these animations are checked.
    /// </summary>
    public IEnumerable<AnimDef> OnlyTheseAnimations;
}
