using AAM;
using AAM.Patches;
using AAM.Workers;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

/// <summary>
/// An <see cref="AnimRenderer"/> is the object that represents and also draws (renders) a currently running animation.
/// Be sure to check the <see cref="IsDestroyed"/> property before interacting with an object of this type.
/// </summary>
public class AnimRenderer
{
    #region Static stuff

    public static readonly char[] Alphabet = new char[] { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H' };
    public static Material DefaultCutout, DefaultTransparent;
    public static IReadOnlyList<AnimRenderer> ActiveRenderers => activeRenderers;
    public static IReadOnlyCollection<Pawn> CapturedPawns => pawnToRenderer.Keys;
    public static int TotalCapturedPawnCount => pawnToRenderer.Count;

    private static List<AnimRenderer> activeRenderers = new List<AnimRenderer>();
    private static Dictionary<Pawn, AnimRenderer> pawnToRenderer = new Dictionary<Pawn, AnimRenderer>();
    private static Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
    private static bool isItterating;

    /// <summary>
    /// Tries to get the <see cref="AnimRenderer"/> that a pawn currently belongs to.
    /// Will return null if none is found.
    /// </summary>
    /// <returns>The renderer, or null. Null indicates that the pawn is not currently part of any animation.</returns>
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
            if (renderer.IsDestroyed)
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

            if (renderer.IsDestroyed)
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
            if (pawn == null || pawn.Destroyed)
                continue;

            var pos = renderer.GetPawnBody(pawn).GetSnapshot(renderer).GetWorldPosition();

            // Render pawn in custom position using patches.
            PreventDrawPatchUpper.AllowNext = true;
            PreventDrawPatch.AllowNext = true;
            MakePawnConsideredInvisible.IsRendering = true;
            pawn.Drawer.renderer.RenderPawnAt(pos, Rot4.West, true);
            Core.Log($"pawn y: {pos.y}");
            MakePawnConsideredInvisible.IsRendering = false;

            // Render shadow.
            // TODO cache or use patch.
            AccessTools.Method(typeof(PawnRenderer), "DrawInvisibleShadow").Invoke(pawn.Drawer.renderer, new object[] { pos });

            // Figure out where to draw the pawn label.
            Vector3 drawPos = pos;
            drawPos.z -= 0.6f;
            Vector2 vector = Find.Camera.WorldToScreenPoint(drawPos) / Prefs.UIScale;
            vector.y = UI.screenHeight - vector.y;
            labelDraw?.Invoke(pawn, vector);

            // TODO remove this awful way of making job and move to somewhere more sensible.

            // Create animation job for all pawns involved, if necessary.
            if (pawn.jobs != null && pawn.CurJobDef != AAM_DefOf.AAM_InAnimation && renderer.CurrentTime < renderer.Duration * 0.95f)
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
        renderer.IsDestroyed = true;
        if (!isItterating)
        {
            DestroyIntWorker(renderer);
        }
    }
    
    #endregion

