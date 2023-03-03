using AAM;
using AAM.Events;
using AAM.Patches;
using AAM.Processing;
using AAM.RendererWorkers;
using AAM.Sweep;
using AAM.Tweaks;
using JetBrains.Annotations;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Verse;
using Verse.AI;
using Color = UnityEngine.Color;
using Debug = UnityEngine.Debug;

/// <summary>
/// An <see cref="AnimRenderer"/> is the object that represents and also draws (renders) a currently running animation.
/// Be sure to check the <see cref="IsDestroyed"/> property before interacting with an object of this type.
/// </summary>
public class AnimRenderer : IExposable
{
    #region Static stuff

    public static readonly char[] Alphabet = new char[] { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H' };
    public static readonly List<AnimRenderer> PostLoadPendingAnimators = new List<AnimRenderer>();
    public static Material DefaultCutout, DefaultTransparent;
    public static IReadOnlyList<AnimRenderer> ActiveRenderers => activeRenderers;
    public static IReadOnlyCollection<Pawn> CapturedPawns => pawnToRenderer.Keys;
    public static int TotalCapturedPawnCount => pawnToRenderer.Count;

    private static readonly MaterialPropertyBlock shadowMpb = new MaterialPropertyBlock();
    private static readonly List<AnimRenderer> activeRenderers = new List<AnimRenderer>();
    private static readonly Dictionary<Pawn, AnimRenderer> pawnToRenderer = new Dictionary<Pawn, AnimRenderer>();
    private static readonly Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();

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

    public static Texture2D ResolveTexture(in AnimPartSnapshot snapshot)
    {
        if (snapshot.Renderer == null)
            return null;

        var ov = snapshot.Renderer.overrides[snapshot.Part.Index];

        if (ov.Texture != null)
            return ov.Texture;

        if (snapshot.TexturePath == null)
            return null;

        return ResolveTexture(snapshot.TexturePath, snapshot);
    }

    public static Texture2D ResolveTexture(string texturePath, in AnimPartSnapshot snapshot)
    {
        if (textureCache.TryGetValue(texturePath, out var found))
            return found;

        Texture2D loaded;
        loaded = ContentFinder<Texture2D>.Get(texturePath, false);
        textureCache.Add(texturePath, loaded);
        if (loaded == null)
            Core.Error($"Failed to load texture '{texturePath}' for {snapshot.PartName} (frame: {snapshot.FrameIndex})");

        return loaded;
    }

    public static void ClearAll()
    {
        activeRenderers.Clear();
        pawnToRenderer.Clear();
        PostLoadPendingAnimators.Clear();
    }

    public static void TickAll()
    {
        AddFromPostLoad();

        foreach (AnimRenderer renderer in activeRenderers)
        {
            if (!renderer.IsDestroyed)
                renderer.Tick();
        }
    }

    public static void DrawAll(float dt, Map map, in CellRect viewBounds, List<Vector2> seekTimes, Action<Pawn, Vector2> labelDraw = null)
    {
        AddFromPostLoad();

        bool shouldSeek = seekTimes == null;

        for (int i = 0 ; i < activeRenderers.Count; i++)
        {
            var renderer = activeRenderers[i];
            if (renderer.Map != map || renderer.IsDestroyed)
                continue;

            try
            {
                DrawSingle(renderer, shouldSeek ? dt : null, !shouldSeek ? seekTimes[i] : null, viewBounds, labelDraw);
            }
            catch(Exception e)
            {
                Core.Error($"Exception rendering animator '{renderer}'", e);
            }
        }
    }

    public static void DrawAllGUI(Map map)
    {
        AddFromPostLoad();

        for (int i = 0; i < activeRenderers.Count; i++)
        {
            var renderer = activeRenderers[i];

            if (renderer.Map != map || renderer.IsDestroyed)
                continue;

            try
            {
                renderer.DrawGUI();
            }
            catch (Exception e)
            {
                Core.Error($"Exception rendering animator '{renderer}'", e);
            }

        }
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

    public static void RemoveDestroyed()
    {
        // Remove destroyed.
        for (int i = 0; i < activeRenderers.Count; i++)
        {
            var r = activeRenderers[i];
            if (r.IsDestroyed)
            {
                DestroyNew(r);
                i--;
            }
        }
    }

    private static void DrawSingle(AnimRenderer renderer, float? dt, Vector2? tp, CellRect viewBounds, Action<Pawn, Vector2> labelDraw = null)
    {
        // Draw and handle events.
        Vector2 timePeriod = default;
        try
        {
            bool cull = Core.Settings.OffscreenCulling && !viewBounds.Contains(renderer.RootPosition.ToIntVec3());
            timePeriod = renderer.Draw(null, dt ?? 0, dt != null, cull, labelDraw);
        }
        catch (Exception e)
        {
            Core.Error($"Rendering exception when doing animation {renderer}", e);
        }

        foreach (var e in renderer.GetEventsInPeriod(tp ?? timePeriod))
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
    }

    private static void DestroyNew(AnimRenderer renderer)
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

    public static float Remap(float value, float a, float b, float a2, float b2)
        => Mathf.Lerp(a2, b2, Mathf.InverseLerp(a, b, value));

    #endregion

    /// <summary>
    /// The active animation data. Animation data is loaded from disk upon request.
    /// Note that this is not the same as an animation def: see <see cref="Def"/> for the definition.
    /// </summary>
    public AnimData Data => ExecutionOutcome <= ExecutionOutcome.Damage ? Def.DataNonLethal : Def.Data;
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
    /// Should the animator be saved with the map?
    /// </summary>
    public bool ShouldSave => !IsDestroyed && PawnCount > 0;

    /// <summary>
    /// The root world position of this animation.
    /// Derived from <see cref="RootTransform"/>.
    /// </summary>
    public Vector3 RootPosition => RootTransform.MultiplyPoint3x4(default);
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
    public bool Loop;
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
    /// <summary>
    /// The custom renderer worker, optional.
    /// </summary>
    public AnimationRendererWorker AnimationRendererWorker;
    /// <summary>
    /// Save data such as data used to track duel status.
    /// </summary>
    public SaveData SD = new SaveData();
    /// <summary>
    /// An action that is invoked when <see cref="OnEnd"/> is called.
    /// This action is not serialized.
    /// </summary>
    public Action<AnimRenderer> OnEndAction;
    /// <summary>
    /// A scale on the speed of this animation.
    /// </summary>
    public float TimeScale = 1f;

    public double DrawMS;
    public double SeekMS;
    public double SweepMS;

    private List<AnimPartData> bodies = new List<AnimPartData>();
    private HashSet<Pawn> pawnsValidEvenIfDespawned = new HashSet<Pawn>();
    private AnimPartSnapshot[] snapshots;
    private AnimPartOverrideData[] overrides;
    private PartWithSweep[] sweeps;
    private MaterialPropertyBlock pb;
    private float time = -1;
    private Pawn[] pawnsForPostLoad;
    private bool hasStarted;

    [UsedImplicitly]
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

        // Dummy array for sweep meshes.
        // Reason: A Unity bug where creating a new mesh (new Mesh()) during loading
        // crashes the game.
        // Solution: delay creating meshes until the game has started rendering.
        sweeps = Array.Empty<PartWithSweep>();

        Debug.Assert(AnimationRendererWorker == null);
        AnimationRendererWorker = Def.TryMakeRendererWorker();
    }

    private void InitSweepMeshes()
    {
        if (Data.SweepDataCount == sweeps.Length)
            return;

        int j = 0;
        sweeps = new PartWithSweep[Data.SweepDataCount];
        foreach (var part in Data.PartsWithSweepData)
        {
            var paths = Data.GetSweepPaths(part);
            foreach (var path in paths)
            {
                sweeps[j++] = new PartWithSweep(this, part, path, new SweepMesh<PartWithSweep.Data>(), BasicSweepProvider.DefaultInstance);
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
        Scribe_Collections.Look(ref pawnsValidEvenIfDespawned, "pawnsValidEvenIfDespawned", LookMode.Reference);
        Scribe_References.Look(ref Map, "map");
        Scribe_Values.Look(ref TimeScale, "timeScale", 1f);
        Scribe_Deep.Look(ref SD, "saveData");
        SD ??= new SaveData();

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            Init();
            Pawns ??= new List<Pawn>();
            pawnsValidEvenIfDespawned ??= new HashSet<Pawn>();
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
                    //Core.Log($"Final matrix was {RootTransform}");
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
    }

    public void FlagAsValidIfDespawned(Pawn pawn)
    {
        if (pawn == null)
            throw new ArgumentNullException(nameof(pawn));

        if (!Pawns.Contains(pawn))
        {
            Core.Error("Should not mark a pawn as valid if despawned if that pawn is not part of this animation!");
            return;
        }

        pawnsValidEvenIfDespawned.Add(pawn);
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
    /// Instead, consider using <see cref="AnimationManager.StartAnimation"/>.
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

        //if (PawnCount != Def.pawnCount)
        //    Core.Warn($"Started AnimRenderer with bad number of pawns! Expected {Def.pawnCount}, got {PawnCount}. (Def: {Def})");

        RegisterInt(this);

        if (!hasStarted)
            OnStart();

        // This tempTime nonsense is necessary because time is written to directly by the ExposeData,
        // and Seek will not run if time is already the target (seek) time.
        float tempTime = time;
        time = -1;
        Seek(tempTime < 0 ? 0 : tempTime, 0); // This will set time back to the correct value.

        AnimationRendererWorker?.SetupRenderer(this);
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

        IsDestroyed = true;
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

        // TODO remove this nonsense and correctly link pawns with bodies when adding pawns.
        while (bodies.Count < Pawns.Count)
        {
            bodies.Add(null);
        }

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
                if (pawnsValidEvenIfDespawned.Contains(pawn))
                    continue;

                Core.Error($"{pawn} has bad job: {pawn.CurJobDef}. Cancelling animation.");
                WasInterrupted = true;
                Destroy();
                return;
            }
        }
    }

    /// <summary>
    /// Gets the first captured pawn that is in an invalid state.
    /// See <see cref="IsPawnValid"/> and <see cref="Pawns"/>.
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
        // Summary:
        // Not dead or downed.
        bool basic = p is {Dead: false, Downed: false};
        if (!basic)
            return false;

        // Check not spawned exceptions list.
        if (!p.Spawned && pawnsValidEvenIfDespawned.Contains(p))
            return true;

        // Spawned (includes things like grappling, flying)
        // Is part of the correct map (checks for drop pods, teleporting across maps)
        return (p.Spawned || ignoreNotSpawned) && ((p.ParentHolder is Map m && m == Map) || (p.ParentHolder == null && ignoreNotSpawned));
    }

