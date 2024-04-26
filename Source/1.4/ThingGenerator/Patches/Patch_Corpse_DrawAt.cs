using System.Collections.Generic;
using AM.AMSettings;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace AM.Patches
{
    [HarmonyPatch(typeof(Corpse), nameof(Corpse.DrawAt))]
    public static class Patch_Corpse_DrawAt
    {
        public static readonly Dictionary<Corpse, CorpseInterpolate> Interpolators = new Dictionary<Corpse, CorpseInterpolate>();

        public static void Tick()
        {
            if(GenTicks.TicksGame % (60 * 30) == 0)
                Interpolators.RemoveAll(p => !p.Key.Spawned);
        }

        [HarmonyPriority(Priority.First)]
        static void Prefix(Corpse __instance, ref Vector3 drawLoc)
        {
            if (Core.Settings.CorpseOffsetMode == CorpseOffsetMode.None)
                return;

            if (!Interpolators.TryGetValue(__instance, out var found))
                return;

            if (!found.Update(ref drawLoc))
                Interpolators.Remove(__instance);
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
}
