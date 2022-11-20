using AAM.Grappling;
using AAM.Tweaks;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace AAM.RendererWorkers;

public class GilgameshRendererWorker : AnimationRendererWorker
{
    public static readonly object GilgameshWeapon = new object();

    public override void SetupRenderer(AnimRenderer renderer)
    {
        // Get all supported long stabbing weapon tweak data.
        // This is used to ensure correct rendering during the attack.
        var allTweaks = DefDatabase<ThingDef>.AllDefsListForReading.Where(td => td.IsMeleeWeapon).Select(TweakDataManager.TryGetTweak).Where(tweak => tweak != null && tweak.MeleeWeaponType.HasFlag(MeleeWeaponType.Long_Stab) && string.IsNullOrEmpty(tweak.CustomRendererClass));
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

        //foreach (var portal in renderer.Data.Parts)
        //{
        //    if (!portal.Name.Contains("Portal"))
        //        continue;

        //    var pos = portal.GetSnapshot(renderer).GetWorldPosition();
        //    FleckMaker.Static(pos, renderer.Map, FleckDefOf.PsycastAreaEffect, 0.2f);

        //}

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

    public override void DrawGUI(AnimRenderer renderer)
    {
        base.DrawGUI(renderer);

        return;

        foreach (var part in renderer.Data.Parts)
        {
            var data = renderer.GetSnapshot(part);
            var overrideData = renderer.GetOverride(data);
            if (overrideData?.UserData != GilgameshWeapon)
                continue;

            var matrix = renderer.RootTransform * data.WorldMatrix;
            float textureRot = overrideData.LocalRotation * Mathf.Deg2Rad;

            float size = matrix.lossyScale.x;
            // The total length of the weapon sprite, in world units (1 unit = 1 cell).
            float length = size * AnimRenderer.Remap(Mathf.Abs(Mathf.Sin(textureRot * 2f)), 0, 1f, 1f, 1.41421356237f); // sqrt(2)
                                                                                                           //Vector2 renderedPos = matrix.MultiplyPoint3x4(Vector3.zero).ToFlat();

            //float startX = renderedPos.x - length * 0.5f;
            //float endX = renderedPos.x + length * 0.5f;
            //float baseX = renderedPos.x - overrideData.LocalOffset.x;
            //float lerp = Mathf.InverseLerp(startX, endX, 70.5f);
            //Vector2 basePos = data.GetWorldPositionNoOverride().ToFlat();

            Matrix4x4 noOverrideMat = data.Renderer.RootTransform * data.WorldMatrixNoOverride * Matrix4x4.Scale(overrideData.LocalScaleFactor.ToWorld());

            Vector2 renderedPos = matrix.MultiplyPoint3x4(Vector3.zero).ToFlat();
            Vector2 startPos = renderedPos - length * 0.5f * noOverrideMat.MultiplyVector(Vector3.right).normalized.ToFlat();
            Vector2 endPos = renderedPos + length * 0.5f * noOverrideMat.MultiplyVector(Vector3.right).normalized.ToFlat();
            Vector2 basePos = renderer.GetSnapshot(part.SplitDrawPivot).GetWorldPosition().ToFlat();

            var ap = basePos - startPos;
            var ab = endPos - startPos;
            float lerp = Vector2.Dot(ap, ab) / Vector2.Dot(ab, ab);
            if (!data.Renderer.MirrorHorizontal)
                lerp = 1 - lerp;

            GenMapUI.DrawText(renderedPos, $"{part.Name} -> {lerp:F3}", Color.white);
            GenMapUI.DrawText(startPos, $"*", Color.green);
            GenMapUI.DrawText(endPos, $"*", Color.red);
            GenMapUI.DrawText(basePos, $"*", Color.black);
            GenMapUI.DrawText(Vector2.Lerp(startPos, endPos, 1f - lerp), $"*", Color.magenta);
        }
    }

    public override void PreRenderPawn(in AnimPartSnapshot part, ref Vector3 position, ref Rot4 rotation, Pawn pawn)
    {
        if (part.DataA < 0.5f)
            return;

        GrappleFlyer.DrawBoundTexture(pawn, position, Color.yellow);
    }
}