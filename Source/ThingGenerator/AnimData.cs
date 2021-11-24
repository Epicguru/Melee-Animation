using AAM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
#if !UNITY_EDITOR
using Verse;
#endif

public class AnimData
{
    #region Static Stuff

    private static Mesh m, mfx, mfy, mfxy;
    private static Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
    private static Dictionary<string, byte> propMap = new Dictionary<string, byte>()
    {
        { "m_LocalPosition.x", 1 },
        { "m_LocalPosition.y", 2 },
        { "m_LocalPosition.z", 3 },
        { "localEulerAnglesRaw.x", 4 },
        { "localEulerAnglesRaw.y", 5 },
        { "localEulerAnglesRaw.z", 6 },
        { "m_LocalScale.x", 7 },
        { "m_LocalScale.y", 8 },
        { "m_LocalScale.z", 9 },
        { "DataA", 1 },
        { "DataB", 2 },
        { "DataC", 3 },
        { "Tint.r", 4 },
        { "Tint.g", 5 },
        { "Tint.b", 6 },
        { "Tint.a", 7 },
        { "FlipX", 8 },
        { "FlipY", 9 },
    };

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

    public static Texture2D ResolveTexture(AnimPartData data)
    {
        if (data == null)
            return null;

        if (data.OverrideData.Texture != null)
            return data.OverrideData.Texture;

        if (data.TexturePath == null)
            return null;

        return ResolveTexture(data.TexturePath);
    }

    public static Texture2D ResolveTexture(string texturePath)
    {
        if (textureCache.TryGetValue(texturePath, out var found))
            return found;

        // Try load...
        Texture2D loaded;
#if UNITY_EDITOR
        loaded = Resources.Load<Texture2D>(texturePath);
#else
        loaded = Verse.ContentFinder<Texture2D>.Get(texturePath, false);
#endif
        textureCache.Add(texturePath, loaded);
        if (loaded == null)
            Debug.LogError($"Failed to load texture '{texturePath}'.");

        return loaded;
    }

#if UNITY_EDITOR
    private static byte EncodeType(Type t)
    {
        if (t == typeof(Transform))
            return 1;
        if (t == typeof(AnimatedPart))
            return 2;
        return 0;
    }
#endif

    private static byte EncodeField(string propName)
    {
        if (propMap.TryGetValue(propName, out var found))
            return found;
        return 0;
    }

    private static GameObject FindGO(GameObject root, string path)
    {
        var q = new Queue<string>(path.Split('/'));
        while (q.Count > 0)
        {
            root = root.transform.Find(q.Dequeue())?.gameObject;
            if (root == null)
                return null;
        }
        return root;
    }

    public static string MakeRelativePath(GameObject root, GameObject child)
    {
        if (child == root)
            return null;

        string parent = MakeRelativePath(root, child.transform.parent.gameObject);
        return $"{parent}{(parent == null ? "" : "/")}{child.name}";
    }

    private static IEnumerable<GameObject> EnumerateChildrenDeep(GameObject root)
    {
        if (root == null)
            yield break;

        foreach (Transform child in root.transform)
        {
            yield return child.gameObject;
            foreach (var subChild in EnumerateChildrenDeep(child.gameObject))
                yield return subChild.gameObject;
        }
    }

    private static int CountFalse(bool[][] array)
    {
        int count = 0;
        for (int i = 0; i < array.Length; i++)
        {
            for (int j = 0; j < array[i].Length; j++)
            {
                if (!array[i][j])
                    count++;
            }
        }
        return count;
    }

#if UNITY_EDITOR
    private static float GetDefaultValue(byte type, byte prop, GameObject go)
    {
        var trs = go.transform;
        var data = go.GetComponent<AnimatedPart>();

        switch (type)
        {
            case 1:
                return prop switch
                {
                    1 => trs.localPosition.x,
                    2 => trs.localPosition.y,
                    3 => trs.localPosition.z,
                    4 => trs.localEulerAngles.x,
                    5 => trs.localEulerAngles.y,
                    6 => trs.localEulerAngles.z,
                    7 => trs.localScale.x,
                    8 => trs.localScale.y,
                    9 => trs.localScale.z,
                    _ => 0
                };

            case 2:
                return prop switch
                {
                    1 => data?.DataA ?? 0,
                    2 => data?.DataB ?? 0,
                    3 => data?.DataC ?? 0,
                    4 => data?.Tint.r ?? 1,
                    5 => data?.Tint.g ?? 1,
                    6 => data?.Tint.b ?? 1,
                    7 => data?.Tint.a ?? 1,
                    8 => (data?.FlipX ?? false) ? 1 : 0,
                    9 => (data?.FlipY ?? false) ? 1 : 0,
                    _ => 0
                };

            default:
                Debug.LogError($"Invalid type {type}");
                return 0;
        }
    }
#endif

