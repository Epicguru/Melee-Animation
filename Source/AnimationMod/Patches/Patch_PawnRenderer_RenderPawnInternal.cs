using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace AM.Patches;

/// <summary>
/// Overrides various parameters of the pawn rendering, specifically direction (north, east etc.)
/// and body angle. Driven by the active animation.
/// Also used to render severed heads.
/// </summary>
//[HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.RenderPawnInternal))]
static class Patch_PawnRenderer_RenderPawnInternal
{
    /*
     * Steps to ensure that I can render exactly how I want:
     */

    public static bool AllowNext;
    public static bool DoNotModify = false;
    public static DrawMode NextDrawMode = DrawMode.Full;
    public static Rot4 HeadRotation; // Only used when NextDrawMode is HeadOnly or HeadStandalone.

    public static float StandaloneHeadRotation;

    public enum DrawMode
    {
        Full,
        BodyOnly,
        HeadOnly,
        HeadStandalone
    }

    [HarmonyPriority(Priority.Last)] // As late as possible. We want to be the last to modify results.
    [HarmonyAfter("com.yayo.yayoAni")] // Go away.
    [HarmonyBefore("rimworld.Nals.FacialAnimation")] // Must go before facial animation otherwise the face gets fucky.
    private static bool Prefix(PawnRenderer __instance, Pawn ___pawn, ref PawnDrawParms parms)
    {
        //parms.matrix

        __instance.renderTree.ParallelPreDraw(parms);

        return true;

        // Do not modify when the result will be stored in cache.
        if (parms.Cache)
            return true;

        float angle = parms.matrix.rotation.eulerAngles.y;
        float oldAngle = angle;

        bool renderBody = !parms.skipFlags.HasFlag(AM_DefOf.Body);

        ref Rot4 bodyFacing = ref parms.facing;
        ref PawnRenderFlags flags = ref parms.flags;

        if (AllowNext)
        {
            //parms.matrix *= Matrix4x4.Scale(0.5f * Vector3.one);
            //__instance.renderTree.SetDirty();
            //__instance.EnsureGraphicsInitialized();
            //parms.coveredInFoam = true;
            AllowNext = false;
            return true;
        }


        return false;

        bool result = ModifyRenderData(___pawn, ref bodyFacing, ref angle, ref flags, ref renderBody);

        if (renderBody)
            parms.skipFlags |= AM_DefOf.Body;
        else
            parms.skipFlags &= ~AM_DefOf.Body;

        float delta = oldAngle - angle;
        if (Math.Abs(delta) > 0.001f)
        {
            parms.matrix *= Matrix4x4.Rotate(Quaternion.Euler(45, delta, 0));
        }

        return result;
    }

    public static bool ModifyRenderData(Pawn ___pawn, ref Rot4 bodyFacing, ref float angle, ref PawnRenderFlags flags, ref bool renderBody)
    {
        // Do not affect portrait rendering:
        if (flags.HasFlag(PawnRenderFlags.Portrait))
            return true;

        // Standalone head (i.e. dropped head on ground after animation) gets a custom method that does things slightly differently.
        if (NextDrawMode == DrawMode.HeadStandalone)
            return RenderStandaloneHeadMode(ref bodyFacing, ref flags, ref angle, ref renderBody);

        // Get the animator for this pawn.
        var anim = PatchMaster.GetAnimator(___pawn);
        if (anim != null)
        {
            if (!DoNotModify)
            {
                //var part = NextDrawMode == DrawMode.HeadOnly ? anim.GetPawnHead(___pawn) : anim.GetPawnBody(___pawn);
                //var snapshot = anim.GetSnapshot(part);
                //angle = snapshot.GetWorldRotation();

                angle = 5;
                bodyFacing = Rot4.East;
                renderBody = false;

                //bodyFacing = NextDrawMode == DrawMode.HeadOnly ? HeadRotation : snapshot.GetWorldDirection();

                //switch (NextDrawMode)
                //{
                //    case DrawMode.BodyOnly:
                //        // Render head stump, do not render head gear.
                //        flags |= PawnRenderFlags.HeadStump;
                //        flags &= ~PawnRenderFlags.Headgear;
                //        break;

                //    case DrawMode.HeadOnly:
                //        // Do not render body.
                //        renderBody = false;
                //        break;
                //}
            }

            if (!AllowNext)
                return false;
        }

        AllowNext = false;
        return true;
    }

    private static bool RenderStandaloneHeadMode(ref Rot4 bodyFacing, ref PawnRenderFlags flags, ref float angle, ref bool renderBody)
    {
        // Add headgear, remove head stump.
        flags |= PawnRenderFlags.Headgear | PawnRenderFlags.DrawNow;
        flags &= ~PawnRenderFlags.HeadStump;

        angle = StandaloneHeadRotation;
        bodyFacing = HeadRotation;
        renderBody = false;
        return true;
    }
}
