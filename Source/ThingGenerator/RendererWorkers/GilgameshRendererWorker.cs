using System.Collections.Generic;
using System.Linq;
using AM.Grappling;
using AM.Tweaks;
using RimWorld;
using UnityEngine;
using Verse;

namespace AM.RendererWorkers;

public class GilgameshRendererWorker : AnimationRendererWorker
{
    public static readonly object GilgameshWeapon = new object();

    public override void SetupRenderer(AnimRenderer renderer)
    {
        // Get all supported long stabbing weapon tweak data.
        // This is used to ensure correct rendering during the attack.
        var allTweaks = DefDatabase<ThingDef>.AllDefsListForReading.Where(td => td.IsMeleeWeapon()).Select(TweakDataManager.TryGetTweak).Where(tweak => tweak != null && tweak.MeleeWeaponType.HasFlag(MeleeWeaponType.Long_Stab) && string.IsNullOrEmpty(tweak.CustomRendererClass));
        if (!allTweaks.Any())
        {
            Core.Error("Failed to find any long stabbing weapons for use with gilgamesh attack");
            return;
        }

        // Find all weapons that need their data overriding.
        var targets = renderer.Data.Parts.Where(p => p.Name.Contains("Weapon")).ToList();
        //var portals = renderer.Data.Parts.Where(p => p.Name.Contains("Portal")).ToList();
        var used = new HashSet<ItemTweakData>(targets.Count);

        ItemTweakData GetRandomTweak()
        {
            for (int i = 0; i < 100; i++)
            {
                var selected = allTweaks.RandomElementByWeight(td => 1f + (td.ScaleX - 1f) * 0.25f);
                if (used.Contains(selected))
                    continue;

                used.Add(selected);
                return selected;
            }

            return allTweaks.RandomElement();
        }

        // Apply a random and unique sprite to each weapon in the attack.
        foreach (var target in targets)
        {
            var tweak = GetRandomTweak();
            var ov = tweak.Apply(renderer, target);

            ov.UserData = GilgameshWeapon;

            // Make the portal scale with weapon size...
            float weaponSize = ov.LocalScaleFactor.y;
            
            // Get the portal.
            var portal = target.Parent.Children.First(p => p.Name.Contains("Pivot")).Children[0];
            var ov2 = renderer.GetOverride(portal);

            // Apply scale.
            var finalScale = Mathf.Clamp(weaponSize + (weaponSize - 1f * 0.5f), 0.5f, 2.5f);
            ov2.LocalScaleFactor = new Vector2(finalScale, finalScale);
        }

        foreach (var portal in renderer.Data.Parts)
        {
            if (!portal.Name.Contains("Portal"))
                continue;

            var pos = portal.GetSnapshot(renderer).GetWorldPosition();
            FleckMaker.Static(pos, renderer.Map, FleckDefOf.PsycastAreaEffect, 0.2f);
        }
    }

    public override void PreRenderPart(in AnimPartSnapshot part, in AnimPartOverrideData overrideData, ref Mesh mesh, ref Matrix4x4 matrix, ref Material mat, ref MaterialPropertyBlock finalMpb)
    {
        base.PreRenderPart(part, overrideData, ref mesh, ref matrix, ref mat, ref finalMpb);

        if (part.PartName.Contains("Portal"))
        {
            var col = finalMpb.GetColor("_Color");

            if (col.a > 0f && overrideData.UserData == null && !part.PartName.Contains("Rope"))
            {
                overrideData.UserData = new object();
                var pos = part.GetWorldPosition();
                FleckMaker.Static(pos, part.Renderer.Map, FleckDefOf.PsycastAreaEffect, 0.05f);
            }

            col.a += -0.2f + Mathf.Sin(Time.time * 6f + part.Part.Index * 0.5f) * 0.2f;
            finalMpb.SetColor("_Color", col);
        }

        if (overrideData.UserData != GilgameshWeapon)
            return;
    }

    public override void PreRenderPawn(in AnimPartSnapshot part, ref Vector3 position, ref Rot4 rotation, Pawn pawn)
    {
        if (part.DataA < 0.5f)
            return;

        GrappleFlyer.DrawBoundTexture(pawn, position, Color.yellow);
    }
}