using AAM;
using AAM.Events;
using AAM.Events.Workers;
using AAM.Patches;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AAM.Tweaks;
using UnityEngine;
using Verse;
using Verse.AI;
using Debug = UnityEngine.Debug;

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
        renderer.WasInterrupted = (renderer.Duration - renderer.CurrentTime) > (1f / 30f);        

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

    internal List<(EventBase e, EventWorkerBase worker)> workersAtEnd = new List<(EventBase e, EventWorkerBase worker)>();
    private AnimPartSnapshot[] snapshots;
    private AnimPartOverrideData[] overrides;
    private AnimPartData[] bodies = new AnimPartData[8]; // TODO REPLACE WITH LIST OR DICT.
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

            // Load workers-at-end.
            workersAtEnd.Clear();
            List<int> endEventIndices = new List<int>();
            Scribe_Collections.Look(ref endEventIndices, "endEventIndices");
            if (endEventIndices != null && Def != null)
            {
                try
                {
                    foreach (int index in endEventIndices)
                    {
                        var e = Def.Data.Events[index];
                        var worker = e.GetWorker<EventWorkerBase>();
                        workersAtEnd.Add((e, worker));
                    }
                }
                catch (Exception e)
                {
                    Core.Error($"Exception loading end-of-clip events from indices. Has the animation changed?", e);
                }
            }
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

                    // Save the workers-at-end.
                    List<int> endEventIndices = workersAtEnd.Select(p => p.e.Index).ToList();
                    Scribe_Collections.Look(ref endEventIndices, "endEventIndices");
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
            Destroy();
            return;
        }

        if (GetFirstInvalidPawn() != null)
        {
            Destroy();
        }

        foreach (var pawn in Pawns)
        {
            if (pawn.CurJobDef != AAM_DefOf.AAM_InAnimation)
            {
                Core.Error($"{pawn} has bad job: {pawn.CurJobDef}. Cancelling animation.");
                Destroy();
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

            if (useMPB)
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

        if (this.time == time)
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

        Core.Log("END");

        foreach(var item in workersAtEnd)
        {
            try
            {
                item.worker?.Run(new AnimEventInput(item.e, this, false, null));
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
}
