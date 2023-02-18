using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AAM.Events;
using UnityEngine;
using System.Runtime.InteropServices;
using AAM.Data.Model;
using AAM.Tweaks;
using Newtonsoft.Json;
#if !UNITY_EDITOR
using Verse;
using AAM;
#endif

public class AnimData
{
    #region Static Stuff

    private static Mesh m, mfx, mfy, mfxy;
    private static readonly Dictionary<string, AnimData> cache = new Dictionary<string, AnimData>();

    public static Mesh GetMesh(bool flipX, bool flipY)
    {
        if (m == null)
        {
            m = MakeMesh(Vector2.one, false, false);
            mfx = MakeMesh(Vector2.one, true, false);
            mfy = MakeMesh(Vector2.one, false, true);
            mfxy = MakeMesh(Vector2.one, true, true);
        }
        return (flipX && flipY) ? mfxy : flipX ? mfx : flipY ? mfy : m;
    }

    private static Mesh MakeMesh(Vector2 size, bool flipX, bool flipY)
    {
        var normal = new Vector2[]
        {
            new Vector2(0, 0),// Bottom-left
            new Vector2(0, 1),// Top-left
            new Vector2(1, 1),// Top-right
            new Vector2(1, 0) // Bottom-right
        };
        var fx = new Vector2[]
        {
            new Vector2(1, 0),// Bottom-left
            new Vector2(1, 1),// Top-left
            new Vector2(0, 1),// Top-right
            new Vector2(0, 0) // Bottom-right
        };
        var fy = new Vector2[]
        {
            new Vector2(0, 0), // Bottom-left
            new Vector2(0, -1),// Top-left
            new Vector2(1, -1),// Top-right
            new Vector2(1, 0)  // Bottom-right
        };
        var fxy = new Vector2[]
        {
            new Vector2(1, 0), // Bottom-left
            new Vector2(1, -1),// Top-left
            new Vector2(0, -1),// Top-right
            new Vector2(0, 0)  // Bottom-right
        };

        Vector3[] verts = new Vector3[4];
        int[] tris = new int[6];
        verts[0] = new Vector3(-0.5f * size.x, 0f, -0.5f * size.y); // Bottom-left
        verts[1] = new Vector3(-0.5f * size.x, 0f, 0.5f * size.y);  // Top-left
        verts[2] = new Vector3(0.5f * size.x, 0f, 0.5f * size.y);   // Top-right
        verts[3] = new Vector3(0.5f * size.x, 0f, -0.5f * size.y);  // Bottom-right
        tris[0] = 0;
        tris[1] = 1;
        tris[2] = 2;
        tris[3] = 0;
        tris[4] = 2;
        tris[5] = 3;
        Mesh mesh = new();
        mesh.name = $"AAM Mesh: {flipX}, {flipY}";
        mesh.vertices = verts;
        mesh.uv = (flipX && flipY) ? fxy : flipX ? fx : flipY ? fy : normal;
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    public static AnimData Load(string filePath, bool allowFromCache = true, bool saveToCache = true)
    {
        if (filePath == null)
            return null;

        if (allowFromCache)
        {
            if (cache.TryGetValue(filePath, out var found))
                return found;
        }

        var loaded = Load(File.ReadAllText(filePath));

        if (saveToCache)
            cache[filePath] = loaded;

        return loaded;
    }

    public static AnimData Load(string json)
    {
        var model = JsonConvert.DeserializeObject<AnimDataModel>(json, new JsonSerializerSettings());

        var data = new AnimData()
        {
            Name = model.Name,
            Duration = model.Length,
            Bounds = model.Bounds,
            ExportTimeUTC = model.ExportTimeUTC
        };

        // Make parts...
        var parts = new List<AnimPartData>();
        var idToPart = new Dictionary<int, AnimPartData>();
        int i = 0;
        foreach (var part in model.Parts)
        {
            var dp = new AnimPartData(part)
            {
                Path = part.Path,
                CustomName = part.CustomName,
                TexturePath = part.TexturePath,
                ID = part.ID,
                Index = i++,
                TransparentByDefault = part.TransparentByDefault
            };

            parts.Add(dp);
            idToPart.Add(dp.ID, dp);
        }

        // Part children, parent and split draw pivot.
        foreach (var part in model.Parts)
        {
            var dp = idToPart[part.ID];

            // Parent & children.
            if (part.ParentID != 0)
            {
                dp.Parent = idToPart[part.ParentID];
                dp.Parent.Children.Add(dp);
            }

            // Split draw pivot.
            if (part.SplitDrawPivotPartID != 0)
                dp.SplitDrawPivot = idToPart[part.SplitDrawPivotPartID];
        }

        // Make events.
        var events = new List<EventBase>();
        foreach (var e in model.Events)
        {
            var created = EventBase.CreateFromSaveData(e.Data);
            if (created == null)
            {
                Core.Error($"Failed to create EventBase from data '{e.Data}'");
                continue;
            }

            created.Time = e.Time;
            events.Add(created);
        }

        // Make sweep data.
        var sweeps = new Dictionary<AnimPartData, List<SweepPointCollection>>();
        foreach (var part in model.Parts)
        {
            var dp = idToPart[part.ID];
            foreach (var sweep in part.SweepPaths)
            {
                if (!sweeps.TryGetValue(dp, out var list))
                {
                    list = new List<SweepPointCollection>();
                    sweeps.Add(dp, list);
                }

                list.Add(new SweepPointCollection(sweep));
            }
        }

        data.parts = parts.ToArray();
        data.events = events.ToArray();
        data.sweeps = sweeps;

        return data;
    }

    public enum SplitDrawMode
    {
        None,
        Before,
        After,
        BeforeAndAfter,
    }

    #endregion

    public string Name { get; private set; }
    public float Duration { get; private set; }
    public Rect Bounds { get; private set; }
    public DateTime ExportTimeUTC { get; private set; }
    public IReadOnlyList<AnimPartData> Parts => parts;
    public IReadOnlyList<EventBase> Events => events;
    public int SweepDataCount => sweeps.Count;
    public IEnumerable<AnimPartData> PartsWithSweepData => sweeps.Keys;

    private AnimPartData[] parts;
    private EventBase[] events;
    private Dictionary<AnimPartData, List<SweepPointCollection>> sweeps;

    public AnimPartData GetPart(string name)
    {
        if (name == null)
            return null;

        foreach (var part in parts)
        {
            if (part.Name == name)
                return part;
        }
#if !UNITY_EDITOR
        Core.Warn($"Failed to find part called '{name}'");
#endif
        return null;
    }

    public IReadOnlyList<SweepPointCollection> GetSweepPaths(AnimPartData forPart)
    {
        if (forPart != null && sweeps.TryGetValue(forPart, out var found))
            return found;
        return Array.Empty<SweepPointCollection>();
    }

    public IEnumerable<EventBase> GetEventsInPeriod(Vector2 range)
    {
        foreach (var e in events)
        {
            if (e.IsInTimeWindow(range))
                yield return e;
        }
    }
}

public class AnimPartData
{
    private static AnimationCurve MakeConstantCurve(float value)
    {
        return new AnimationCurve(new Keyframe(0f, value)) { postWrapMode = WrapMode.ClampForever, preWrapMode = WrapMode.ClampForever };
    }

