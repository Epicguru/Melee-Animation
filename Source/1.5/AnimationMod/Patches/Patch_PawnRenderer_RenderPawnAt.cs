using AM.Grappling;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace AM.Patches;

/// <summary>
/// This is the 'main' patch that actually gets pawns rendering in animations.
/// When a pawn is being animated, RenderPawnAt needs to be modified:
/// first, the private field 'results' needs to be modified in order to ensure that RenderPawnDynamic is called.
/// Next, call <see cref="PawnRenderTree.ParallelPreDraw(PawnDrawParms)"/> with the arguments that I want in order to modify how the pawn will actually be displayed.
/// </summary>
[HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.RenderPawnAt))]
public static class Patch_PawnRenderer_RenderPawnAt
{
    public static bool AllowNext;
    public static bool DoNotModify = false;
    public static DrawMode NextDrawMode = DrawMode.Full;
    public static Rot4 HeadRotation; // Only used when NextDrawMode is HeadOnly or HeadStandalone.
    public static float StandaloneHeadAngle;    
    public static Vector3 StandaloneHeadPosition;    

    [HarmonyPriority(Priority.First)]
    public static bool Prefix(Pawn ___pawn, PawnRenderTree ___renderTree, ref PawnRenderer.PreRenderResults ___results, ref PawnRenderer.PreRenderResults? __state)
    {
        __state = null; // Harmony requires this to be initialized.

        // Draw the ropes that bind the pawn when being grappled.
        DrawGrappleRopeIfRequired(___pawn);

        // Standalone heads don't have or need animator.
        AnimRenderer animator = null;
        if (NextDrawMode != DrawMode.HeadStandalone)
        {
            // Try to get an active animator for this pawn.
            animator = PatchMaster.GetAnimator(___pawn);
            if (animator == null)
            {
                return true;
            }
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

        // Because of the way that the stump and head renderer work, it can be necessary to force the IsHead property
        // to return a certain value when it suits us.
        if (NextDrawMode == DrawMode.HeadStandalone)
            Patch_HediffSet_HasHead.ForcedHasHeadValue = true;

        // Force RenderTree to recalculate with the new parameters.
        ___renderTree.SetDirty();
        ___renderTree.EnsureInitialized(___results.parms.flags);
        ___renderTree.ParallelPreDraw(___results.parms);

        Patch_HediffSet_HasHead.ForcedHasHeadValue = null;

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

    private static void MakeDrawArgs(AnimRenderer animator, Pawn pawn, ref PawnDrawParms parms)
    {
        // During certain special animations, the pawn is being animated by the regular draw is still
        // used, such as the punt animation.
        if (DoNotModify)
        {
            // However, still remove the invisible flag as it is not wanted.
            parms.flags &= ~PawnRenderFlags.Invisible;
            return;
        }

        // Standalone heads get their own method.
        if (NextDrawMode == DrawMode.HeadStandalone)
        {
            // It seems that pawn is not populated when the pawn is dead?
            parms.pawn = pawn;
            MakeDrawArgsForStandaloneHead(ref parms);
            return;
        }

        // Try to get the pawn body or head snapshot, depending on the current mode.
        var part = NextDrawMode == DrawMode.HeadOnly
            ? animator.GetPawnHead(pawn)
            : animator.GetPawnBody(pawn);
        var snapshot = animator.GetSnapshot(part);

        // Debug:
        //parms.tint = Color.magenta;

        // Must be standing.
        parms.posture = PawnPosture.Standing;

        // Remove invisible flag.
        parms.flags &= ~PawnRenderFlags.Invisible;

        // Set facing direction:
        var facing = NextDrawMode == DrawMode.HeadOnly
            ? HeadRotation
            : snapshot.GetWorldDirection();
        parms.facing = facing;

        // New transform matrix calculation:
        Vector3 worldPos = snapshot.GetWorldPosition();
        float worldAngle = snapshot.GetWorldRotation();
        Vector3 scale = snapshot.LocalScale; // Just use local part as scale, not currently used anyway but might be useful in future.
        parms.matrix = Matrix4x4.TRS(worldPos + pawn.ageTracker.CurLifeStage.bodyDrawOffset, Quaternion.Euler(0, worldAngle, 0), scale);
    
        // Some additional flags depending on the rendering mode:
        switch (NextDrawMode)
        {
            case DrawMode.BodyOnly:
                // When rendering a headless body, add a head stump.
                // Note: do not skip the head using a flag, because that causes the stump to not be rendered.
                parms.flags |= PawnRenderFlags.HeadStump;

                // TODO known issue: head stump does not render because the render tree node checks for 
                // the presence of a head before drawing the stump, regardless of the flag. Fix!

                break;

            case DrawMode.HeadOnly:
                // Don't render the body.
                parms.skipFlags |= AM_DefOf.Body;
                break;
        }
    }

    private static void MakeDrawArgsForStandaloneHead(ref PawnDrawParms parms)
    {
        parms.skipFlags |= AM_DefOf.Body; // Don't draw the body, just the head.
        parms.posture = PawnPosture.Standing; // Don't use the wrong mesh set.
        parms.flipHead = false; //  Don't flip the head round.
        //parms.dead = false;
        parms.tint = Color.white; // Don't tint transparent. Idk why it gets set to (0, 0, 0, 0) upon death, but it does...
        parms.rotDrawMode = RotDrawMode.Fresh; // Force the head to not rot.
        StandaloneHeadPosition.y = AltitudeLayer.Building.AltitudeFor(); // Force the head to be put on the ground, should be done elsewhere, but I'm lazy.

        // Always render headgear and clothes, do not render head stump (head stump prevents head from drawing).
        parms.flags |= PawnRenderFlags.Headgear | PawnRenderFlags.Clothes;
        parms.flags &= ~PawnRenderFlags.HeadStump;

        // Set correct position and angle for head.
        parms.facing = HeadRotation;
        parms.matrix = Matrix4x4.TRS(StandaloneHeadPosition, Quaternion.Euler(0, StandaloneHeadAngle, 0), Vector3.one);
    }

    public enum DrawMode
    {
        Full,
        BodyOnly,
        HeadOnly,
        HeadStandalone
    }
}