    /// <summary>
    /// The active animation data. Animation data is loaded from disk upon request.
    /// Note that this is not the same as an animation def: see <see cref="Def"/> for the definition.
    /// </summary>
    public AnimData Data => Def.Data;
    /// <summary>
    /// The duration, in seconds, of the current animation.
    /// May not accurately represent how long the animation actually plays for, depending on the type of animation (i.e. Duels may last longer).
    /// </summary>
    public float Duration => Data?.Duration ?? 0;
    /// <summary>
    /// Same as the <see cref="Duration"/>, but expressed in Rimworld ticks.
    /// </summary>
    public int DurationTicks => Mathf.RoundToInt(Duration * 60);
    /// <summary>
    /// The active animation def.
    /// </summary>
    public readonly AnimDef Def;
    /// <summary>
    /// An array of pawns included in this animation.
    /// <b>DO NOT MODIFY THIS ARRAY!</b> It is for reading only.
    /// </summary>
    public Pawn[] Pawns = new Pawn[8];
    /// <summary>
    /// The base transform that the animation is centered on.
    /// Useful functions to modify this are <see cref="Matrix4x4.TRS(Vector3, Quaternion, Vector3)"/>
    /// and <see cref="Extensions.MakeAnimationMatrix(Pawn)"/>.
    /// </summary>
    public Matrix4x4 RootTransform = Matrix4x4.identity;
    /// <summary>
    /// The current <see cref="AnimSection"/> that is being played. May be null.
    /// </summary>
    public AnimSection CurrentSection { get; private set; }
    /// <summary>
    /// The <see cref="Map"/> that this animation is running on.
    /// </summary>
    public Map Map;
    /// <summary>
    /// The Unity Camera that this animation renders to. If null, all cameras are targeted.
    /// </summary>
    public Camera Camera;
    /// <summary>
    /// If true, the animation loops rather than ending.
    /// </summary>
    public bool Loop = false;
    /// <summary>
    /// If true, the animation is mirrored on this axis.
    /// </summary>
    public bool MirrorHorizontal, MirrorVertical;
    /// <summary>
    /// Will only be valid after the animation has already ended (see <see cref="IsDestroyed"/>).
    /// If true, the animator ended prematurely, such as by loosing a pawn or otherwise being ended unexpectedly.
    /// If false, the animator ended because the animation reached it's natural end.
    /// </summary>
    public bool WasInterrupted { get; private set; }
    /// <summary>
    /// If true, this animator has finished playing and is no longer active.
    /// You should not keep references to destroyed AnimRenderers.
    /// </summary>
    public bool IsDestroyed { get; private set; }
    /// <summary>
    /// The current time, in seconds, that the animation is playing.
    /// </summary>
    public float CurrentTime => time;
    /// <summary>
    /// Gets the number of non-null pawns in the <see cref="Pawns"/> array.
    /// </summary>
    public int PawnCount
    {
        get
        {
            int c = 0;
            foreach (var p in Pawns)
                if (p != null)
                    c++;
            return c;
        }
    }

    private AnimPartSnapshot[] snapshots;
    private AnimPartOverrideData[] overrides;
    internal List<(AnimEvent e, AnimEventWorker worker)> workersAtEnd = new List<(AnimEvent e, AnimEventWorker worker)>();
    private AnimPartData[] bodies = new AnimPartData[8];
    private MaterialPropertyBlock pb;
    private float time = -1;
    
    private AnimRenderer(AnimDef def)
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

    /// <summary>
    /// Gets the current state (snapshot) for a particular animation part.
    /// </summary>
    public AnimPartSnapshot GetSnapshot(AnimPartData part) => part == null ? default : snapshots[part.Index];
    
    /// <summary>
    /// Gets the override data based on the part index.
    /// See <see cref="GetOverride(AnimPartData)"/>.
    /// </summary>
    /// <param name="index">The part index, such as from <see cref="AnimPartData.Index"/>.</param>
    /// <returns>The override object, or null if the index is out of bounds.</returns>
    public AnimPartOverrideData GetOverride(int index) => index >= 0 && index < overrides.Length ? overrides[index] : null;

    /// <summary>
    /// Gets the override data of a particular part.
    /// </summary>
    /// <returns>The override object, or null if the part is null.</returns>
    public AnimPartOverrideData GetOverride(AnimPartData part) => GetOverride(part?.Index ?? -1);

    /// <summary>
    /// Gets the override data for a particular snapshot. The <see cref="AnimPartSnapshot.Part"/>
    /// object is used to find the override object.
    /// </summary>
    /// <returns>The override object, or null if the part is null.</returns>
    public AnimPartOverrideData GetOverride(in AnimPartSnapshot snapshot) => GetOverride(snapshot.Part);