    public string Name => CustomName ?? Path;

    public string Path;
    public string CustomName;
    public string TexturePath;
    public int ID;
    public int Index;
    public bool TransparentByDefault;
    public List<AnimPartData> Children = new List<AnimPartData>();
    public AnimPartData Parent;
    public AnimPartData SplitDrawPivot;

    public readonly AnimationCurve PosX, PosY, PosZ;
    public readonly AnimationCurve RotX, RotY, RotZ;
    public readonly AnimationCurve ScaleX, ScaleY, ScaleZ;
    public readonly AnimationCurve DataA, DataB, DataC;
    public readonly AnimationCurve ColorR, ColorG, ColorB, ColorA;
    public readonly AnimationCurve FlipX, FlipY;
    public readonly AnimationCurve Active;
    public readonly AnimationCurve Direction;
    public readonly AnimationCurve SplitDrawModeCurve;
    public readonly AnimationCurve FrameIndex;

    public AnimPartData(AnimPartModel model)
    {
        AnimationCurve GetCurve(string prop)
        {
            // Try get active curve.
            if (model.Curves.TryGetValue(prop, out var found))
                return found.ToAnimationCurve();

            // Try get a default value curve.
            if (model.DefaultValues.TryGetValue(prop, out float def))
                return MakeConstantCurve(def);

            // Not found...
            return null;
        }

        // Is Active.
        Active = GetCurve("GameObject.m_IsActive");

        // Facing direction.
        Direction = GetCurve("PawnBody.Direction");

        // Position.
        PosX = GetCurve("Transform.m_LocalPosition.x");
        PosY = GetCurve("Transform.m_LocalPosition.y");
        PosZ = GetCurve("Transform.m_LocalPosition.z");

        // Rotation.
        RotX = GetCurve("Transform.localEulerAnglesRaw.x");
        RotY = GetCurve("Transform.localEulerAnglesRaw.y");
        RotZ = GetCurve("Transform.localEulerAnglesRaw.z");

        // Scale.
        ScaleX = GetCurve("Transform.m_LocalScale.x");
        ScaleY = GetCurve("Transform.m_LocalScale.y");
        ScaleZ = GetCurve("Transform.m_LocalScale.z");

        // Data.
        DataA = GetCurve("AnimatedPart.DataA");
        DataB = GetCurve("AnimatedPart.DataB");
        DataC = GetCurve("AnimatedPart.DataC");

        // Tint.
        ColorR = GetCurve("AnimatedPart.Tint.r");
        ColorG = GetCurve("AnimatedPart.Tint.g");
        ColorB = GetCurve("AnimatedPart.Tint.b");
        ColorA = GetCurve("AnimatedPart.Tint.a");

        // Flip.
        FlipX = GetCurve("AnimatedPart.FlipX");
        FlipY = GetCurve("AnimatedPart.FlipY");

        // Misc.
        SplitDrawModeCurve = GetCurve("AnimatedPart.SplitDrawMode");
        FrameIndex = GetCurve("AnimatedPart.FrameIndex");
    }

