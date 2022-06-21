using AAM;
using AAM.Events;
using AAM.Events.Workers;
using AAM.Patches;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AAM.Sweep;
using AAM.Tweaks;
using UnityEngine;
using Verse;
using Verse.AI;
using Debug = UnityEngine.Debug;
using Color = UnityEngine.Color;

/// <summary>
/// An <see cref="AnimRenderer"/> is the object that represents and also draws (renders) a currently running animation.
/// Be sure to check the <see cref="IsDestroyed"/> property before interacting with an object of this type.
/// </summary>
public class AnimRenderer : IExposable
{
    #region Static stuff

    public static readonly Stopwatch SeekTimer = new Stopwatch();
    public static readonly Stopwatch DrawTimer = new Stopwatch();
    public static readonly Stopwatch EventsTimer = new Stopwatch();
    public static readonly char[] Alphabet = new char[] { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H' };
    public static Material DefaultCutout, DefaultTransparent;
    public static IReadOnlyList<AnimRenderer> ActiveRenderers => activeRenderers;
    public static IReadOnlyCollection<Pawn> CapturedPawns => pawnToRenderer.Keys;
    public static int TotalCapturedPawnCount => pawnToRenderer.Count;
    public static List<AnimRenderer> PostLoadPendingAnimators = new List<AnimRenderer>();

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

    public static void ClearAll()
    {
        activeRenderers.Clear();
        pawnToRenderer.Clear();
        PostLoadPendingAnimators.Clear();
        isItterating = false;
    }

    public static void ResetTimers()
    {
        SeekTimer.Reset();
        DrawTimer.Reset();
        EventsTimer.Reset();
    }

    public static void TickAll()
    {
        AddFromPostLoad();

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
        AddFromPostLoad();

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

    private static void AddFromPostLoad()
    {
        if (PostLoadPendingAnimators.Count == 0)
            return;

        foreach (var item in PostLoadPendingAnimators)
        {
            if (item == null)
                continue;
            item.ApplyPostLoad();
            item.Register();
        }

        Core.Log($"Post-Load Init: Registered {PostLoadPendingAnimators.Count} pending animators.");
        PostLoadPendingAnimators.Clear();
    }

    private static void DrawSingle(AnimRenderer renderer, float dt, Action<Pawn, Vector2> labelDraw = null)
    {
        // Draw and handle events.
        // TODO handle exceptions.
        var timePeriod = renderer.Draw(null, dt, labelDraw);

        EventsTimer.Start();
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
        EventsTimer.Stop();
    }

    private static void DestroyIntWorker(AnimRenderer renderer)
    {
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
    public AnimDef Def;
    /// <summary>
    /// A list of pawns included in this animation.
    /// </summary>
    public List<Pawn> Pawns = new List<Pawn>();
    /// <summary>
    /// The base transform that the animation is centered on.
    /// Useful functions to modify this are <see cref="Matrix4x4.TRS(Vector3, Quaternion, Vector3)"/>
    /// and <see cref="Extensions.MakeAnimationMatrix(Pawn)"/>.
    /// </summary>
    public Matrix4x4 RootTransform = Matrix4x4.identity;
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
    /// <summary>
    /// The outcome of this animation if it is an execution animation.
    /// </summary>
    public ExecutionOutcome ExecutionOutcome = ExecutionOutcome.Nothing;

    private AnimPartSnapshot[] snapshots;
    private AnimPartOverrideData[] overrides;
    private AnimPartData[] bodies = new AnimPartData[8]; // TODO REPLACE WITH LIST OR DICT.
    private PartWithSweep[] sweeps;
    private MaterialPropertyBlock pb;
    private float time = -1;
    private Pawn[] pawnsForPostLoad;
    private bool hasStarted;

    public AnimRenderer()
    {
        
    }

    public AnimRenderer(AnimDef def, Map map)
    {        
        Map = map;
        Def = def;
        Init();
    }

    private void Init()
    {
        if (snapshots != null)
        {
            Core.Error("Init called multiple times!");
            return;
        }

        snapshots = new AnimPartSnapshot[Data.Parts.Count];
        overrides = new AnimPartOverrideData[Data.Parts.Count];
        pb = new MaterialPropertyBlock();

        for (int i = 0; i < overrides.Length; i++)
            overrides[i] = new AnimPartOverrideData();

        // Create sweep meshes.
        int j = 0;
        sweeps = new PartWithSweep[Data.SweepDataCount];
        foreach (var part in Data.PartsWithSweepData)
        {
            var paths = Data.GetSweepPaths(part);
            foreach (var path in paths)
            {
                sweeps[j++] = new PartWithSweep(this, part, path, new());
            }
        }
    }

    public void ExposeData()
    {
        Scribe_Defs.Look(ref Def, "def");
        Scribe_Values.Look(ref time, "time");
        Scribe_Values.Look(ref MirrorHorizontal, "mirrorX");
        Scribe_Values.Look(ref MirrorVertical, "mirrorY");
        Scribe_Values.Look(ref Loop, "loop");
        Scribe_Values.Look(ref hasStarted, "hasStarted");
        Scribe_Collections.Look(ref Pawns, "pawns", LookMode.Reference);
        Scribe_References.Look(ref Map, "map");

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            Init();
        }

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            Pawns ??= new List<Pawn>();
            pawnsForPostLoad = Pawns.ToArray();
            Pawns.Clear();
        }

        try
        {
            switch (Scribe.mode)
            {
                case LoadSaveMode.Saving:
                {
                    // Save the root transform matrix.
                    string s = "";
                    for (int i = 0; i < 16; i++)
                    {
                        s += RootTransform[i];
                        if (i != 15)
                            s += ',';
                    }
                    Scribe_Values.Look(ref s, "rootTransform");
                    break;
                }
                case LoadSaveMode.LoadingVars:
                {
                    string s = string.Empty;
                    Scribe_Values.Look(ref s, "rootTransform");
                    string[] parts = s.Split(',');
                    for (int i = 0; i < 16; i++)
                    {
                        RootTransform[i] = float.Parse(parts[i]);
                    }

                    break;
                }
                case LoadSaveMode.PostLoadInit:
                    Core.Log($"Final matrix was {RootTransform}");
                    break;
            }
        }
        catch (Exception e)
        {
            Core.Error($"Exception exposing matrix during {Scribe.mode}:", e);
        }
        

        bool temp = IsDestroyed;
        Scribe_Values.Look(ref temp, "isDestroyed");
        IsDestroyed = temp;

        temp = WasInterrupted;
        Scribe_Values.Look(ref temp, "wasInterrupted");
        WasInterrupted = temp;

        // TODO workers at end.
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
        if (IsDestroyed)
        {
            Core.Error("Tried to register an already destroyed AnimRenderer");
            return false;
        }

        if (activeRenderers.Contains(this))
        {
            Core.Error("Tried to register AnimRenderer that is already in active list!");
            return false;
        }

        Pawn invalid = GetFirstInvalidPawn();
        if (invalid != null)
        {
            Core.Error($"Tried to start animation with 1 or more invalid pawn: [{invalid.Faction?.Name ?? "No faction"}] {invalid.NameFullColored}");
            foreach (var pawn in Pawns)
            {
                if (!IsPawnValid(pawn))
                    Core.Error($"Invalid Pawn '{pawn.NameShortColored}': Spawned: {pawn.Spawned}, Dead: {pawn.Dead}, Downed: {pawn.Downed}, HasHolderParent: {pawn.ParentHolder is not Verse.Map} ({pawn.ParentHolder}, {pawn.ParentHolder?.GetType().FullName})");
            }
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

        if (!hasStarted)
        {
            hasStarted = true;
            OnStart();
        }

        // This tempTime nonsense is necessary because time is written to directly by the ExposeData,
        // and Seek will not run if time is already the target (seek) time.
        float tempTime = time;
        time = -1;
        Seek(tempTime < 0 ? 0 : tempTime, null); // This will set time back to the correct value.
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

        for (int i = 0; i < Pawns.Count; i++)
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
            WasInterrupted = true;
            Destroy();
            return;
        }

        if (GetFirstInvalidPawn() != null)
        {
            WasInterrupted = true;
            Destroy();
            return;
        }

        foreach (var pawn in Pawns)
        {
            if (pawn.CurJobDef != AAM_DefOf.AAM_InAnimation)
            {
                Core.Error($"{pawn} has bad job: {pawn.CurJobDef}. Cancelling animation.");
                WasInterrupted = true;
                Destroy();
                return;
            }
        }
    }

    /// <summary>
    /// Gets the first captured pawn that is in an invalid state.
    /// See <see cref="IsPawnValid(Pawn)"/> and <see cref="Pawns"/>.
    /// </summary>
    /// <returns>The first invalid pawn or null if all pawns are valid, or there are no pawns in this animation.</returns>
    public Pawn GetFirstInvalidPawn(bool ignoreNotSpawned = false)
    {
        foreach(var pawn in Pawns)
        {
            if (pawn != null && !IsPawnValid(pawn, ignoreNotSpawned))
                return pawn;
        }
        return null;
    }

    /// <summary>
    /// Is this pawn valid to be used in this animator?
    /// Checks simple conditions such as not dead, destroyed, downed, or held by another Thing...
    /// </summary>
    public virtual bool IsPawnValid(Pawn p, bool ignoreNotSpawned = false)
    {
        return p != null && (p.Spawned || ignoreNotSpawned) && !p.Dead && !p.Downed && (p.ParentHolder is Map || (p.ParentHolder == null && ignoreNotSpawned));
    }

    public Vector2 Draw(float? atTime, float? dt, Action<Pawn, Vector2> labelDraw = null)
    {
        if (IsDestroyed)
            return Vector2.zero;

        var range = Seek(atTime, dt * Core.Settings.GlobalAnimationSpeed);

        if (Find.CurrentMap != Map)
            return range; // Do not actually draw if not on the current map...

        DrawTimer.Start();

        foreach (var path in sweeps)
        {
            path.Draw(time);
        }

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
            var matrix = RootTransform * snap.WorldMatrix;
            bool preFx = ov.FlipX ? !snap.FlipX : snap.FlipX;
            bool preFy = ov.FlipY ? !snap.FlipY : snap.FlipY;
            bool fx = MirrorHorizontal ? !preFx : preFx;
            bool fy = MirrorVertical ? !preFy : preFy;
            var mesh = AnimData.GetMesh(fx, fy);

            if (ov.CustomRenderer != null)
            {
                ov.CustomRenderer.TRS = matrix;
                ov.CustomRenderer.OverrideData = ov;
                ov.CustomRenderer.Part = snap.Part;
                ov.CustomRenderer.Snapshot = snap;
                ov.CustomRenderer.Renderer = this;
                ov.CustomRenderer.TweakData = null; // TODO assign tweak data.
                ov.CustomRenderer.Mesh = mesh;
                ov.CustomRenderer.Material = mat;

                bool stop = ov.CustomRenderer.Draw();
                if (stop)
                    continue;
            }

            bool useMPB = ov.UseMPB;

            var color = snap.FinalColor;

            if (useMPB)
            {
                pb.SetTexture("_MainTex", tex);
                pb.SetColor("_Color", color);
            }

            Graphics.DrawMesh(mesh, matrix, mat, 0, Camera, 0, useMPB ? pb : null);
        }

        DrawPawns(labelDraw);

        DrawTimer.Stop();
        return range;
    }

