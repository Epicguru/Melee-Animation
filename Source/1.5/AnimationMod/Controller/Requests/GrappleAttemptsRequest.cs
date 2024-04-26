using AM.Controller.Reports;
using Verse;

namespace AM.Controller.Requests;

public struct GrappleAttemptRequest
{
    /// <summary>
    /// The pawn to do the grappling.
    /// </summary>
    public Pawn Grappler;

    /// <summary>
    /// The target pawn to be grappled.
    /// </summary>
    public Pawn Target;

    /// <summary>
    /// The optional target cell for the grapple.
    /// It is assumed to be adjacent to the pawn.
    /// </summary>
    public IntVec3? DestinationCell;

    /// <summary>
    /// If <see cref="DestinationCell"/> is null,
    /// this defines the destination cell selection algorithm.
    /// </summary>
    public GrappleSpotPickingBehaviour GrappleSpotPickingBehaviour;

    /// <summary>
    /// If true, the <see cref="Grappler"/> is not checked for a lasso.
    /// </summary>
    public bool DoNotCheckLasso;

    /// <summary>
    /// If true, the <see cref="Grappler"/> is not checked for lasso cooldown.
    /// </summary>
    public bool DoNotCheckCooldown;

    /// <summary>
    /// If true, the <see cref="GrappleAttemptReport.ErrorMessage"/> is not populated, to save time.
    /// </summary>
    public bool NoErrorMessages;

    /// <summary>
    /// The optional occupied mask around the <see cref="Grappler"/>.
    /// If supplied, it speeds up the operation by avoiding checking the map constantly.
    /// </summary>
    public uint? OccupiedMask;

    /// <summary>
    /// If true, most lasso checks are skipped (such as melee skill, manipulation etc.)
    /// </summary>
    public bool TrustLassoUsability;

    /// <summary>
    /// Allows the specification of lasso range. If null, it is extracted from the <see cref="Grappler"/>'s stats.
    /// </summary>
    public float? LassoRange;
}