    public AnimPartSnapshot GetSnapshot(AnimRenderer renderer)
    {
        if (renderer == null)
            return default;
        return renderer.GetSnapshot(this);
    }
}

public struct AnimPartSnapshot
{
    public bool Valid => Part != null;
    public string TexturePath => Part?.TexturePath == null ? null : (Part.TexturePath + (FrameIndex > 0 ? FrameIndex.ToString() : null));
    public string PartName => Part?.Name;
    public float Depth => WorldMatrix.MultiplyPoint3x4(Vector3.zero).y;
    public AnimPartData SplitDrawPivot => Part?.SplitDrawPivot;
    public Color FinalColor
    {
        get
        {
            if (Renderer == null)
                return default;

            var ov = Renderer.GetOverride(this);
            if (ov.ColorOverride != default)
                return ov.ColorOverride;

            return Color * ov.ColorTint;
        }
    }

    public readonly AnimRenderer Renderer;
    public readonly AnimPartData Part;
    public float Time;

    public Vector3 LocalPosition;
    public Vector3 LocalScale;
    public Vector3 LocalRotation;
    public Color Color;
    public float DataA, DataB, DataC;
    public bool FlipX, FlipY;
    public bool Active;
    public Rot4 Direction;
    public int FrameIndex;
    public AnimData.SplitDrawMode SplitDrawMode;

    public Matrix4x4 LocalMatrix;
    public Matrix4x4 WorldMatrix;
    public Matrix4x4 WorldMatrixNoOverride;

    public AnimPartSnapshot(AnimPartData part, AnimRenderer renderer, float time)
    {
        Part = part ?? throw new ArgumentNullException(nameof(part));
        Renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        Time = time;

        LocalPosition = new Vector3(Eval(part.PosX, time), Eval(part.PosY, time), Eval(part.PosZ, time));
        LocalRotation = new Vector3(Eval(part.RotX, time), Eval(part.RotY, time), Eval(part.RotZ, time));
        LocalScale = new Vector3(Eval(part.ScaleX, time), Eval(part.ScaleY, time), Eval(part.ScaleZ, time));

        DataA = Eval(part.DataA, time);
        DataB = Eval(part.DataB, time);
        DataC = Eval(part.DataC, time);

        Color = new Color(Eval(part.ColorR, time), Eval(part.ColorG, time), Eval(part.ColorB, time), Eval(part.ColorA, time));

        FlipX = Eval(part.FlipX, time) >= 0.5f;
        FlipY = Eval(part.FlipY, time) >= 0.5f;

        Active = Eval(part.Active, time) >= 0.5f;
        SplitDrawMode = (AnimData.SplitDrawMode)(int)Eval(part.SplitDrawModeCurve, time);
        FrameIndex = (int)Eval(part.FrameIndex, time);

        Direction = new Rot4((byte)Eval(part.Direction, time));

        LocalMatrix = default;
        WorldMatrix = default;
        WorldMatrixNoOverride = default;
        UpdateLocalMatrix();
    }