    protected void DrawPawns(Action<Pawn, Vector2> labelDraw = null)
    {
        foreach (var pawn in Pawns)
        {
            if (pawn == null || pawn.Destroyed)
                continue;

            var pawnSS = GetSnapshot(GetPawnBody(pawn));

            if (pawnSS.Active)
            {
                // Position and direction.
                var pos = pawnSS.GetWorldPosition();
                var dir = pawnSS.GetWorldDirection();

                // Render pawn in custom position using patches.
                PreventDrawPatchUpper.AllowNext = true;
                PreventDrawPatch.AllowNext = true;
                MakePawnConsideredInvisible.IsRendering = true;
                pawn.Drawer.renderer.RenderPawnAt(pos, dir, true); // This direction here is not the final one.
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
            }
        }
    }

    private void ApplyPostLoad()
    {
        if (pawnsForPostLoad == null)
            return;

        foreach (var pawn in pawnsForPostLoad)
            AddPawn(pawn);

        pawnsForPostLoad = null;
    }

    /// <summary>
    /// Changes the current time of this animation.
    /// Depending on the parameters specified, this may act as a 'jump' (<paramref name="atTime"/>) or a continuous movement (<paramref name="dt"/>).
    /// </summary>
    public Vector2 Seek(float? atTime, float? dt)
    {
        SeekTimer.Start();

        float t = atTime ?? (this.time + dt.Value);
        var range = SeekInt(t, MirrorHorizontal, MirrorVertical);

        if (t > Data.Duration)
        {
            if (Loop)
                this.time = 0;
            else
                Destroy();
        }

        SeekTimer.Stop();
        return range;
    }

