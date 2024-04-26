using System.Collections.Generic;
using AM.AMSettings;
using HarmonyLib;
using UnityEngine;
using Verse;
using LudeonTK;

namespace AM.Patches;

[HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.ParallelPreRenderPawnAt))] // Corpse DrawAt was removed in 1.5
public static class Patch_Corpse_DrawAt
{
    public static readonly Dictionary<Corpse, CorpseInterpolate> Interpolators = new Dictionary<Corpse, CorpseInterpolate>();
    public static readonly Dictionary<Pawn, Rot4> OverrideRotations = new Dictionary<Pawn, Rot4>();

    public static void Tick()
    {
        if(GenTicks.TicksGame % (60 * 30) == 0)
        {
            Interpolators.RemoveAll(p => !p.Key.Spawned);
            OverrideRotations.RemoveAll(p => !p.Key.SpawnedOrAnyParentSpawned);
        }
    }

    [HarmonyPriority(Priority.Last)]
    private static void Prefix(PawnRenderer __instance, ref Vector3 drawLoc, ref Rot4? rotOverride)
    {
        var corpse = __instance.pawn.ParentHolder as Corpse;

        if (corpse == null)
            return;

        DoOffsetLogic(corpse, ref drawLoc, ref rotOverride);
    }

    private static void DoOffsetLogic(Corpse corpse, ref Vector3 drawLoc, ref Rot4? rotOverride)
    {
        // Override facing direction of corpse:
        if (OverrideRotations.TryGetValue(corpse.InnerPawn, out var foundRot))
        {
            rotOverride = foundRot;
        }

        // Override position and rotation of corpse...

        if (Core.Settings.CorpseOffsetMode == CorpseOffsetMode.None)
            return;

        if (!Interpolators.TryGetValue(corpse, out var found))
            return;

        if (!found.Update(ref drawLoc))
            Interpolators.Remove(corpse);
    }
}

public class CorpseInterpolate
{
    [TweakValue("AM", 0.01f, 100f)]
    public static float CorpseLerpSpeed = 5;

    public Vector3 TargetPosition;
    public Vector3 CurrentPosition;
    public Vector3 InitialOffset;

    public CorpseInterpolate(Corpse corpse, Vector3 startPos)
    {
        TargetPosition = corpse.DrawPos;
        startPos.y = TargetPosition.y;
        InitialOffset = startPos - TargetPosition;
        CurrentPosition = startPos;
    }

    public bool Update(ref Vector3 drawLoc)
    {
        switch (Core.Settings.CorpseOffsetMode)
        {
            case CorpseOffsetMode.InterpolateToCorrect:
                drawLoc += InitialOffset;
                InitialOffset = Vector3.MoveTowards(InitialOffset, Vector3.zero, Time.deltaTime * CorpseLerpSpeed);
                return InitialOffset.sqrMagnitude > 0.001f;

            case CorpseOffsetMode.KeepOffset:
                drawLoc += InitialOffset;
                return true;

            default:
                return false;
        }
    }
}