    public readonly Vector3 GetWorldPosition(Vector3 localPos = default)
    {
        return (Renderer.RootTransform * WorldMatrix).MultiplyPoint3x4(localPos);
    }

    public readonly Vector3 GetWorldPositionNoOverride(Vector3 localPos = default)
    {
        return (Renderer.RootTransform * WorldMatrixNoOverride).MultiplyPoint3x4(localPos);
    }

    public readonly Rot4 GetWorldDirection()
    {
        // Should this respect flip?
        // Override flip too?
        bool mx = Renderer.MirrorHorizontal;
        bool my = Renderer.MirrorVertical;
        return Direction.AsInt switch
        {
            0 => my ? Rot4.South : Rot4.North, // North
            1 => mx ? Rot4.West : Rot4.East, // East
            2 => my ? Rot4.North : Rot4.South, // South
            3 => mx ? Rot4.East : Rot4.West, // West
            _ => throw new Exception("Invalid")
        };
    }

    public readonly float GetWorldRotation()
    {
        return (Renderer.RootTransform * WorldMatrix).rotation.eulerAngles.y;
    }

    public void UpdateLocalMatrix()
    {
        LocalMatrix = Matrix4x4.TRS(LocalPosition, Quaternion.Euler(LocalRotation), LocalScale);
    }

    private readonly Matrix4x4 MakeWorldMatrix()
    {
        if (Part?.Parent == null)
            return LocalMatrix;

        return Renderer.GetSnapshot(Part.Parent).MakeWorldMatrix() * LocalMatrix;
    }

    private readonly bool MakeHierarchyActive()
    {
        if (Part?.Parent == null)
            return Active;
        return Active && Renderer.GetSnapshot(Part.Parent).MakeHierarchyActive();
    }

    public void UpdateWorldMatrix(bool mirrorX, bool mirrorY)
    {
        var preProc  = Matrix4x4.Scale(new Vector3(mirrorX ? -1f : 1f, 1f, mirrorY ? -1f : 1f));
        var postProc = Matrix4x4.Scale(new Vector3(mirrorX ? -1f : 1f, 1f, mirrorY ? -1f : 1f));

        var ov = Renderer.GetOverride(this);
        bool fx = FlipX;
        if (ov.FlipX)
            fx = !fx;
        bool fy = FlipY;
        if (ov.FlipY)
            fy = !fy;

        Vector2 off = new(fx ? -ov.LocalOffset.x : ov.LocalOffset.x, fy ? -ov.LocalOffset.y : ov.LocalOffset.y);
        float offRot = fx ^ fy ? -ov.LocalRotation : ov.LocalRotation;
        var adjust = Matrix4x4.TRS(new Vector3(off.x, 0f, off.y), Quaternion.Euler(0, offRot, 0f), new Vector3(ov.LocalScaleFactor.x, 1f, ov.LocalScaleFactor.y));

        Active = MakeHierarchyActive();
        WorldMatrix = preProc * MakeWorldMatrix() * adjust * postProc;
        WorldMatrixNoOverride = preProc * MakeWorldMatrix() * postProc;
    }

    private static float Eval(AnimationCurve curve, float time, float fallback = 0f)
    {
        return curve?.Evaluate(time) ?? fallback;
    }

    public readonly override string ToString()
    {
        if (Part == null)
            return "<default-snapshot>";

        return $"[{Time:F2}s] {Part.Name}";
    }
}

public class AnimPartOverrideData
{
    public Texture2D Texture;
    public Material Material;
    public bool PreventDraw;
    public Vector2 LocalOffset;
    public float LocalRotation;
    public Vector2 LocalScaleFactor = Vector2.one;
    public Color ColorTint = Color.white;
    public Color ColorOverride;
    public bool FlipX, FlipY;
    public bool UseMPB = true;
    public bool UseDefaultTransparentMaterial;
    public PartRenderer CustomRenderer;
    public object UserData;
    public ItemTweakData TweakData;
    public Thing Weapon;
}

[Serializable]
public class SpaceRequirement
{
    #region Static functions