    public Vector2 Draw(float? atTime, float dt, bool seek, bool cullDraw, Action<Pawn, Vector2> labelDraw = null)
    {
        if (IsDestroyed)
            return Vector2.zero;

        InitSweepMeshes();
        foreach (var item in sweeps)
            if (item != null)
                item.MirrorHorizontal = MirrorHorizontal;

        var range = new Vector2(-1, -1);
        if (seek)
            range = Seek(atTime, dt);
        else
            SeekMS = 0;

        if (Find.CurrentMap != Map || cullDraw)
        {
            DrawMS = 0;
            return range; // Do not actually draw if not on the current map or culled.
        }

        var timer = new RefTimer();
        var timer2 = new RefTimer();
        foreach (var path in sweeps)
            path.Draw(time);
        timer2.GetElapsedMilliseconds(out SweepMS);

        foreach (var snap in snapshots)
        {
            if (!ShouldDraw(snap))
                continue;

            var tex = ResolveTexture(snap);
            if (tex == null)
                continue;
            
            var mat = GetMaterialFor(snap, out bool forceMPB);
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
                ov.CustomRenderer.TweakData = ov.TweakData;
                ov.CustomRenderer.Mesh = mesh;
                ov.CustomRenderer.Material = mat;

                bool stop = ov.CustomRenderer.Draw();
                if (stop)
                    continue;
            }

            bool useMPB = forceMPB || ov.UseMPB;
            var color = snap.FinalColor;

            int passes = 1;
            for (int i = 0; i < passes; i++)
            {
                if (useMPB)
                {
                    //Core.Log($"Use mpb mat is {mat} for {ov.TweakData?.ItemDefName} on {snap.PartName} with mode {snap.SplitDrawMode}");
                    pb.Clear();

                    // Basic texture and color, always used. Color might be replaced, see below.
                    pb.SetTexture("_MainTex", tex);
                    pb.SetColor("_Color", color);

                    if (ov.Material != null)
                    {
                        // Check for a mask...
                        bool doesUseMask = ov.Material.HasProperty(ShaderPropertyIDs.MaskTex);
                        if (doesUseMask)
                        {
                            // Get the mask and mask color.
                            var mask = ov.Material.GetTexture(ShaderPropertyIDs.MaskTex);

                            // Tint is applied to the mask.
                            pb.SetColor("_Color", color); // Color comes from animation.
                            pb.SetColor("_ColorTwo", ov.Weapon.DrawColor); // Mask tint
                            pb.SetTexture(ShaderPropertyIDs.MaskTex, mask);

                        }
                        else
                        {
                            // Tint is applied straight to main color.
                            pb.SetColor("_Color", color * ov.Weapon.DrawColor);
                        }
                    }

                    if (snap.SplitDrawMode != AnimData.SplitDrawMode.None && snap.SplitDrawPivot != null)
                    {
                        ConfigureSplitDraw(snap, ref matrix, pb, ov, i, ref passes);
                    }
                }

                var finalMpb = useMPB ? pb : null;

                AnimationRendererWorker?.PreRenderPart(snap, ov, ref mesh, ref matrix, ref mat, ref finalMpb);
                Graphics.DrawMesh(mesh, matrix, mat, 0, Camera, 0, finalMpb);
            }
        }

