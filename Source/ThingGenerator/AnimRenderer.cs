using AAM;
using AAM.Workers;
using System.Collections.Generic;
using UnityEngine;
#if !UNITY_EDITOR
using Verse;
#endif

public class AnimRenderer
{
    public static Material DefaultCutout, DefaultTransparent;
    public static IReadOnlyList<AnimRenderer> ActiveRenderers => activeRenderers;

    private static List<AnimRenderer> activeRenderers = new List<AnimRenderer>();
    private static char[] indexToAlpha = new char[] { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H' };
#if !UNITY_EDITOR
    private static Dictionary<Pawn, AnimRenderer> pawnToRenderer = new Dictionary<Pawn, AnimRenderer>();

    public static AnimRenderer TryGetAnimator(Pawn pawn)
    {
        if (pawn == null)
            return null;

        if (pawnToRenderer.TryGetValue(pawn, out var found))
            return found;
        return null;
    }

    public static void Update()
    {
        for (int i = 0; i < activeRenderers.Count; i++)
        {
            var renderer = activeRenderers[i];
            renderer.Tick();
            if (renderer.Destroyed)
            {
                renderer.WasInterrupted = renderer.Data.CurrentTime < 0.999f;
                renderer.OnEnd();

                foreach (var pawn in renderer.Pawns)
                {
                    if (pawn != null)
                        pawnToRenderer.Remove(pawn);
                }

                renderer.bodies = null;
                renderer.Pawns = null;
                activeRenderers.RemoveAt(i);
                i--;
                continue;
            }
        }
    }

    private static void RegisterInt(AnimRenderer renderer)
    {
        if (!activeRenderers.Contains(renderer))
        {
            activeRenderers.Add(renderer);
            foreach (var item in renderer.Pawns)
            {
                if (item != null)
                    pawnToRenderer.Add(item, renderer);
            }

            renderer.OnStart();
        }
    }
#endif

    private static void DestroyInt(AnimRenderer renderer)
    {
        renderer.Destroyed = true;
    }

    public readonly AnimData Data;
    public bool MirrorHorizontal, MirrorVertical;
#if !UNITY_EDITOR
    public Pawn[] Pawns = new Pawn[8];
#endif
    public bool Destroyed { get; private set; }
    public Matrix4x4 RootTransform = Matrix4x4.identity;
    public bool AutoHandled { get; private set; }
    public bool Loop = false;
    public float Duration => Data?.Duration ?? 0;
    public int DurationTicks => Mathf.RoundToInt(Duration * 60);
    public bool WasInterrupted { get; private set; }

    internal List<(AnimEvent e, AnimEventWorker worker)> workersAtEnd = new List<(AnimEvent e, AnimEventWorker worker)>();
    private AnimPartData[] bodies = new AnimPartData[8];
    private MaterialPropertyBlock pb;
    private float time;
    private bool destroyPending;

    public AnimRenderer(AnimData data)
    {
        Data = data;
        pb = new MaterialPropertyBlock();
    }

#if !UNITY_EDITOR
    public void Register(bool autoHandle = true)
    {
        Pawn invalid = GetFirstInvalidPawn();
        if (invalid != null)
        {
            Core.Error($"Tried to start animation with 1 or more invalid pawn: [{invalid.Faction?.Name ?? "No faction"}] {invalid.NameFullColored}");
            return;
        }

        foreach(var pawn in Pawns)
        {
            var anim = TryGetAnimator(pawn);
            if(anim != null)
            {
                Core.Error($"Tried to start animation with '{pawn.LabelShortCap}' but that pawn is already in animation {anim.Data.Name}!");
                return;
            }
        }

        this.AutoHandled = autoHandle;
        RegisterInt(this);
    }
#endif

    public void Destroy()
    {
        if (destroyPending)
            return;

        destroyPending = true;
        DestroyInt(this);
    }

#if !UNITY_EDITOR
    public AnimPartData GetPawnBody(Pawn pawn)
    {
        if (pawn == null)
            return null;
        for (int i = 0; i < Pawns.Length; i++)
        {
            if (Pawns[i] == pawn)
            {
                var b = bodies[i];
                if (b != null)
                    return b;

                bodies[i] = GetPart($"Body{indexToAlpha[i]}");
                return bodies[i];
            }
        }
        return null;
    }

    public void Tick()
    {
        if (Destroyed)
            return;

        if (GetFirstInvalidPawn() != null)
        {
            Destroy();
        }
    }

    public Pawn GetFirstInvalidPawn()
    {
        foreach(var pawn in Pawns)
        {
            if (pawn != null && !IsPawnValid(pawn))
                return pawn;
        }
        return null;
    }

    public virtual bool IsPawnValid(Pawn p)
    {
        return !p.Destroyed && p.Spawned && !p.Dead && !p.Downed;
    }
#endif

    public Vector2 Draw(float? atTime, float? dt)
    {
        if (Destroyed)
            return Vector2.zero;        

        foreach (var snap in Data.CurrentSnapshots)
        {
            if (!ShouldDraw(snap))
                continue;

            var tex = snap.Part.Texture;
            if (tex == null)
                continue;

            var mat = GetMaterialFor(snap);
            if (mat == null)
                continue;

            var color = snap.FinalColor;

            pb.SetTexture("_MainTex", tex);
            pb.SetColor("_Color", color);
            snap.Part.PreDraw(mat, pb);

            var matrix = RootTransform * snap.WorldMatrix;

            bool fx = MirrorHorizontal ? !snap.FlipX : snap.FlipX;
            bool fy = MirrorVertical ? !snap.FlipY : snap.FlipY;
            var mesh = AnimData.GetMesh(fx, fy);

            Graphics.DrawMesh(mesh, matrix, mat, 0, null, 0, pb);
        }        

        return Seek(atTime, dt);
    }

    public Vector2 Seek(float? atTime, float? dt, bool generateSectionEvents = true)
    {
        float time = atTime ?? (this.time += dt.Value);
        this.time = time;
        var range = Data.Seek(time, this, true, MirrorHorizontal, MirrorVertical, generateSectionEvents);

        if (time > Data.Duration)
        {
            if (Loop)
                this.time = 0;
            else
                Destroy();
        }
        return range;
    }

    public void OnStart()
    {
        IntVec3 basePos = RootTransform.MultiplyPoint3x4(Vector3.zero).ToIntVec3();
        basePos.y = 0;

        for (int i = 0; i < Pawns.Length; i++)
        {
            var pawn = Pawns[i];
            if (pawn == null)
                continue;

            var posData = Data.GetPawnCells(i, MirrorHorizontal, MirrorVertical);
            TeleportPawn(pawn, basePos + new IntVec3(posData.x, 0, posData.z));
        }
    }

    public void OnEnd()
    {
        // Do not teleport to end if animation was interrupted.
        if (WasInterrupted)
            return;

        IntVec3 basePos = RootTransform.MultiplyPoint3x4(Vector3.zero).ToIntVec3();
        basePos.y = 0;
        Core.Log($"Base pos: {basePos}");

        for (int i = 0; i < Pawns.Length; i++)
        {
            var pawn = Pawns[i];
            if (pawn == null)
                continue;

            var posData = Data.GetPawnCells(i, MirrorHorizontal, MirrorVertical);
            TeleportPawn(pawn, basePos + new IntVec3(posData.x2, 0, posData.z2));
            pawn?.jobs.EndCurrentJob(Verse.AI.JobCondition.InterruptForced);
        }

        foreach(var item in workersAtEnd)
        {
            Core.Log($"Run worker: {item.worker.GetType().FullName}");
            item.worker.Run(new AnimEventInput(item.e, this, false, null));
        }
        workersAtEnd.Clear();
    }

    protected virtual void TeleportPawn(Pawn pawn, IntVec3 pos)
    {
        if (pawn == null)
            return;

        pawn.Position = pos;
        pawn.pather?.StopDead();
        pawn.pather?.Notify_Teleported_Int();
        pawn.Drawer?.tweener?.ResetTweenedPosToRoot();
    }

    public IEnumerable<AnimEvent> GetEventsInPeriod(Vector2 timePeriod)
    {
        return Data.GetEventsInPeriod(timePeriod);
    }

    public AnimPartData GetPart(string name) => Data.GetPart(name);

    protected virtual bool ShouldDraw(in AnimPartSnapshot snapshot)
    {
        // TODO: Check scale too?
        return !snapshot.Part.OverrideData.PreventDraw && snapshot.FinalColor.a > 0;
    }

    protected virtual Material GetMaterialFor(in AnimPartSnapshot snapshot)
    {
        if (snapshot.Part.OverrideData.Material != null)
            return snapshot.Part.OverrideData.Material;

        if (snapshot.FinalColor.a < 1f)
        {
            return DefaultTransparent;
        }

        return DefaultCutout;
    }
}
