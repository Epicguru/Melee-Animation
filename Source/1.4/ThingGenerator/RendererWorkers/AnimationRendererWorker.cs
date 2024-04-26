using UnityEngine;
using Verse;

namespace AM.RendererWorkers;

public abstract class AnimationRendererWorker
{
    /// <summary>
    /// Called when the animation renderer is setting up, including when a new save has been loaded.
    /// </summary>
    public abstract void SetupRenderer(AnimRenderer renderer);

    /// <summary>
    /// Called every time the animation is sampled, can be used to modify the calculated transforms.
    /// </summary>
    /// <param name="part">The part that has just been sampled.</param>
    /// <param name="time">The time that has been sampled.</param>
    /// <param name="renderer">The renderer.</param>
    public virtual void PostProcessSnapshotPart(ref AnimPartSnapshot part, float time, AnimRenderer renderer)
    {
    }

    /// <summary>
    /// Called every frame just before an part is about to be rendered. Use this to modify any values.
    /// The <paramref name="part"/> cannot be modified here because the values from it have already been used to compute all other parameters.
    /// </summary>
    /// <param name="part">Information about the part this about to be rendered.</param>
    /// <param name="overrideData">The override data for this part. Read only for the same reason <paramref name="part"/> is.</param>
    /// <param name="mesh">The mesh that is about to be used to render with.</param>
    /// <param name="matrix">The transform of the part about to be rendered.</param>
    /// <param name="mat">The material (shader) that will be used to render.</param>
    /// <param name="finalMpb">The material property block that will be used to render with. May be null.</param>
    public virtual void PreRenderPart(in AnimPartSnapshot part, in AnimPartOverrideData overrideData, ref Mesh mesh, ref Matrix4x4 matrix, ref Material mat, ref MaterialPropertyBlock finalMpb)
    {
        
    }

    /// <summary>
    /// Called every frame for every animator, for debugging purposes.
    /// </summary>
    public virtual void DrawGUI(AnimRenderer renderer)
    {

    }

    /// <summary>
    /// Called every frame just before a pawn is rendered.
    /// </summary>
    public virtual void PreRenderPawn(in AnimPartSnapshot part, ref Vector3 position, ref Rot4 rotation, Pawn pawn)
    {

    }
}