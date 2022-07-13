using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AAM.Events;
using UnityEngine;
using System.Runtime.InteropServices;
#if !UNITY_EDITOR
using Verse;
using AAM;
#endif

public class AnimData
{
    #region Static Stuff

    private static Mesh m, mfx, mfy, mfxy;
    private static Dictionary<string, AnimData> cache = new();

    public static Mesh GetMesh(bool flipX, bool flipY)
    {
        if (true)
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
            new Vector2(0, 0),
            new Vector2(0, 1),
            new Vector2(1, 1),
            new Vector2(1, 0)
        };
        var fx = new Vector2[]
        {
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1),
            new Vector2(0, 0)
        };
        var fy = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(0, -1),
            new Vector2(1, -1),
            new Vector2(1, 0)
        };
        var fxy = new Vector2[]
        {
            new Vector2(1, 0),
            new Vector2(1, -1),
            new Vector2(0, -1),
            new Vector2(0, 0)
        };

        Vector3[] verts = new Vector3[4];
        int[] tris = new int[6];
        verts[0] = new Vector3(-0.5f * size.x, 0f, -0.5f * size.y);
        verts[1] = new Vector3(-0.5f * size.x, 0f, 0.5f * size.y);
        verts[2] = new Vector3(0.5f * size.x, 0f, 0.5f * size.y);
        verts[3] = new Vector3(0.5f * size.x, 0f, -0.5f * size.y);
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

    private static AnimationCurve ReadCurve(BinaryReader reader)
    {
        // Wrap modes.
        var preWrap = (WrapMode)reader.ReadByte();
        var postWrap = (WrapMode)reader.ReadByte();

        // Key count.
        int count = reader.ReadInt32();

        // Key data.
        var keys = new Keyframe[count];
        for (int i = 0; i < count; i++)
        {
            Keyframe k = new();

            k.time = reader.ReadSingle();
            k.value = reader.ReadSingle();

            k.inTangent = reader.ReadSingle();
            k.outTangent = reader.ReadSingle();

            k.inWeight = reader.ReadSingle();
            k.outWeight = reader.ReadSingle();
            k.weightedMode = (WeightedMode)reader.ReadByte();

            keys[i] = k;
        }

        return new AnimationCurve(keys) { preWrapMode = preWrap, postWrapMode = postWrap };
    }

    private static AnimationCurve MakeConstantCurve(float value)
    {
        return new AnimationCurve(new Keyframe(0f, value)) { postWrapMode = WrapMode.ClampForever, preWrapMode = WrapMode.ClampForever };
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

        using var fs = new FileStream(filePath, FileMode.Open);
        using var reader = new BinaryReader(fs);
        var loaded = Load(reader);

        if (saveToCache)
            cache[filePath] = loaded;

        return loaded;
    }

    public static AnimData Load(BinaryReader reader)
    {
        // Name.
        string clipName = reader.ReadString();

        // Length.
        float length = reader.ReadSingle();

        // Part count.
        int partCount = reader.ReadInt32();

        // Animation events.
        EventBase[] events = new EventBase[reader.ReadInt32()];
        for (int i = 0; i < events.Length; i++)
        {
            string saveData = reader.ReadString();
            float time = reader.ReadSingle();
            var e = EventBase.CreateFromSaveData(saveData);
            e.Time = time;
            events[i] = e;
        }

        AnimPartData[] parts = new AnimPartData[partCount];
        short[] parentIds = new short[partCount];

        // Initialize parts and read paths & texture paths.
        for (int i = 0; i < partCount; i++)
        {
            var part = new AnimPartData();

            // Path.
            part.Path = reader.ReadString();

            // Parent index, convert to path. Will need to be resolved later.
            parentIds[i] = reader.ReadInt16();

            // Custom name.
            if (reader.ReadBoolean())
                part.CustomName = reader.ReadString();

            // Texture.
            if (reader.ReadBoolean())
                part.TexturePath = reader.ReadString();

            // Default transparency.
            bool defaultTrs = reader.ReadBoolean();
            part.TransparentByDefault = defaultTrs;

            parts[i] = part;
            //Core.Log($"Read part {i}: {part.Path}, P:{parentIds[i]}, CN:{part.CustomName}, Tex:{part.TexturePath}");
        }

        // Assign parent references now that all parts are created.
        for (int i = 0; i < partCount; i++)
        {
            var part = parts[i];
            var parentIndex = parentIds[i];
            if (parentIndex < 0)
                continue;

            part.Parent = parts[parentIndex];
        }

        // Curve count.
        int curveCount = reader.ReadInt32();

        //Core.Log("Read curve count.");

        // Read curves and populate part data.
        for (int i = 0; i < curveCount; i++)
        {
            byte typeId = reader.ReadByte();
            byte fieldId = reader.ReadByte();
            AnimPartData part = parts[reader.ReadByte()];

            // Get a reference to this part's corresponding curve field.
            ref AnimationCurve curve = ref part.GetCurve(typeId, fieldId);

            // Assign that reference. This updates the field in the actual object. C# is cool like that sometimes.
            curve = ReadCurve(reader);
        }

        // Read default values.
        for (int i = 0; i < partCount; i++)
        {
            int defaultValueCount = reader.ReadByte();
            for (int j = 0; j < defaultValueCount; j++)
            {
                byte type = reader.ReadByte();
                byte prop = reader.ReadByte();
                float value = reader.ReadSingle();

                var constant = MakeConstantCurve(value);

                // Write default value as a constant 'curve'.
                ref AnimationCurve curve = ref parts[i].GetCurve(type, prop);
                curve = constant;
            }
        }

        // Read sweeps.
        var sweeps = new Dictionary<AnimPartData, List<SweepPointCollection>>();
        int sweepCount = reader.ReadInt32();
        for (int i = 0; i < sweepCount; i++)
        {
            // Parent index.
            int sweepObjIndex = reader.ReadInt32();
            var part = parts[sweepObjIndex];

            // Actual data.
            var sweep = new SweepPointCollection();
            sweep.Read(reader);

            if (!sweeps.TryGetValue(part, out var list))
            {
                list = new List<SweepPointCollection>();
                sweeps.Add(part, list);
            }
            list.Add(sweep);
        }

        return new AnimData(clipName, length, parts, events)
        {
            sweeps = sweeps
        };
    }
    
    #endregion

    public readonly string Name;
    public readonly float Duration;
    public IReadOnlyList<AnimPartData> Parts => parts;
    public IReadOnlyList<EventBase> Events => events;
    public IReadOnlyList<AnimSection> Sections => sections;
    public int SweepDataCount => sweeps.Count;
    public IEnumerable<AnimPartData> PartsWithSweepData => sweeps.Keys;

    private AnimPartData[] parts;
    private EventBase[] events;
    private AnimSection[] sections;
    private Dictionary<AnimPartData, List<SweepPointCollection>> sweeps;

    public AnimData(string name, float duration, AnimPartData[] parts, EventBase[] events)
    {
        this.Name = name;
        this.Duration = duration;
        this.parts = parts ?? Array.Empty<AnimPartData>();
        this.events = events ?? Array.Empty<EventBase>();
        this.sections = GenerateSections();

        for (int i = 0; i < parts.Length; i++)        
            parts[i].Index = i;

        for (int i = 0; i < this.events.Length; i++)
            this.events[i].Index = i;
    }

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

    public AnimSection GetSectionNamed(string name)
    {
        foreach (var section in sections)
        {
            if (section.Name == name)
                return section;
        }
        return null;
    }

    public AnimSection GetSectionAtTime(float time)
    {
        foreach (var section in sections)
        {
            if (section.ContainsTime(time))
                return section;
        }
        Core.Warn($"Didn't find any section for time {time}. Clip is {Duration}s with {sections.Length} sections.");
        return null;
    }

    protected virtual AnimSection[] GenerateSections()
    {
        // TODO re-implement or remove.
        //if (events == null)
        //    return new AnimSection[] { new AnimSection(this, null, null) };

        //var list = new List<AnimSection>();
        //EventBase lastSection = null;
        //foreach (var e in events)
        //{
        //    if (e.HandlerName.ToLowerInvariant() == "section")
        //    {
        //        var sec = new AnimSection(this, lastSection, e);
        //        list.Add(sec);
        //        lastSection = e;
        //    }
        //}
        //list.Add(new AnimSection(this, lastSection, null));
        return Array.Empty<AnimSection>();
    }
}

