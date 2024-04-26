using AM.Patches;
using UnityEngine;
using Verse;

namespace AM.Heads;

/// <summary>
/// A decapitated head instance.
/// For simplicity and performance reasons, these are not serialized.
/// </summary>
public sealed class HeadInstance
{
    public Pawn Pawn { get; set; }
    public Map Map { get; set; }
    public Vector3 Position { get; set; }
    public float Rotation { get; set; }
    public float TimeToLive { get; set; }
    public Rot4 Direction { get; set; }

    public bool Render()
    {
        if (Pawn is not { Dead: true })
        {
            return false;
        }

        TimeToLive -= Time.unscaledDeltaTime;
        if (TimeToLive <= 0f)
            return false;

        // Do not actually render if the map is not currently visible.
        if (Map != Find.CurrentMap)
            return true;

        //Render pawn in custom position using patches.
        Patch_PawnRenderer_RenderPawnAt.NextDrawMode = Patch_PawnRenderer_RenderPawnAt.DrawMode.HeadStandalone;
        Patch_PawnRenderer_RenderPawnAt.HeadRotation = Direction;
        Patch_PawnRenderer_RenderPawnAt.StandaloneHeadAngle = Rotation;
        Patch_PawnRenderer_RenderPawnAt.StandaloneHeadPosition = Position;
        Patch_PawnRenderer_RenderPawnAt.AllowNext = true;
        Patch_PawnRenderer_DrawShadowInternal.Suppress = true; // In 1.4 shadow rendering is baked into RenderPawnAt and may need to be prevented.

        try
        {
            AnimRenderer.PrePawnSpecialRender?.Invoke(Pawn, null, Map);

            Pawn.Drawer.renderer.RenderPawnAt(Position, Direction, true);

            AnimRenderer.PostPawnSpecialRender?.Invoke(Pawn, null, Map);
        }
        finally
        {
            Patch_PawnRenderer_RenderPawnAt.NextDrawMode = Patch_PawnRenderer_RenderPawnAt.DrawMode.Full;
            Patch_PawnRenderer_DrawShadowInternal.Suppress = false;
        }
        return true;
    }
}