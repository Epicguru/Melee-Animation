using AAM;
using AAM.Workers;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

public class AnimRenderer
{
    #region Static stuff

    public static readonly char[] Alphabet = new char[] { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H' };
    public static Material DefaultCutout, DefaultTransparent;
    public static IReadOnlyList<AnimRenderer> ActiveRenderers => activeRenderers;
    public static IReadOnlyCollection<Pawn> CapturedPawns => pawnToRenderer.Keys;
    public static int CapturedPawnCount => pawnToRenderer.Count;

    private static List<AnimRenderer> activeRenderers = new List<AnimRenderer>();
    private static Dictionary<Pawn, AnimRenderer> pawnToRenderer = new Dictionary<Pawn, AnimRenderer>();
    private static Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
    private static bool isItterating;

    public static AnimRenderer TryGetAnimator(Pawn pawn)
    {
        if (pawn == null)
            return null;

        if (pawnToRenderer.TryGetValue(pawn, out var found))
            return found;
        return null;
    }

    public static Texture2D ResolveTexture(AnimPartSnapshot snapshot)
    {
        if (snapshot.Renderer == null)
            return null;

        var ov = snapshot.Renderer.overrides[snapshot.Part.Index];

        if (ov.Texture != null)
            return ov.Texture;

        if (snapshot.TexturePath == null)
            return null;

        return ResolveTexture(snapshot.TexturePath);
    }

    public static Texture2D ResolveTexture(string texturePath)
    {
        if (textureCache.TryGetValue(texturePath, out var found))
            return found;

        Texture2D loaded;
        loaded = ContentFinder<Texture2D>.Get(texturePath, false);
        textureCache.Add(texturePath, loaded);
        if (loaded == null)
            Debug.LogError($"Failed to load texture '{texturePath}'.");

        return loaded;
    }

    public static void UpdateAll()
    {
        isItterating = true;
        for (int i = 0; i < activeRenderers.Count; i++)
        {
            var renderer = activeRenderers[i];
            renderer.Tick();
            if (renderer.Destroyed)
            {
                DestroyIntWorker(renderer);
                i--;
                continue;
            }
        }
        isItterating = false;
    }

    public static void DrawAll(float dt, Map map, Action<Pawn, Vector2> labelDraw = null)
    {
        isItterating = true;
        for(int i = 0; i < activeRenderers.Count; i++)
        {
            var renderer = activeRenderers[i];

            if (renderer.Map != map)
                continue;

            if (renderer.Destroyed)
            {
                DestroyIntWorker(renderer);
                i--;
                continue;
            }

            try
            {
                DrawSingle(renderer, dt, labelDraw);
            }
            catch(Exception e)
            {
                Core.Error($"Exception rendering animator '{renderer}'", e);
            }

        }
        isItterating = false;
    }

    private static void DrawSingle(AnimRenderer renderer, float dt, Action<Pawn, Vector2> labelDraw = null)
    {
        // Draw and handle events.
        var timePeriod = renderer.Draw(null, dt);
        foreach (var e in renderer.GetEventsInPeriod(timePeriod))
        {
            try
            {
                EventHelper.Handle(e, renderer);
            }
            catch (Exception ex)
            {
                Core.Error($"{ex.GetType().Name} when handling animation event '{e}':", ex);
            }
        }

        int madeJobCount = 0;

        foreach (var pawn in renderer.Pawns)
        {
            if (pawn == null)
                continue;

            var pos = renderer.GetPawnBody(pawn).GetSnapshot(renderer).GetWorldPosition();

            // Render pawn in custom position using patches.
            PreventDrawPatch.AllowNext = true;
            MakePawnConsideredInvisible.IsRendering = true;
            pawn.Drawer.renderer.RenderPawnAt(pos, Rot4.West, true);
            MakePawnConsideredInvisible.IsRendering = false;

            // Render shadow.
            AccessTools.Method(typeof(PawnRenderer), "DrawInvisibleShadow").Invoke(pawn.Drawer.renderer, new object[] { pos });

            // Figure out where to draw the pawn label.
            Vector3 drawPos = pos;
            drawPos.z -= 0.6f;
            Vector2 vector = Find.Camera.WorldToScreenPoint(drawPos) / Prefs.UIScale;
            vector.y = UI.screenHeight - vector.y;
            labelDraw?.Invoke(pawn, vector);

            // Create animation job for all pawns involved, if necessary.
            if (pawn.jobs != null && pawn.jobs.curJob != null && pawn.jobs.curJob.def != AAM_DefOf.AAM_InAnimation && renderer.CurrentTime < renderer.Duration * 0.95f)
            {
                var newJob = JobMaker.MakeJob(AAM_DefOf.AAM_InAnimation);
                
                newJob.collideWithPawns = true;
                newJob.playerForced = true;
                if (pawn.verbTracker?.AllVerbs != null)
                    foreach (var verb in pawn.verbTracker.AllVerbs)
                        verb.Reset();


                if (pawn.equipment?.AllEquipmentVerbs != null)
                    foreach (var verb in pawn.equipment.AllEquipmentVerbs)
                        verb.Reset();

                pawn.jobs.StartJob(newJob, JobCondition.InterruptForced, null, false, true, null);
                madeJobCount++;
            }
        }

        if (madeJobCount > 0)
        {
            renderer.OnStart();
        }
    }

    private static void DestroyIntWorker(AnimRenderer renderer)
    {
        renderer.WasInterrupted = renderer.CurrentTime < 0.999f;        

        if (renderer.Pawns != null)
        {
            foreach (var pawn in renderer.Pawns)
            {
                if (pawn != null)
                    pawnToRenderer.Remove(pawn);
            }
        }
        
        activeRenderers.Remove(renderer);

        try
        {
            renderer.OnEnd();
        }
        catch (Exception e)
        {
            Core.Error("Exception in AnimRenderer.OnEnd:", e);
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

    private static void DestroyInt(AnimRenderer renderer)
    {
        renderer.Destroyed = true;
        if (!isItterating)
        {
            DestroyIntWorker(renderer);
        }
    }
    
    #endregion

    public AnimData Data => Def.Data;
    public float Duration => Data?.Duration ?? 0;
    public int DurationTicks => Mathf.RoundToInt(Duration * 60);
    public readonly AnimDef Def;
    public Pawn[] Pawns = new Pawn[8];
    public Matrix4x4 RootTransform = Matrix4x4.identity;
    public AnimSection CurrentSection { get; private set; }
    public Map Map;
    public Camera Camera;
    public bool Loop = false;
    public bool MirrorHorizontal, MirrorVertical;
    public bool WasInterrupted { get; private set; }
    public bool Destroyed { get; private set; }
    public float CurrentTime => time;

    private AnimPartSnapshot[] snapshots;
    private AnimPartOverrideData[] overrides;
    internal List<(AnimEvent e, AnimEventWorker worker)> workersAtEnd = new List<(AnimEvent e, AnimEventWorker worker)>();
    private AnimPartData[] bodies = new AnimPartData[8];
    private MaterialPropertyBlock pb;
    private float time = -1;

    public AnimRenderer(AnimDef def)
    {
        Def = def;
        snapshots = new AnimPartSnapshot[Data.Parts.Count];
        overrides = new AnimPartOverrideData[Data.Parts.Count];
        pb = new MaterialPropertyBlock();

        for (int i = 0; i < overrides.Length; i++)        
            overrides[i] = new AnimPartOverrideData();
    }

    public AnimRenderer(AnimDef def, Map map) : this(def)
    {        
        Map = map;
    }

    public AnimPartSnapshot GetSnapshot(AnimPartData part)
    {
        return snapshots[part.Index];
    }

    public AnimPartOverrideData GetOverride(int index) => index >= 0 && index < overrides.Length ? overrides[index] : null;

    public AnimPartOverrideData GetOverride(AnimPartData part) => GetOverride(part?.Index ?? -1);

    public AnimPartOverrideData GetOverride(in AnimPartSnapshot snapshot) => GetOverride(snapshot.Part);

    public void Register()
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

        RegisterInt(this);
        Seek(0f, null);
    }

    public void Destroy()
    {
        if (Destroyed)
        {
            Core.Warn("Tried to destroy renderer that is already destroyed...");
            return;
        }

        DestroyInt(this);
    }

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

                bodies[i] = GetPart($"Body{Alphabet[i]}");
                return bodies[i];
            }
        }
        return null;
    }

    public void Tick()
    {
        if (Destroyed)
            return;

        if(Map == null || (Find.TickManager.TicksAbs % 120 == 0 && Map.Index < 0))
        {
            Destroy();
            return;
        }

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

    public Vector2 Draw(float? atTime, float? dt)
    {
        if (Destroyed)
            return Vector2.zero;

        var range = Seek(atTime, dt);

        foreach (var snap in snapshots)
        {
            if (!ShouldDraw(snap))
                continue;

            var tex = ResolveTexture(snap);
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

            var ov = GetOverride(snap);

            bool preFx = ov.FlipX ? !snap.FlipX : snap.FlipX;
            bool preFy = ov.FlipY ? !snap.FlipY : snap.FlipY;
            bool fx = MirrorHorizontal ? !preFx : preFx;
            bool fy = MirrorVertical ? !preFy : preFy;
            var mesh = AnimData.GetMesh(fx, fy);

            Graphics.DrawMesh(mesh, matrix, mat, 0, Camera, 0, null);
        }        

        return range;
    }

    public Vector2 Seek(float? atTime, float? dt, bool generateSectionEvents = true)
    {
        float t = atTime ?? (this.time + dt.Value);
        var range = SeekInt(t, MirrorHorizontal, MirrorVertical, generateSectionEvents);

        if (t > Data.Duration)
        {
            if (Loop)
                this.time = 0;
            else
                Destroy();
        }
        return range;
    }

    private Vector2 SeekInt(float time, bool mirrorX = false, bool mirrorY = false, bool generateSectionEvents = true)
    {
        time = Mathf.Clamp(time, 0f, Duration);

        if (this.time == time)
            return new Vector2(-1, -1);

        // Pass 1: Evaluate curves, make local matrices.
        for (int i = 0; i < Data.Parts.Count; i++)
            snapshots[i] = new AnimPartSnapshot(Data.Parts[i], this, time);

        // Pass 2: Resolve world matrices using inheritance tree.
        for (int i = 0; i < Data.Parts.Count; i++)
            snapshots[i].UpdateWorldMatrix(mirrorX, mirrorY);

        // Sort by depth if necessary.
        // TODO
        //if (sortByDepth)
        //    SortByDepth();

        float start = Mathf.Min(this.time, time);
        float end = Mathf.Max(this.time, time);
        this.time = time;

        if (CurrentSection == null)
        {
            CurrentSection = Data.GetSectionAtTime(time);
            CurrentSection.OnSectionEnter(this);
        }
        else if (!CurrentSection.ContainsTime(time))
        {
            var old = CurrentSection;
            CurrentSection = Data.GetSectionAtTime(time);

            if (generateSectionEvents)
            {
                old.OnSectionExit(this);
                CurrentSection.OnSectionEnter(this);
            }
        }

        return new Vector2(start, end);
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

            var offset = Def.TryGetCell(AnimCellData.Type.PawnStart, MirrorHorizontal, MirrorVertical, i) ?? IntVec2.Zero;
            TeleportPawn(pawn, basePos + offset.ToIntVec3);
        }
    }

    public void OnEnd()
    {
        // Do not teleport to end if animation was interrupted.
        if (WasInterrupted)
            return;

        IntVec3 basePos = RootTransform.MultiplyPoint3x4(Vector3.zero).ToIntVec3();
        basePos.y = 0;

        for (int i = 0; i < Pawns.Length; i++)
        {
            var pawn = Pawns[i];
            if (pawn == null)
                continue;

            var posData = Def.TryGetCell(AnimCellData.Type.PawnEnd, MirrorHorizontal, MirrorVertical, i);
            if (posData == null)
                continue;

            TeleportPawn(pawn, basePos + posData.Value.ToIntVec3);
            pawn?.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }

        foreach(var item in workersAtEnd)
        {
            try
            {
                item.worker.Run(new AnimEventInput(item.e, this, false, null));
            }
            catch(Exception e)
            {
                Core.Error("Error running end worker:", e);
            }
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
        return snapshot.Active && !GetOverride(snapshot).PreventDraw && snapshot.FinalColor.a > 0;
    }

    protected virtual Material GetMaterialFor(in AnimPartSnapshot snapshot)
    {
        var ovMat = GetOverride(snapshot).Material;
        if (ovMat != null)        
            return ovMat;        

        if (snapshot.FinalColor.a < 1f)        
            return DefaultTransparent;        

        return DefaultCutout;
    }
}