    /// <summary>
    /// Causes this animation renderer to be registered with the system, and finish setting up.
    /// Note: this is not the preferred way of starting an animation.
    /// Instead, consider using <see cref="AnimationManager.StartAnimation(AnimDef, Matrix4x4, Pawn[])"/>.
    /// </summary>
    public bool Register()
    {
        Pawn invalid = GetFirstInvalidPawn();
        if (invalid != null)
        {
            Core.Error($"Tried to start animation with 1 or more invalid pawn: [{invalid.Faction?.Name ?? "No faction"}] {invalid.NameFullColored}");
            IsDestroyed = true;
            return false;
        }

        foreach(var pawn in Pawns)
        {
            var anim = TryGetAnimator(pawn);
            if(anim != null)
            {
                Core.Error($"Tried to start animation with '{pawn.LabelShortCap}' but that pawn is already in animation {anim.Data.Name}!");
                IsDestroyed = true;
                return false;
            }
        }

        if (PawnCount != Def.pawnCount)
            Core.Warn($"Started AnimRenderer with bad number of pawns! Expected {Def.pawnCount}, got {PawnCount}. (Def: {Def})");

        RegisterInt(this);
        Seek(0f, null);
        return true;
    }

    /// <summary>
    /// Cancels and destroys this animator, releasing it's pawns and stopping it from playing.
    /// This call may not immediately release pawns, it may be delayed until the end of the frame.
    /// </summary>
    public void Destroy()
    {
        if (IsDestroyed)
        {
            Core.Warn("Tried to destroy renderer that is already destroyed...");
            return;
        }

        DestroyInt(this);
    }

    /// <summary>
    /// Gets the <see cref="AnimPartData"/> associated with a pawn's body.
    /// This part data can then be used to get the current position of the body using <see cref="GetSnapshot(AnimPartData)"/>.
    /// </summary>
    /// <returns>The part data, or null if the pawn is null or is not captured by this animation.</returns>
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

    /// <summary>
    /// Should be called once per Rimworld tick.
    /// If this animation is managed automatically (such as when using the <see cref="AnimationManager"/> utility)
    /// then you do not need to call this manually, as it will be done for you.
    /// </summary>
    public void Tick()
    {
        if (IsDestroyed)
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

    /// <summary>
    /// Gets the first captured pawn that is in an invalid state.
    /// See <see cref="IsPawnValid(Pawn)"/> and <see cref="Pawns"/>.
    /// </summary>
    /// <returns>The first invalid pawn or null if all pawns are valid, or there are no pawns in this animation.</returns>
    public Pawn GetFirstInvalidPawn()
    {
        foreach(var pawn in Pawns)
        {
            if (pawn != null && !IsPawnValid(pawn))
                return pawn;
        }
        return null;
    }

    /// <summary>
    /// Is this pawn valid to be used in this animator?
    /// Checks simple conditions such as not dead, destroyed, downed...
    /// </summary>
    public virtual bool IsPawnValid(Pawn p)
    {
        return p is
        {
            Spawned: true,
            Dead: false,
            Downed: false
        };
    }

    public Vector2 Draw(float? atTime, float? dt)
    {
        if (IsDestroyed)
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

            var ov = GetOverride(snap);

            bool useMPB = ov.UseMPB;

            var color = snap.FinalColor;

            //if (useMPB)
            {
                pb.SetTexture("_MainTex", tex);
                pb.SetColor("_Color", color);
            }
            snap.Part.PreDraw(mat, useMPB ? pb : null);

            var matrix = RootTransform * snap.WorldMatrix;

            bool preFx = ov.FlipX ? !snap.FlipX : snap.FlipX;
            bool preFy = ov.FlipY ? !snap.FlipY : snap.FlipY;
            bool fx = MirrorHorizontal ? !preFx : preFx;
            bool fy = MirrorVertical ? !preFy : preFy;
            var mesh = AnimData.GetMesh(fx, fy);

            if (snap.PartName.Contains("Regular"))
                Core.Log($"Sword y: {snap.GetWorldPosition().y}");

            Graphics.DrawMesh(mesh, matrix, mat, 0, Camera, 0, true ? pb : null);
        }        

        return range;
    }

    /// <summary>
    /// Changes the current time of this animation.
    /// Depending on the parameters specified, this may act as a 'jump' (<paramref name="atTime"/>) or a continuous movement (<paramref name="dt"/>).
    /// </summary>
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