    private static void WriteCurve(BinaryWriter writer, AnimationCurve curve)
    {
        // Wrap modes.
        writer.Write((byte)curve.preWrapMode);
        writer.Write((byte)curve.postWrapMode);

        // Key count.
        writer.Write(curve.length);

        // Key data.
        for (int i = 0; i < curve.length; i++)
        {
            var key = curve.keys[i];

            writer.Write(key.time);
            writer.Write(key.value);

            writer.Write(key.inTangent);
            writer.Write(key.outTangent);

            writer.Write(key.inWeight);
            writer.Write(key.outWeight);
            writer.Write((byte)key.weightedMode);
        }
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
            Keyframe k = new Keyframe();

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

#if UNITY_EDITOR
    public static void Save(BinaryWriter writer, AnimationClip clip, GameObject animatorRoot, bool allowStrip, IEnumerable<SpaceRequirement> spaceReqs)
    {
        HashSet<string> paths = new HashSet<string>();

        var bindings = UnityEditor.AnimationUtility.GetCurveBindings(clip);
        int count = 0;
        foreach (var binding in bindings)
        {
            if (EncodeType(binding.type) != 0)
            {
                paths.Add(binding.path);
                count++;
            }
        }
        if (!allowStrip)
        {
            foreach (var go in EnumerateChildrenDeep(animatorRoot))
            {
                string path = MakeRelativePath(animatorRoot, go);
                if (paths.Add(path))
                    Debug.LogWarning($"Saved {path} from being stripped from the output.");
            }
        }

        var pathList = new List<string>(paths);

        // Name.
        writer.Write(clip.name);

        // Length.
        writer.Write(clip.length);

        // Part count.
        writer.Write(pathList.Count);

        // Animation events.
        var events = new List<AnimEvent>();
        foreach(var raw in clip.events)
        {
            if(raw.functionName != "DoAnimationEvent")
            {
                Debug.LogWarning($"Ignoring animation event for fuction '{raw.functionName}'");
                continue;
            }
            events.Add(new AnimEvent(raw.stringParameter, raw.time));
        }
        writer.Write(events.Count);
        foreach(var e in events)
        {
            writer.Write(e.Time);
            writer.Write(e.RawInput);
        }

        // Space requirements.
        var space = spaceReqs?.ToArray() ?? new SpaceRequirement[0];
        writer.Write(space.Length);
        foreach(var item in space)        
            SpaceRequirement.Write(item, writer);        

        // Object paths, parents & textures.
        foreach (var path in pathList)
        {
            var go = FindGO(animatorRoot, path);
            var parentGO = go.transform.parent.gameObject;
            Debug.Assert(go != null);
            Debug.Assert(parentGO != null);

            // Path.
            writer.Write(path);

            // Parent index.
            int parentIndex = parentGO == animatorRoot ? -1 : pathList.IndexOf(MakeRelativePath(animatorRoot, parentGO));
            writer.Write((short)parentIndex);

            var comp = go.GetComponent<AnimatedPart>();

            // Custom name.
            writer.Write(comp != null && comp.HasCustomName);
            if (comp != null && comp.HasCustomName)
                writer.Write(comp.CustomName);

            // Texture path.
            writer.Write(comp != null && comp.HasTexturePath);
            if (comp != null && comp.HasTexturePath)
                writer.Write(comp.TexturePath);
        }

        // Curve count.
        writer.Write(count);

        int leftOver = pathList.Count * (9 + 9);
        bool[][][] hasValue = new bool[pathList.Count][][];
        for (int i = 0; i < hasValue.Length; i++)
        {
            bool[][] temp = new bool[2][];
            temp[0] = new bool[9];
            temp[1] = new bool[9];
            hasValue[i] = temp;
        }

        // Write active curves.
        foreach (var binding in bindings)
        {
            if (EncodeType(binding.type) == 0)
                continue;

            var curve = UnityEditor.AnimationUtility.GetEditorCurve(clip, binding);
            byte type = EncodeType(binding.type);
            byte prop = EncodeField(binding.propertyName);
            int objIndex = pathList.IndexOf(binding.path);

            // Type.
            writer.Write(type);

            // Prop name.
            writer.Write(prop);

            // Part path.
            writer.Write((byte)objIndex);

            // Curve data.
            WriteCurve(writer, curve);

            hasValue[objIndex][type - 1][prop - 1] = true;
            leftOver--;
        }

        // Write default values.
        for (int i = 0; i < pathList.Count; i++)
        {
            writer.Write((byte)CountFalse(hasValue[i]));

            for (int j = 0; j < hasValue[i].Length; j++)
            {
                for (int k = 0; k < hasValue[i][j].Length; k++)
                {
                    if (hasValue[i][j][k])
                        continue;

                    // Get default value.
                    float def = GetDefaultValue((byte)(j + 1), (byte)(k + 1), FindGO(animatorRoot, pathList[i]));

                    // Write default value.
                    writer.Write((byte)(j + 1));
                    writer.Write((byte)(k + 1));
                    writer.Write(def);

                    leftOver--;
                }
            }
        }

        Debug.Assert(leftOver == 0);
    }
#endif

    public static AnimData Load(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open);
        using var reader = new BinaryReader(fs);
        return Load(reader);
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
        AnimEvent[] events = new AnimEvent[reader.ReadInt32()];
        for (int i = 0; i < events.Length; i++)
        {
            float time = reader.ReadSingle();
            string input = reader.ReadString();
            events[i] = new AnimEvent(input, time);
        }

        // Pawn positions.
        var space = new SpaceRequirement[reader.ReadInt32()];
        for (int i = 0; i < space.Length; i++)        
            space[i] = SpaceRequirement.Read(reader);        

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

            parts[i] = part;
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

        return new AnimData(clipName, length, parts, events, space);
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
        Mesh mesh = new Mesh();
        mesh.name = $"AAM Mesh: {flipX}, {flipY}";
        mesh.vertices = verts;
        mesh.uv = (flipX && flipY) ? fxy : flipX ? fx : flipY ? fy : normal;
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    #endregion

    public readonly string Name;
    public readonly float Duration;
    public float CurrentTime { get; private set; } = -1;

    public IReadOnlyList<AnimPartData> Parts => parts;
    public IReadOnlyList<AnimEvent> Events => events;
    public IReadOnlyList<AnimSection> Sections => sections;
    public AnimSection CurrentSection { get; private set; }
    public IEnumerable<AnimPartSnapshot> CurrentSnapshots
    {
        get
        {
            foreach (var part in parts)
                yield return part.CurrentSnapshot;
        }
    }

    private AnimPartData[] parts;
    private AnimEvent[] events;
    private SpaceRequirement[] spaceReqs;
    private AnimSection[] sections;

    public AnimData(string name, float duration, AnimPartData[] parts, AnimEvent[] events, SpaceRequirement[] spaceReqs)
    {
        this.Name = name;
        this.Duration = duration;
        this.parts = parts ?? new AnimPartData[0];
        this.events = events ?? new AnimEvent[0];
        this.spaceReqs = spaceReqs ?? new SpaceRequirement[0];
        this.sections = GenerateSections();

        Seek(0, null);
    }

    public AnimPartData GetPart(string name)
    {
        if (name == null)
            return null;

        foreach (var part in parts)
            if (part.Name == name)
                return part;

        return null;
    }

    public (SpaceRequirement start, SpaceRequirement end) GetPawnLocations(int pawnIndex, bool flipX, bool flipY)
    {
        return SpaceRequirement.GetPawnPositions(spaceReqs, pawnIndex);
    }

    public (int x, int z, int x2, int z2) GetPawnCells(int pawnIndex, bool flipX, bool flipY)
    {
        var pair = GetPawnLocations(pawnIndex, flipX, flipY);
        if (pair.start == null)
            return (0, 0, 0, 0);

        var cs = pair.start.GetCell(flipX, flipY);
        var ce = pair.end.GetCell(flipX, flipY);

        return (cs.x, cs.z, ce.x, ce.z);
    }

    public IEnumerable<(int x, int z)> GetSpaceRequirementCells(bool flipX, bool flipY, Func<SpaceRequirement, bool> selector = null)
    {
        foreach(var area in GetSpaceRequirements(selector))
        {
            foreach (var cell in area.GetCells(flipX, flipY))
                yield return cell;
        }
    }

    public IEnumerable<SpaceRequirement> GetSpaceRequirements(Func<SpaceRequirement, bool> selector = null)
    {
        if(selector == null)
        {
            foreach (var item in spaceReqs)
                yield return item;
        }
        else
        {
            foreach (var item in spaceReqs)
                if (selector(item))
                    yield return item;
        }
    }

    public IEnumerable<AnimPartData> GetPartsRegex(string regex)
    {
        var r = new Regex(regex, RegexOptions.IgnoreCase);

        foreach (var part in parts)
        {
            if (r.IsMatch(part.Name))
                yield return part;
        }
    }

    public int ModifyParts(string searchPattern, Action<AnimPartData> func)
    {
        if (func == null)
            return 0;

        int count = 0;
        foreach (var part in GetPartsRegex(searchPattern))
        {
            func(part);
            count++;
        }
        return count;
    }

    public void SortByDepth()
    {
        Array.Sort(parts, (a, b) => a.CurrentSnapshot.Depth.CompareTo(b.CurrentSnapshot.Depth));
    }

    public Vector2 Seek(float time, AnimRenderer renderer, bool sortByDepth = true, bool mirrorX = false, bool mirrorY = false, bool generateSectionEvents = true)
    {
        time = Mathf.Clamp(time, 0f, Duration);

        if (CurrentTime == time)
            return new Vector2(-1, -1);

        // Pass 1: Evaluate curves, make local matrices.
        for (int i = 0; i < parts.Length; i++)
            parts[i].CurrentSnapshot = new AnimPartSnapshot(parts[i], time);

        // Pass 2: Resolve world matrices using inheritance tree.
        for (int i = 0; i < parts.Length; i++)
            parts[i].CurrentSnapshot.UpdateWorldMatrix(mirrorX, mirrorY);


        if (sortByDepth)
            SortByDepth();

        float start = Mathf.Min(CurrentTime, time);
        float end   = Mathf.Max(CurrentTime, time);
        CurrentTime = time;

        if(CurrentSection == null)
        {
            CurrentSection = GetSectionAtTime(time);           
            CurrentSection.OnSectionEnter(renderer);
        }
        else if (!CurrentSection.ContainsTime(time))
        {
            var old = CurrentSection;
            CurrentSection = GetSectionAtTime(time);

            if (generateSectionEvents)
            {
                old.OnSectionExit(renderer);
                CurrentSection.OnSectionEnter(renderer);
            }            
        }

        return new Vector2(start, end);
    }

    public AnimSection GetSectionNamed(string name)
    {
        foreach(var section in sections)
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

    public IEnumerable<AnimEvent> GetEventsInPeriod(Vector2 range)
    {
        foreach (var e in events)
        {
            if (e.IsInTimeWindow(range))
                yield return e;
        }
    }

    protected virtual AnimSection[] GenerateSections()
    {
        if (events == null)
            return new AnimSection[] {new AnimSection(this, null, null)};

        var list = new List<AnimSection>();
        AnimEvent lastSection = null;
        foreach(var e in events)
        {
            if(e.HandlerName.ToLowerInvariant() == "section")
            {
                var sec = new AnimSection(this, lastSection, e);
                list.Add(sec);
                lastSection = e;
            }
        }
        list.Add(new AnimSection(this, lastSection, null));
        return list.ToArray();
    }

    public void Reset()
    {
        CurrentSection = null; // reset section. it is re-assinged in seek().
        Seek(0, null, false);
        foreach (var part in parts)
            part.Reset();
    }
}

public class AnimPartData
{
    private static AnimationCurve dummy;

    public string Name => CustomName ?? Path;
    public Texture2D Texture => resolvedTex ??= AnimData.ResolveTexture(this);

    public AnimPartData Parent;
    public string Path;
    public string CustomName;
    public string TexturePath;

    public AnimationCurve PosX, PosY, PosZ;
    public AnimationCurve RotX, RotY, RotZ;
    public AnimationCurve SclX, SclY, SclZ;
    public AnimationCurve DtaA, DtaB, DtaC;
    public AnimationCurve ColR, ColG, ColB, ColA;
    public AnimationCurve FlipX, FlipY;

    public AnimPartOverrideData OverrideData = new AnimPartOverrideData();
    public AnimPartSnapshot CurrentSnapshot;

    private Texture2D resolvedTex;

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

        return ref dummy; // Shut up compiler.
    }

    public virtual void PreDraw(Material mat, MaterialPropertyBlock pb)
    {

    }

    public void Reset()
    {
        resolvedTex = null;
        OverrideData.Reset();
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
            if (Part.OverrideData.ColorOverride != default)
                return Part.OverrideData.ColorOverride;

            return Color * Part.OverrideData.ColorTint;
        }
    }