    public static IEnumerable<SpaceRequirement> GetAllMustBeClear(IEnumerable<SpaceRequirement> reqs)
    {
        foreach (var req in reqs)
        {
            if (req != null && req.Type == RequirementType.MustBeClear)
                yield return req;
        }
    }

    public static (SpaceRequirement start, SpaceRequirement end) GetPawnPositions(IEnumerable<SpaceRequirement> reqs, int pawnIndex)
    {
        SpaceRequirement start = null;
        SpaceRequirement end = null;

        foreach (var req in reqs)
        {
            if (req == null)
                continue;

            if (req.Type == RequirementType.PawnStart && req.PawnIndex == pawnIndex)
                start = req;
            if (req.Type == RequirementType.PawnEnd && req.PawnIndex == pawnIndex)
                end = req;
        }

        start ??= end;
        end ??= start;

        return (start, end);
    }

    public static void Write(SpaceRequirement space, BinaryWriter writer)
    {
        writer.Write((byte)space.Type);
        writer.Write(space.PawnIndex);
        writer.Write((sbyte)space.Area.xMin);
        writer.Write((sbyte)space.Area.yMin);
        writer.Write((byte)space.Area.width);
        writer.Write((byte)space.Area.height);
    }

    public static SpaceRequirement Read(BinaryReader reader)
    {
        var type = (RequirementType)reader.ReadByte();
        var index = reader.ReadByte();
        int x = reader.ReadSByte();
        int y = reader.ReadSByte();
        int w = reader.ReadByte();
        int h = reader.ReadByte();

        return new SpaceRequirement()
        {
            Type = type,
            PawnIndex = index,
            Area = new RectInt(x, y, w, h)
        };
    }

    #endregion

    public enum RequirementType
    {
        MustBeClear,
        PawnStart,
        PawnEnd
    }

    public int CellCount => Mathf.Abs(Area.width * Area.height);
    public RectInt Area;
    public RequirementType Type;
    public byte PawnIndex;

    public void SetPoint(int x, int z)
    {
        Area = new RectInt(new Vector2Int(x, z), new Vector2Int(1, 1));
    }

    public (int x, int z) GetCell(bool fx, bool fy)
    {
        return GetCells(fx, fy).FirstOrDefault();
    }

    public IEnumerable<(int x, int z)> GetCells(bool fx, bool fy)
    {
        foreach (var cell in Area.allPositionsWithin)
        {
            var raw = Resolve(cell, fx, fy);
            yield return (raw.x, raw.y);
        }
    }

    private Vector2Int Resolve(Vector2Int vector, bool fx, bool fy)
    {
        if (fx)
            vector.x = -vector.x;

        if (fy)
            vector.y = -vector.y;

        return vector;
    }

    public void DrawGizmos()
    {
        if (CellCount == 0)
            return;

        Color c = Type switch
        {
            RequirementType.MustBeClear => Color.cyan,
            RequirementType.PawnStart => Color.green,
            RequirementType.PawnEnd => Color.red,
            _ => Color.white
        };
        float shrink = Type switch
        {
            RequirementType.MustBeClear => 0f,
            RequirementType.PawnStart => 0.1f,
            RequirementType.PawnEnd => 0.2f,
            _ => 1f
        };

        var center = new Vector3(Area.center.x - 0.5f, 0f, Area.center.y - 0.5f);
        var size = new Vector3(Area.size.x - shrink, 0f, Area.size.y - shrink);
        Gizmos.color = c;
        Gizmos.DrawWireCube(center, size);
    }
}

public class SweepPointCollection
{
    public int Count => points?.Length ?? 0;
    private readonly SweepPoint[] points;

    public SweepPointCollection(SweepPoint[] points)
    {
        this.points = points;
    }

    public SweepPoint[] CloneWithVelocities(float downDst, float upDst)
    {
        SweepPoint[] clone = new SweepPoint[points.Length];
        Array.Copy(points, clone, points.Length);

        Vector3 prevDown = default;
        Vector3 prevUp = default;
        float prevTime = 0;

        for (int i = 0; i < clone.Length; i++)
        {
            if(i != 0)
                clone[i].SetVelocity(downDst, upDst, prevDown, prevUp, prevTime);

            clone[i].GetEndPoints(downDst, upDst, out prevDown, out prevUp);
            prevTime = clone[i].Time;
        }

        return clone;
    }
}