public class AnimPartData
{
    private static AnimationCurve dummy;

    public string Name => CustomName ?? Path;

    public AnimPartData Parent;
    public string Path;
    public string CustomName;
    public string TexturePath;
    public int Index;
    public bool TransparentByDefault;

    public AnimationCurve PosX, PosY, PosZ;
    public AnimationCurve RotX, RotY, RotZ;
    public AnimationCurve SclX, SclY, SclZ;
    public AnimationCurve DtaA, DtaB, DtaC;
    public AnimationCurve ColR, ColG, ColB, ColA;
    public AnimationCurve FlipX, FlipY;
    public AnimationCurve Active;
    public AnimationCurve Direction;

    public ref AnimationCurve GetCurve(byte type, byte field)
    {
        if (type == 0 || field == 0)
            return ref dummy;

        // TYPE: Transform
        if (type == 1)
        {
            // Position
            if (field == 1)
                return ref PosX;
            if (field == 2)
                return ref PosY;
            if (field == 3)
                return ref PosZ;

            // Rotation
            if (field == 4)
                return ref RotX;
            if (field == 5)
                return ref RotY;
            if (field == 6)
                return ref RotZ;

            // Scale
            if (field == 7)
                return ref SclX;
            if (field == 8)
                return ref SclY;
            if (field == 9)
                return ref SclZ;
        }

        // TYPE: Data
        if (type == 2)
        {
            if (field == 1)
                return ref DtaA;
            if (field == 2)
                return ref DtaB;
            if (field == 3)
                return ref DtaC;
            if (field == 4)
                return ref ColR;
            if (field == 5)
                return ref ColG;
            if (field == 6)
                return ref ColB;
            if (field == 7)
                return ref ColA;
            if (field == 8)
                return ref FlipX;
            if (field == 9)
                return ref FlipY;
        }

        // TYPE: GameObject
        if (type == 3)
        {
            if (field == 1)
                return ref Active;
        }

        // TYPE: Direction (PawnBody)
        if (type == 4)
        {
            if (field == 1)
                return ref Direction;
        }

        Core.Error($"Invalid curve ID: Type: {type}, Field: {field}");
        return ref dummy; // Shut up compiler.
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
    public string TexturePath => Part?.TexturePath;
    public string PartName => Part?.Name;
    public float Depth => WorldMatrix.MultiplyPoint3x4(Vector3.zero).y;
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
    public Rot4 Direction; // TODO populate.

    public Matrix4x4 LocalMatrix;
    public Matrix4x4 WorldMatrix;

    public AnimPartSnapshot(AnimPartData part, AnimRenderer renderer, float time)
    {
        Part = part ?? throw new ArgumentNullException(nameof(part));
        Renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        Time = time;

        var d = part;
        var t = time;

        LocalPosition = new Vector3(Eval(d.PosX, t), Eval(d.PosY, t), Eval(d.PosZ, t));
        LocalRotation = new Vector3(Eval(d.RotX, t), Eval(d.RotY, t), Eval(d.RotZ, t));
        LocalScale = new Vector3(Eval(d.SclX, t), Eval(d.SclY, t), Eval(d.SclZ, t));

        DataA = Eval(d.DtaA, t);
        DataB = Eval(d.DtaB, t);
        DataC = Eval(d.DtaC, t);

        Color = new Color(Eval(d.ColR, t), Eval(d.ColG, t), Eval(d.ColB, t), Eval(d.ColA, t));

        FlipX = Eval(d.FlipX, t) >= 0.5f;
        FlipY = Eval(d.FlipY, t) >= 0.5f;

        Active = Eval(d.Active, t) >= 0.5f;

        Direction = new Rot4((byte)Eval(d.Direction, t));

        LocalMatrix = default;
        WorldMatrix = default;
        UpdateLocalMatrix();
    }

    public Vector3 GetWorldPosition(Vector3 localPos = default)
    {
        return (Renderer.RootTransform * WorldMatrix).MultiplyPoint3x4(localPos);
    }

    public Rot4 GetWorldDirection()
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

    public float GetWorldRotation()
    {
        return (Renderer.RootTransform * WorldMatrix).rotation.eulerAngles.y;
    }

    public void UpdateLocalMatrix()
    {
        LocalMatrix = Matrix4x4.TRS(LocalPosition, Quaternion.Euler(LocalRotation), LocalScale);
    }

    private Matrix4x4 MakeWorldMatrix()
    {
        if (Part?.Parent == null)
            return LocalMatrix;

        return Renderer.GetSnapshot(Part.Parent).MakeWorldMatrix() * LocalMatrix;
    }

    private bool MakeHierarchyActive()
    {
        if (Part?.Parent == null)
            return Active;
        return Active && Renderer.GetSnapshot(Part.Parent).MakeHierarchyActive();
    }

    public void UpdateWorldMatrix(bool mirrorX, bool mirrorY)
    {
        var preProc = Matrix4x4.Scale(new Vector3(mirrorX ? -1f : 1f, 1f, mirrorY ? -1f : 1f));
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
    }

    private static float Eval(AnimationCurve curve, float time, float fallback = 0f)
    {
        return curve?.Evaluate(time) ?? fallback;
    }

    public override string ToString()
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
    public IReadOnlyList<SweepPoint> Points => points ?? (IReadOnlyList<SweepPoint>)writePoints;

    private readonly List<SweepPoint> writePoints = new();
    private SweepPoint[] points;
    private float currTime;
    private int currIndex;

    public void Add(in SweepPoint point)
    {
        writePoints.Add(point);
    }

    public void EndAdd()
    {
        points = writePoints.ToArray();
        writePoints.Clear();
    }

    public void Write(BinaryWriter writer)
    {
        // Length.
        writer.Write(points?.Length ?? 0);

        // Points.
        if (points != null)
        {
            foreach (var p in points)
                p.Write(writer);
        }
    }

    public void Read(BinaryReader reader)
    {
        int count = reader.ReadInt32();

        points = new SweepPoint[count];
        for(int i = 0; i < points.Length; i++)
            points[i].Read(reader);
    }

    public IEnumerable<SweepPoint> Seek(float newTime)
    {
        // Check for no movement.
        if (newTime == currTime)
            yield break;

        // Check rewind.
        bool rewind = newTime < currTime;
        if (rewind)
        {
            currTime = newTime;
            currIndex = GetIndexForTime(newTime);
            yield break;
        }

        // Moving forwards...
        int c = currIndex;
        while (points[c].Time < newTime)
        {
            yield return points[c];
            c++;
        }
        currTime = newTime;
        currIndex = c;
    }

    public void RecalculateVelocities(float downDst, float upDst)
    {
        Vector3 prevDown = default;
        Vector3 prevUp = default;
        float prevTime = 0;

        for (int i = 0; i < points.Length; i++)
        {
            if(i != 0)
                points[i].SetVelocity(downDst, upDst, prevDown, prevUp, prevTime);

            points[i].GetEndPoints(downDst, upDst, out prevDown, out prevUp);
            prevTime = points[i].Time;
        }
    }

    private int GetIndexForTime(float time)
    {
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i].Time > time)
                return i;
        }
        return points.Length - 1;
    }

    public SweepPointCollection Clone()
    {
        var created = new SweepPointCollection();
        created.points = new SweepPoint[points?.Length ?? 0];
        if (points != null)
            Array.Copy(points, created.points, points.Length);
        return created;
    }

    public void Clear()
    {
        writePoints.Clear();
        points = null;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct SweepPoint
{
    public static SweepPoint Lerp(in SweepPoint a, in SweepPoint b, float t)
        => new()
        {
            Time = Mathf.Lerp(a.Time, b.Time, t),
            X = Mathf.Lerp(a.X, b.X, t),
            Z = Mathf.Lerp(a.Z, b.Z, t),
            DX = Mathf.Lerp(a.DX, b.DX, t),
            DZ = Mathf.Lerp(a.DZ, b.DZ, t),
            VelocityTop = Mathf.Lerp(a.VelocityTop, b.VelocityTop, t),
            VelocityBottom = Mathf.Lerp(a.VelocityBottom, b.VelocityBottom, t),
            Disable = t >= 0.5f ? b.Disable : a.Disable
        };

    public float Time;
    public float X, Z;
    public float DX, DZ;
    public bool Disable;
    public float VelocityTop, VelocityBottom;

    public SweepPoint(float time, Vector3 position, float dx, float dz, bool disable = false)
    {
        Time = time;
        X = position.x;
        Z = position.z;
        DX = dx;
        DZ = dz;
        Disable = disable;
        VelocityTop = 0;
        VelocityBottom = 0;
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write(Time);
        writer.Write(X);
        writer.Write(Z);
        writer.Write(DX);
        writer.Write(DZ);
        writer.Write(Disable);
    }

    public void Read(BinaryReader reader)
    {
        Time = reader.ReadSingle();
        X = reader.ReadSingle();
        Z = reader.ReadSingle();
        DX = reader.ReadSingle();
        DZ = reader.ReadSingle();
        Disable = reader.ReadBoolean();
    }

    public void GetEndPoints(float downDst, float upDst, out Vector3 down, out Vector3 up)
    {
        down = new Vector3(X, 0, Z) + new Vector3(DX, 0, DZ) * downDst;
        up   = new Vector3(X, 0, Z) + new Vector3(DX, 0, DZ) * upDst;
    }

    public void SetZeroVelocity()
    {
        VelocityBottom = 0;
        VelocityTop = 0;
    }

    public void SetVelocity(float downDst, float upDst, Vector3 prevDown, Vector3 prevUp, float prevTime)
    {
        float timeDelta = this.Time - prevTime;
        if (timeDelta == 0)
            throw new Exception("Bad time delta.");

        GetEndPoints(downDst, upDst, out var down, out var up);

        downDst = Vector3.Distance(prevDown, down);
        upDst = Vector3.Distance(prevUp, up);

        VelocityBottom = downDst / timeDelta;
        VelocityTop = upDst / timeDelta;
    }
}