        DrawPawns(labelDraw);

        timer.GetElapsedMilliseconds(out DrawMS);
        return range;
    }

    private void ConfigureSplitDraw(in AnimPartSnapshot part, ref Matrix4x4 matrix, MaterialPropertyBlock pb, AnimPartOverrideData ov, int currentPass, ref int passCount)
    {
        bool preFx = ov.FlipX ? !part.FlipX : part.FlipX;
        bool preFy = ov.FlipY ? !part.FlipY : part.FlipY;
        bool fx = MirrorHorizontal ? !preFx : preFx;
        bool fy = MirrorVertical ? !preFy : preFy;

        float textureRot = ov.LocalRotation * Mathf.Deg2Rad;
        pb.SetFloat("CutoffAngle", textureRot * (fy ? -1f : 1f));
        var mode = part.SplitDrawMode;
        if (mode == AnimData.SplitDrawMode.BeforeAndAfter)
        {
            mode = currentPass == 0 ? AnimData.SplitDrawMode.Before : AnimData.SplitDrawMode.After;
            if (currentPass == 0)
                passCount++;
            else
                matrix *= Matrix4x4.Translate(new Vector3(0, -0.9f, 0)); // This is the actual offset depth here.
        }
        pb.SetFloat("Polarity", mode == AnimData.SplitDrawMode.Before ? 1f : -1f);

        // The total length of the weapon sprite, in world units (1 unit = 1 cell).
        float length = matrix.lossyScale.x * Remap(Mathf.Abs(Mathf.Sin(textureRot * 2f)), 0, 1f, 1f, 1.41421356237f); // sqrt(2)
        float distanceScale = Remap(Mathf.Abs(Mathf.Sin(textureRot * 2f)), 0, 1f, 0.5f, 0.70710678118f); // sqrt(0.5)

        Matrix4x4 noOverrideMat = part.Renderer.RootTransform * part.WorldMatrixNoOverride * Matrix4x4.Scale(ov.LocalScaleFactor.ToWorld());

        Vector2 renderedPos = matrix.MultiplyPoint3x4(Vector3.zero).ToFlat();
        Vector2 startPos = renderedPos - length * 0.5f * noOverrideMat.MultiplyVector(Vector3.right).normalized.ToFlat();
        Vector2 endPos = renderedPos + length * 0.5f * noOverrideMat.MultiplyVector(Vector3.right).normalized.ToFlat();
        Vector2 basePos = GetSnapshot(part.SplitDrawPivot).GetWorldPosition().ToFlat();

        var ap = basePos - startPos;
        var ab = endPos - startPos;
        float lerp = Vector2.Dot(ap, ab) / Vector2.Dot(ab, ab);
        if (!fx)
            lerp = 1 - lerp;

        pb.SetFloat("Distance", distanceScale * (-1f + lerp * 2f));
    }

    public void DrawGUI()
    {
        AnimationRendererWorker?.DrawGUI(this);
    }

    protected void DrawPawns(Action<Pawn, Vector2> labelDraw = null)
    {
        foreach (var pawn in Pawns)
        {
            if (pawn == null || pawn.Destroyed)
                continue;

            var pawnSS = GetSnapshot(GetPawnBody(pawn));

            if (!pawnSS.Active)
            {
                if (Def.drawDisabledPawns)
                {
                    // Regular pawn render.
                    PreventDrawPatchUpper.AllowNext = true;
                    PreventDrawPatch.AllowNext = true;
                    PreventDrawPatch.DoNotModify = true; // Don't use animation position/rotation.
                    MakePawnConsideredInvisible.IsRendering = true;
#if V13
                    pawn.DrawAt(pawn.DrawPos);
#else
                    pawn.DrawAt(pawn.DrawPosHeld ?? pawn.DrawPos);
#endif
                    PreventDrawPatch.DoNotModify = false;
                    MakePawnConsideredInvisible.IsRendering = false;

                    // Draw label.
                    Vector3 drawPos2 = pawn.DrawPos;
                    drawPos2.z -= 0.6f;
                    Vector2 vector2 = Find.Camera.WorldToScreenPoint(drawPos2) / Prefs.UIScale;
                    vector2.y = UI.screenHeight - vector2.y;
                    labelDraw?.Invoke(pawn, vector2);

                }
                continue;
            }

            // Position and direction.
            var pos = pawnSS.GetWorldPosition();
            var dir = pawnSS.GetWorldDirection();

            // Worker pre-draw
            AnimationRendererWorker?.PreRenderPawn(pawnSS, ref pos, ref dir, pawn);

            bool suppressShadow = Def.shadowDrawFromData && pawnSS.DataC > 0.2f;

            // Render pawn in custom position using patches.
            PreventDrawPatchUpper.AllowNext = true;
            PreventDrawPatch.AllowNext = true;
            MakePawnConsideredInvisible.IsRendering = true;
            Patch_PawnRenderer_DrawInvisibleShadow.Suppress = suppressShadow; // In 1.4 shadow rendering is baked into RenderPawnAt and may need to be prevented.
            pawn.Drawer.renderer.RenderPawnAt(pos, dir, true); // This direction here is not the final one.
            Patch_PawnRenderer_DrawInvisibleShadow.Suppress = false;
            MakePawnConsideredInvisible.IsRendering = false;

            // Render shadow.
            if (Def.shadowDrawFromData)
            {
                // DataC now drives the shadow rendering.
                // Value of 0 means regular shadow rendering,
                // value of 1 means shadow is a ground-based entity shadow (a-la-minecraft).
                Vector3 groundPos = pos with { z = RootPosition.z };
                Vector3 scale = new Vector3(1, 1, 0.5f);
                Color color = new Color(0, 0, 0, pawnSS.DataC * 0.6f);

                if (pawnSS.DataC > 0f)
                    DrawSimpleShadow(groundPos, scale, color);
            }
            else
            {
                // Regular shadow draw, just like base game.
                // Only required in Rimworld 1.3, in 1.4 it is baked into the RenderPawnAt method.
#if V13
                pawn.Drawer.renderer.DrawInvisibleShadow(pos);
#endif
            }

            // Figure out where to draw the pawn label.
            Vector3 drawPos = pos;
            drawPos.z -= 0.6f;
            Vector2 vector = Find.Camera.WorldToScreenPoint(drawPos) / Prefs.UIScale;
            vector.y = UI.screenHeight - vector.y;
            labelDraw?.Invoke(pawn, vector);
        }
    }

    private void DrawSimpleShadow(Vector3 pos, Vector3 size, Color color)
    {
        var trs = Matrix4x4.TRS(pos, Quaternion.identity, size);
        shadowMpb.SetColor("_Color", color);
        shadowMpb.SetTexture("_MainTex", Content.Shadow);
        Graphics.DrawMesh(MeshPool.plane10, trs, DefaultTransparent, 0, Camera, 0, shadowMpb);
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
    public Vector2 Seek(float? atTime, float dt)
    {
        float t = atTime ?? (time + dt * TimeScale * Core.Settings.GlobalAnimationSpeed);

        if (Math.Abs(this.time - t) < 0.0001f)
            return new Vector2(-1, -1);

        var timer = new RefTimer();

        var range = SeekInt(t, MirrorHorizontal, MirrorVertical);

        timer.GetElapsedMilliseconds(out SeekMS);

        if (t > Data.Duration)
        {
            if (Loop)
                time = 0;
            else
                Destroy();
        }
        return range;
    }

    private Vector2 SeekInt(float time, bool mirrorX = false, bool mirrorY = false)
    {
        time = Mathf.Clamp(time, 0f, Duration);

        // Pass 1: Evaluate curves, make local matrices.
        for (int i = 0; i < Data.Parts.Count; i++)
        {
            snapshots[i] = new AnimPartSnapshot(Data.Parts[i], this, time);
        }

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
        if (hasStarted)
            Core.Error("Started twice!");
        
        hasStarted = true;

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

            pawn.jobs.StartJob(newJob, JobCondition.InterruptForced);

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

        // Custom sweep paths:
        ISweepProvider sweepProvider = BasicSweepProvider.DefaultInstance;
        var weapon = Pawns.Count == 0 ? null : Pawns[0].GetFirstMeleeWeapon();
        var tweak = weapon == null ? null : TweakDataManager.TryGetTweak(weapon.def);
        if (tweak?.GetSweepProvider() != null)
            sweepProvider = tweak.GetSweepProvider();
        else if (Def.sweepProvider != null)
            sweepProvider = Def.sweepProvider;

        InitSweepMeshes();
        foreach (var sweep in sweeps)
        {
            if (sweep == null)
                continue;
            sweep.ColorProvider = sweepProvider;
        }
    }

    public void OnEnd()
    {
        try
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
                pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);
            }
        }
        catch (Exception e)
        {
            Core.Error("Exception in AnimRenderer.OnEnd", e);
        }
        finally
        {
            foreach (var sweep in sweeps)
            {
                sweep?.Mesh?.Dispose();
            }

            OnEndAction?.Invoke(this);
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

    protected virtual Material GetMaterialFor(in AnimPartSnapshot snapshot, out bool forceMPB)
    {
        forceMPB = false;

        // If drawing in split mode, must use the split shader.
        if (snapshot.SplitDrawMode != AnimData.SplitDrawMode.None && snapshot.SplitDrawPivot != null)
        {
            // This shader is designed to work with the mpb.
            forceMPB = true;
            return Content.CustomCutoffMaterial;
        }

        // Otherwise check for an override material.
        var ov = GetOverride(snapshot);
        var ovMat = ov.Material;
        if (ovMat != null)        
            return ovMat;

        // Finally fall back to transparent or cutout.
        if (ov.UseDefaultTransparentMaterial || snapshot.Part.TransparentByDefault || snapshot.FinalColor.a < 1f)        
            return DefaultTransparent;        

        return DefaultCutout;
    }

    public bool AddPawn(Pawn pawn) => AddPawn(pawn, Pawns.Count, true);

    public bool AddPawn(Pawn pawn, int index, bool register)
    {
        if (pawn == null)
            return false;

        if (register)
            Pawns.Add(pawn);

        char tagChar = Alphabet[index];

        // Hands.
        ConfigureHandsForPawn(pawn, index);

        // Held item.
        string itemName = $"Item{tagChar}";
        var weapon = pawn.GetFirstMeleeWeapon();
        var tweak = weapon == null ? null : TweakDataManager.TryGetTweak(weapon.def);

        // Apply weapon.
        var itemPart = GetPart(itemName);
        if (weapon != null && itemPart != null && tweak != null)
        {
            tweak.Apply(this, itemPart);
            var ov = GetOverride(itemPart);
            ov.Weapon = weapon;
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

                var mask = ov.Material.HasProperty(ShaderPropertyIDs.MaskTex) ? ov.Material.GetTexture(ShaderPropertyIDs.MaskTex) : null;
                if (mask != null)
                    mask.wrapMode = TextureWrapMode.Repeat;
            }

            if (ov.CustomRenderer != null)
                ov.CustomRenderer.Item = weapon;

            InitSweepMeshes();
            foreach (var path in sweeps)
            {
                if (path.Part == itemPart)
                {
                    path.DownDst = tweak.BladeStart;
                    path.UpDst = tweak.BladeEnd;
                }
            }
        }

        return true;
    }

    private void ConfigureHandsForPawn(Pawn pawn, int index)
    {
        var weapon = pawn.GetFirstMeleeWeapon();
        var tweak = weapon?.TryGetTweakData();
        var handsMode = tweak?.HandsMode ?? HandsMode.Default;

        // Hands and skin color...
        string mainHandName = $"HandA{(index > 0 ? (index + 1) : "")}";
        string altHandName = $"HandB{(index > 0 ? (index + 1) : "")}";

        Color skinColor = pawn.story?.SkinColor ?? Color.white;

        // Hand visibility uses the animation data first and foremost, and if the animation does
        // not care about hand visibility, then it is dictated by the weapon.
        var vis = Def.GetHandsVisibility(index);
        bool showMain = Core.Settings.ShowHands && (vis.showMainHand ?? (weapon != null && handsMode != HandsMode.No_Hands));
        bool showAlt = Core.Settings.ShowHands && (vis.showAltHand ?? (weapon != null && handsMode == HandsMode.Default));

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
    }

    public override string ToString() => Def.label == null ? Def.defName : Def.LabelCap;

    public class SaveData : IExposable
    {
        public HashSet<int> DuelSegmentsDone = new HashSet<int>();
        public int DuelSegmentsDoneCount;
        public int TargetDuelSegmentCount;

        public void ExposeData()
        {
            Scribe_Values.Look(ref DuelSegmentsDoneCount, "duelSegmentsDoneCount");
            Scribe_Values.Look(ref TargetDuelSegmentCount, "targetDuelSegmentCount");
            Scribe_Collections.Look(ref DuelSegmentsDone, "duelSegmentsDone", LookMode.Value);
            DuelSegmentsDone ??= new HashSet<int>();
        }
    }
}