    public AnimPartData Part;
    public float Time;

    public Vector3 LocalPosition;
    public Vector3 LocalScale;
    public Vector3 LocalRotation;
    public Color Color;
    public float DataA, DataB, DataC;
    public bool FlipX, FlipY;

    public Matrix4x4 LocalMatrix;
    public Matrix4x4 WorldMatrix;

    public AnimPartSnapshot(AnimPartData data, float time)
    {
        Part = data ?? throw new System.ArgumentNullException(nameof(data));
        Time = time;

        var d = data;
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

        LocalMatrix = default;
        WorldMatrix = default;
        UpdateLocalMatrix();
    }

    public Vector3 GetWorldPosition(Matrix4x4 transform, Vector3 localPos = default)
    {
        return (transform * WorldMatrix).MultiplyPoint3x4(localPos);
    }

    public float GetWorldRotation(Matrix4x4 transform)
    {
        return (transform * WorldMatrix).rotation.eulerAngles.y;
    }

    public void UpdateLocalMatrix()
    {
        LocalMatrix = Matrix4x4.TRS(LocalPosition, Quaternion.Euler(LocalRotation), LocalScale);
    }

    private Matrix4x4 MakeWorldMatrix()
    {
        if (Part?.Parent == null)
            return LocalMatrix;

        return Part.Parent.CurrentSnapshot.MakeWorldMatrix() * LocalMatrix;
    }

