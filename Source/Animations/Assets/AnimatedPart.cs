using Assets.Editor;
using UnityEngine;
using static AnimData;

[ExecuteInEditMode]
[ExecuteAlways]
public class AnimatedPart : MonoBehaviour
{
    public bool HasCustomName => !string.IsNullOrWhiteSpace(CustomName);
    public bool HasTexturePath => !DoNotIncludeTexture && !string.IsNullOrWhiteSpace(TexturePath);

    [Header("Info")]
    public string CustomName;
    public string TexturePath;
    public bool DoNotIncludeTexture = false;

    [Header("Adaptive Options")]
    public Vector2 IdleOffset;
    [Range(-360, 360f)]
    public float IdleRotation;
    public Vector2 IdleScale = new Vector2(1f, 1f);
    public bool IdleFlipX, IdleFlipY;
    public bool TransparentByDefault;

    [Header("Animated")]
    public Color Tint = Color.white;
    public float DataA, DataB, DataC;
    public bool FlipX, FlipY;
    public int FrameIndex;

    [Header("Other")]
    public AnimatedPart SplitDrawPivot;
    public SplitDrawMode SplitDrawMode;

    private string relativePath;
    private AnimationDataCreator creator;

    private void LateUpdate()
    {
        if (creator == null)
            creator = GetComponentInParent<AnimationDataCreator>();

        if (creator == null)
            return;

        relativePath ??= AnimData.MakeRelativePath(GetComponentInParent<Animator>().gameObject, this.gameObject);
        bool dontDraw = false;
        string name = !string.IsNullOrWhiteSpace(CustomName) ? CustomName : relativePath; 
        var tex = AnimationDataCreator.Instance?.ResolveTexture(name, TexturePath, FrameIndex, out dontDraw);
        if (tex == null || dontDraw)
            return;

        var trs = MakeTrs(out bool fx, out bool fy);
        var mesh = GetMesh(fx, fy);

        // pre * root * localAdjust * post
        creator.PushToDraw(new AnimationDataCreator.DrawItem
        {
            Matrix = trs,
            Color = Tint,
            Mesh = mesh,
            Texture = tex,
        });
    }

    public Matrix4x4 MakeTrs(out bool fx, out bool fy)
    {
        if (creator == null)
            creator = GetComponentInParent<AnimationDataCreator>();

        var trs = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        fx = FlipX;
        fy = FlipY;
        if (IdleFlipX)
            fx = !fx;
        if (IdleFlipY)
            fy = !fy;

        bool negRot = fy ^ fx;
        Vector2 off = new Vector2(fx ? -IdleOffset.x : IdleOffset.x, fy ? -IdleOffset.y : IdleOffset.y);
        float offRot = negRot ? -IdleRotation : IdleRotation;
        trs *= Matrix4x4.TRS(new Vector3(off.x, 0f, off.y), Quaternion.Euler(0, offRot, 0f), new Vector3(IdleScale.x, 1f, IdleScale.y));

        fx = creator.MirrorHorizontal ? !fx : fx;
        fy = creator.MirrorVertical ? !fy : fy;

        // No idea why I wrote this.
        // Not touching it because it works.
        var preProc  = Matrix4x4.Scale(new Vector3(creator.MirrorHorizontal ? -1f : 1f, 1f, creator.MirrorVertical ? -1f : 1f));
        var postProc = Matrix4x4.Scale(new Vector3(creator.MirrorHorizontal ? -1f : 1f, 1f, creator.MirrorVertical ? -1f : 1f));

        trs = preProc * trs * postProc;

        return trs;
    }

    public void OnDrawGizmos()
    {
        if (SplitDrawPivot == null)
            return;

        Gizmos.color = new Color(0, 1, 0, 0.5f);
        Gizmos.DrawCube(SplitDrawPivot.transform.position, Vector3.one * 0.07f);
    }
}