    private Vector2 SeekInt(float time, bool mirrorX = false, bool mirrorY = false)
    {
        time = Mathf.Clamp(time, 0f, Duration);

        if (Math.Abs(this.time - time) < 0.0001f)
            return new Vector2(-1, -1);

        // Pass 1: Evaluate curves, make local matrices.
        for (int i = 0; i < Data.Parts.Count; i++)
            snapshots[i] = new AnimPartSnapshot(Data.Parts[i], this, time);

        // Pass 2: Resolve world matrices using inheritance tree.
        for (int i = 0; i < Data.Parts.Count; i++)
            snapshots[i].UpdateWorldMatrix(mirrorX, mirrorY);

        float start = Mathf.Min(this.time, time);
        float end = Mathf.Max(this.time, time);
        this.time = time;

        return new Vector2(start, end);
    }

    public void OnStart()
    {
        // Give pawns their jobs.
        foreach (var pawn in Pawns)
        {
            var newJob = JobMaker.MakeJob(AAM_DefOf.AAM_InAnimation);

            if (pawn.verbTracker?.AllVerbs != null)
                foreach (var verb in pawn.verbTracker.AllVerbs)
                    verb.Reset();


            if (pawn.equipment?.AllEquipmentVerbs != null)
                foreach (var verb in pawn.equipment.AllEquipmentVerbs)
                    verb.Reset();

            pawn.jobs.StartJob(newJob, JobCondition.InterruptForced, null, false, true, null);

            if (pawn.CurJobDef != AAM_DefOf.AAM_InAnimation)
            {
                Core.Error($"CRITICAL ERROR: Failed to force interrupt {pawn}'s job with animation job. Likely a mod conflict.");
            }
        }

        // Teleport pawns to their starting positions. They should already be there, but check just in case.
        IntVec3 basePos = RootTransform.MultiplyPoint3x4(Vector3.zero).ToIntVec3();
        basePos.y = 0;

        for (int i = 0; i < Pawns.Count; i++)
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

        for (int i = 0; i < Pawns.Count; i++)
        {
            var pawn = Pawns[i];
            if (pawn == null)
                continue;

            var posData = Def.TryGetCell(AnimCellData.Type.PawnEnd, MirrorHorizontal, MirrorVertical, i);
            if (posData == null)
                continue;

            TeleportPawn(pawn, basePos + posData.Value.ToIntVec3);
            pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }

        foreach (var sweep in sweeps)
        {
            sweep.Mesh.Dispose();
        }
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

    public IEnumerable<EventBase> GetEventsInPeriod(Vector2 timePeriod)
    {
        return Data.GetEventsInPeriod(timePeriod);
    }

    public AnimPartData GetPart(string name) => Data.GetPart(name);

    protected virtual bool ShouldDraw(in AnimPartSnapshot snapshot)
    {
        return snapshot.Active && !GetOverride(snapshot).PreventDraw && snapshot.FinalColor.a > 0;
    }

    protected virtual Material GetMaterialFor(in AnimPartSnapshot snapshot)
    {
        var ov = GetOverride(snapshot);
        var ovMat = ov.Material;
        if (ovMat != null)        
            return ovMat;        

        if (ov.UseDefaultTransparentMaterial || snapshot.Part.TransparentByDefault || snapshot.FinalColor.a < 1f)        
            return DefaultTransparent;        

        return DefaultCutout;
    }

    public bool AddPawn(Pawn pawn)
    {
        if (pawn == null)
            return false;

        int index = Pawns.Count;
        Pawns.Add(pawn);
        char tagChar = AnimRenderer.Alphabet[index];

        // Held item.
        string itemName = $"Item{tagChar}";
        var weapon = pawn.GetFirstMeleeWeapon();
        var tweak = weapon == null ? null : TweakDataManager.GetOrCreateDefaultTweak(weapon.def);
        var handsMode = tweak?.HandsMode ?? HandsMode.Default;

        // Hands and skin color...
        string mainHandName = $"HandA{(index > 0 ? (index + 1) : "")}";
        string altHandName = $"HandB{(index > 0 ? (index + 1) : "")}";

        Color skinColor = pawn.story?.SkinColor ?? Color.white;
        bool showMain = weapon != null && handsMode != HandsMode.No_Hands;
        bool showAlt = weapon != null && handsMode == HandsMode.Default;

        // Apply weapon.
        var itemPart = GetPart(itemName);
        if (weapon != null && itemPart != null)
        {
            tweak.Apply(this, itemPart);
            var ov = GetOverride(itemPart);
            ov.Material = weapon.Graphic?.MatSingleFor(weapon);
            ov.UseMPB = false; // Do not use the material property block, because it will override the material second color and mask.

            // FIX: Certain vanilla textures are set to Clamp instead of Wrap. This breaks flipping.
            // Which ones seems random. (Beer is Clamp, Breach Axe is Repeat).
            // For now, force them to be repeat. Not sure if this will have any negative impact elsewhere in the game. Hopefully not.
            if (ov.Material != null)
            {
                var main = ov.Material.mainTexture;
                if(main != null)
                    main.wrapMode = TextureWrapMode.Repeat;

                var mask = ov.Material.GetTexture(ShaderPropertyIDs.MaskTex);
                if (mask != null)
                    mask.wrapMode = TextureWrapMode.Repeat;
            }

            if (ov.CustomRenderer != null)
                ov.CustomRenderer.Item = weapon;

            foreach (var path in sweeps)
            {
                if (path.Part == itemPart)
                {
                    path.DownDst = tweak.BladeStart;
                    path.UpDst = tweak.BladeEnd;
                }
            }
        }

        // Apply main hand.
        var mainHandPart = GetPart(mainHandName);
        if (mainHandPart != null)
        {
            var ov = GetOverride(mainHandPart);
            ov.PreventDraw = !showMain;
            ov.Texture = AnimationManager.HandTexture;
            ov.ColorOverride = skinColor;
        }

        // Apply alt hand.
        var altHandPart = GetPart(altHandName);
        if (mainHandPart != null)
        {
            var ov = GetOverride(altHandPart);
            ov.PreventDraw = !showAlt;
            ov.Texture = AnimationManager.HandTexture;
            ov.ColorOverride = skinColor;
        }

        return true;
    }

    public class PartWithSweep
    {
        public readonly AnimPartData Part;
        public readonly SweepPointCollection Points;
        public readonly SweepMesh<Data> Mesh;
        public readonly AnimRenderer Renderer;
        public float DownDst, UpDst;

        private float lastTime = -1;
        private int lastIndex = -1;

        public struct Data
        {
            public float Time;
            public float DownVel;
            public float UpVel;
        }

        public PartWithSweep(AnimRenderer renderer, AnimPartData part, SweepPointCollection points, SweepMesh<Data> mesh)
        {
            Renderer = renderer;
            Part = part;
            Points = points;
            Mesh = mesh;
        }

        public void Draw(float time)
        {
            DrawInt(time);

            Graphics.DrawMesh(Mesh.Mesh, Renderer.RootTransform, DefaultCutout, 0);
        }

        private bool DrawInt(float time)
        {
            if (time == lastTime)
                return false;

            if (time < lastTime)
            {
                Rebuild(time);
                return true;
            }

            for (int i = lastIndex + 1; i < Points.Points.Count; i++)
            {
                var point = Points.Points[i];
                if (point.Time > time)
                    break;

                point.GetEndPoints(DownDst, UpDst, out var down, out var up);
                Mesh.AddLine(down, up, new Data()
                {
                    Time = point.Time,
                    UpVel = point.VelocityTop,
                    DownVel = point.VelocityBottom
                });
                lastIndex = i;
            }

            lastTime = time;
            AddInterpolatedPos(lastIndex, time);

            Mesh.UpdateColors(MakeColors);
            Mesh.Rebuild();
            return true;
        }

        private void Rebuild(float upTo)
        {
            Mesh.Clear();
            for(int i = 0; i < Points.Points.Count; i++)
            {
                var point = Points.Points[i];
                if (point.Time > upTo)
                    break;

                point.GetEndPoints(DownDst, UpDst, out var down, out var up);
                Mesh.AddLine(down, up, new Data()
                {
                    Time = point.Time,
                    UpVel = point.VelocityTop,
                    DownVel = point.VelocityBottom
                }); lastIndex = i;
            }
            AddInterpolatedPos(lastIndex, upTo);

            Mesh.UpdateColors(MakeColors);
            Mesh.Rebuild();
            lastTime = upTo;
        }

        private (Color down, Color up) MakeColors(in Data data)
        {
            return (Color.red, Color.green);
        }

        private void AddInterpolatedPos(int lastIndex, float currentTime)
        {
            if (lastIndex < 0)
                return;
            if (lastIndex >= Points.Count - 1)
                return; // Can't interpolate if we don't have the end.

            Core.Log($"{lastIndex} of {Points.Count}");

            var lastPoint = Points.Points[lastIndex];
            if (Mathf.Abs(lastPoint.Time - currentTime) < 0.001f)
                return;

            var nextPoint = Points.Points[lastIndex + 1];

            float t = Mathf.InverseLerp(lastPoint.Time, nextPoint.Time, currentTime);
            var newPoint = SweepPoint.Lerp(lastPoint, nextPoint, t);
            newPoint.GetEndPoints(DownDst, UpDst, out var down, out var up);
            Mesh.AddLine(down, up, new Data()
            {
                Time = currentTime,
                UpVel = newPoint.VelocityTop,
                DownVel = newPoint.VelocityBottom
            });
        }
    }
}