    public void UpdateWorldMatrix(bool mirrorX, bool mirrorY)
    {
        var preProc = Matrix4x4.Scale(new Vector3(mirrorX ? -1f : 1f, 1f, mirrorY ? -1f : 1f));
        var postProc = Matrix4x4.Scale(new Vector3(mirrorX ? -1f : 1f, 1f, mirrorY ? -1f : 1f));

        bool fx = FlipX;
        bool fy = FlipY;
        var ov = Part.OverrideData;

        Vector2 off = new Vector2(fx ? -ov.LocalOffset.x : ov.LocalOffset.x, fy ? -ov.LocalOffset.y : ov.LocalOffset.y);
        float offRot = fx ^ fy ? -ov.LocalRotation : ov.LocalRotation;
        var adjust = Matrix4x4.TRS(new Vector3(off.x, 0f, off.y), Quaternion.Euler(0, offRot, 0f), new Vector3(ov.LocalScaleFactor.x, 1f, ov.LocalScaleFactor.y));

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
    public bool PreventDraw;
    public Vector2 LocalOffset;
    public float LocalRotation;
    public Vector2 LocalScaleFactor = Vector2.one;
    public Material Material;
    public Color ColorTint = Color.white;
    public Color ColorOverride;

    public void Reset()
    {
        Texture = null;
        PreventDraw = false;
        LocalOffset = default;
        LocalRotation = 0f;
        LocalScaleFactor = Vector2.one;
        Material = null;
        ColorTint = Color.white;
        ColorOverride = default;
    }
}

public class AnimEvent
{
    public string HandlerName => parts.Length > 0 ? parts[0] : null;
    public readonly string RawInput;
    public readonly float Time;

