using AM.Grappling;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace AM.Patches;

/// <summary>
/// When a pawn is being animated, RenderPawnAt needs to be modified:
/// first, the private field 'results' needs to be modified in order to ensure that RenderPawnDynamic is called.
/// Next, call <see cref="PawnRenderTree.ParallelPreDraw(PawnDrawParms)"/> with the arguments that I want in order to modify how the pawn will actually be displayed.
/// </summary>
[HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.RenderPawnAt))]
public static class Patch_PawnRenderer_RenderPawnAt
{
    public static bool AllowNext;

    [HarmonyPriority(Priority.First)]
    public static bool Prefix(Pawn ___pawn, PawnRenderTree ___renderTree, ref PawnRenderer.PreRenderResults ___results, ref PawnRenderer.PreRenderResults? __state)
    {
        __state = null; // Harmony requires this to be initialized.

        // Draw the ropes that bind the pawn when being grappled.
        DrawGrappleRopeIfRequired(___pawn);

        // Try to get an active animator for this pawn.
        var animator = PatchMaster.GetAnimator(___pawn);
        if (animator == null)
        {
            return true;
        }

        // Ok, the pawn is in an active animation, but we want to suppress the regular draw call
        // in order to draw only when we want to. This is controlled by the AllowNext flag.
        if (!AllowNext)
        {
            return false;
        }

        // Auto-reset AllowNext flag.
        AllowNext = false;

        // Pawn is being animated, so the 'results' field must be modified to
        // ensure that RenderPawnInternal gets called, as opposed to cached rendering.
        // Store the current value of results in 'state', so that is can be restored in the postfix.
        __state = ___results;

        // Setting these values ensures that RenderPawnInternal gets called:
        ___results.valid = true;
        ___results.draw = true;
        ___results.useCached = false;

        // Make are the arguments that get passed into RenderPawnInternal and ParallelPreDraw.
        // Note that the original results.parms is used as a base, which is useful because we can inherit some
        // flags like 'coveredInFoam'.
        MakeDrawArgs(animator, ___pawn, ref ___results.parms);

        // Most importantly, need to call ParallelPreRender on the renderTree to actually
        // set up all the matrices and whatnot that gets used when renderTree draw is called.
        ___renderTree.ParallelPreDraw(___results.parms);

        // Do run the regular RenderPawnAt method now.
        return true;
    }

    [HarmonyPriority(Priority.First)]
    public static void Postfix(PawnRenderer.PreRenderResults? __state, ref PawnRenderer.PreRenderResults ___results)
    {
        // Restore private field 'results' to original value before modification, 
        // if required.
        if (__state != null)
        {
            ___results = __state.Value;
        }
    }

    private static void DrawGrappleRopeIfRequired(Pawn pawn)
    {
        var job = pawn.CurJob;
        if (job?.def == AM_DefOf.AM_GrapplePawn)
        {
            JobDriver_GrapplePawn.DrawEnsnaringRope(pawn, job);
        }
    }

    public static void MakeDrawArgs(AnimRenderer animator, Pawn pawn, ref PawnDrawParms parms)
    {
        // Try to get the pawn snapshot.
        var part = animator.GetPawnBody(pawn);
        var snapshot = animator.GetSnapshot(part);

        // Dead pawns should not be animated, but make sure that it is not dead just in case.
        parms.dead = false;

        // Debug:
        parms.tint = Color.magenta;

        // Must be standing.
        parms.posture = RimWorld.PawnPosture.Standing;

        // Remove invisible flag.
        parms.flags &= ~PawnRenderFlags.Invisible;

        // Set facing direction:
        parms.facing = snapshot.GetWorldDirection();
        
        // New transform matrix calculation:
        Vector3 worldPos = snapshot.GetWorldPosition();
        float worldAngle = snapshot.GetWorldRotation();
        Vector3 scale = snapshot.LocalScale; // Just use local part as scale, not currently used anyway but might be useful in future.
        parms.matrix = Matrix4x4.TRS(worldPos + pawn.ageTracker.CurLifeStage.bodyDrawOffset, Quaternion.Euler(0, worldAngle, 0), scale);
    }
}
