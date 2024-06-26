﻿using System;
using AM.Idle;
using System.Collections.Generic;
using CombatAI;
using Verse;

namespace AM.CAI5000Patch;

public static class CAI5000AnimationPatch
{
    private static readonly Dictionary<Map, MapComponent_FogGrid> foggers = new Dictionary<Map, MapComponent_FogGrid>();

    public static void Init()
    {
        IdleControllerComp.ShouldDrawAdditional.Add(ShouldDraw);

        GameComp.LazyTick += FlushOldMapsFromCache;

        AnimRenderer.PrePawnSpecialRender += FixCustomRenderInAnimator.PreCustomPawnRender;
        AnimRenderer.PostPawnSpecialRender += FixCustomRenderInAnimator.PostCustomPawnRender;
    }

    private static MapComponent_FogGrid GetFogger(ThingComp comp)
    {
        var map = comp.parent?.Map;
        if (map == null)
            return null;

        if (foggers.TryGetValue(map, out var found))
            return found;

        found = map.GetComponent<MapComponent_FogGrid>();
        foggers.Add(map, found);
        return found;
    }

    public static void FlushOldMapsFromCache()
    {
        // Remove destroyed maps.
        foggers.RemoveAll(p => p.Key.Index < 0);
    }

    public static bool ShouldDraw(IdleControllerComp comp)
    {
        try
        {
            if (!Finder.Settings.FogOfWar_Enabled)
                return true;

            var fogger = GetFogger(comp);
            if (fogger == null)
                return true;

            // This is the CAI5000 fog-of-war check:
            return !fogger.IsFogged(comp.parent.Position);
        }
        catch (Exception e)
        {
            Core.Error("CAI5000 patch error:", e);
            return true;
        }
    }
}