    private string[] parts;

    public AnimEvent(string input, float time)
    {
        RawInput = input;
        Time = time;
        if (string.IsNullOrWhiteSpace(input))
            parts = new string[0];
        else
        {
            parts = input.Trim().Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = parts[i].Trim();            
            }      
        }
    }

    public string GetPartRaw(int argIndex)
    {
        if (argIndex < 0 || argIndex >= parts.Length - 1)
            return null;
        return parts[argIndex + 1];
    }

    public T TryParsePart<T>(int argIndex, object input = null, T fallback = default)
    {
        return TryParsePart(GetPartRaw(argIndex), input, fallback);
    }

    public T TryParsePart<T>(string part, object input = null, T fallback = default)
    {
        if (part == null)
            return fallback;

        if (typeof(T) == typeof(string))
            return (T)(object)part;

        if (typeof(T) == typeof(float))
        {
            if (float.TryParse(part, out var found))
                return (T)(object)found;
            else
                return fallback;
        }

        if (typeof(T) == typeof(int))
        {
            if (int.TryParse(part, out var found))
                return (T)(object)found;
            else
                return fallback;
        }

        if (typeof(T) == typeof(bool))
        {
            if (bool.TryParse(part, out var found))
                return (T)(object)found;
            else
                return fallback;
        }

#if !UNITY_EDITOR
        if(typeof(T) == typeof(Pawn))
        {
            var animator = input as AnimRenderer;
            if (animator == null)
                return fallback;

            if (int.TryParse(part, out var index) && index >= 0 && index < animator.Pawns.Length)
                return (T)(object)animator.Pawns[index];
            return fallback;
        }
        
        if(typeof(T) == typeof(AnimPartData))
        {
            var animator = input as AnimRenderer;
            if (animator == null)
                return fallback;
            return (T)(object)animator.GetPart(part) ?? fallback;
        }

        if(typeof(T).IsSubclassOf(typeof(Def)))        
            return ((T)GenGeneric.InvokeStaticMethodOnGenericType(typeof(DefDatabase<>), typeof(T), "GetNamed", part, true)) ?? fallback;

        if(typeof(T) == typeof(FloatRange))   
            return (T)(object)FloatRange.FromString(part);
#endif

#if UNITY_EDITOR
        Debug.LogError($"Cannot parse unknown type '{typeof(T).FullName}' from input string '{part}'");
#else
        Core.Error($"Cannot parse unknown type '{typeof(T).FullName}' from input string '{part}'");
#endif
        return fallback;
    }

    public bool IsInTimeWindow(Vector2 range)
    {
        return (Time == 0 ? Time >= range.x : Time > range.x) && Time <= range.y;
    }

    public override string ToString()
    {
        return $"[{Time:F2}] {RawInput}";
    }
}

[Serializable]
public class SpaceRequirement
{
    #region Static functions

    public static IEnumerable<SpaceRequirement> GetAllMustBeClear(IEnumerable<SpaceRequirement> reqs)
    {
        foreach(var req in reqs)
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
        foreach(var cell in Area.allPositionsWithin)
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

        var center = new Vector3(Area.center.x-0.5f, 0f, Area.center.y-0.5f);
        var size = new Vector3(Area.size.x - shrink, 0f, Area.size.y - shrink);
        Gizmos.color = c;
        Gizmos.DrawWireCube(center, size);
    }
}
