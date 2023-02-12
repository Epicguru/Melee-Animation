using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Assets.Editor;
using UnityEditor;
using UnityEngine;

public class AnimData
{
    private static Mesh m, mfx, mfy, mfxy;
    private static Dictionary<string, byte> propMap = new Dictionary<string, byte>()
    {
        // Transform
        { "m_LocalPosition.x", 1 },
        { "m_LocalPosition.y", 2 },
        { "m_LocalPosition.z", 3 },
        { "localEulerAnglesRaw.x", 4 },
        { "localEulerAnglesRaw.y", 5 },
        { "localEulerAnglesRaw.z", 6 },
        { "m_LocalScale.x", 7 },
        { "m_LocalScale.y", 8 },
        { "m_LocalScale.z", 9 },

        // AnimatedPart.cs
        { "DataA", 1 },
        { "DataB", 2 },
        { "DataC", 3 },
        { "Tint.r", 4 },
        { "Tint.g", 5 },
        { "Tint.b", 6 },
        { "Tint.a", 7 },
        { "FlipX", 8 },
        { "FlipY", 9 },
        { "SplitDrawMode", 10 },
        { "FrameIndex", 11 },

        // PawnBody.cs
        { "m_IsActive", 1 },
        { "Direction", 1 }
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

    private static byte EncodeType(Type t)
    {
        if (t == typeof(Transform))
            return 1;
        if (t == typeof(AnimatedPart))
            return 2;
        if (t == typeof(GameObject))
            return 3;
        if (t == typeof(PawnBody))
            return 4;

        Debug.LogWarning($"Failed to encode type: {t.FullName}");
        return 0;
    }

    private static byte EncodeField(string propName)
    {
        if (propMap.TryGetValue(propName, out var found))
            return found;

        Debug.LogWarning($"Failed to encode field: {propName}");
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
            if (child.CompareTag("AnimIgnore"))
                continue;

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

    private static float GetDefaultValue(byte type, byte prop, GameObject go)
    {
        var trs = go.transform;
        var data = go.GetComponent<AnimatedPart>();
        var body = go.GetComponent<PawnBody>();

        switch (type)
        {
            // Transform
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
                    _ => throw new NotImplementedException()
                };

            // AnimatedPart.cs
            case 2:
                return prop switch
                {
                    1  => data?.DataA ?? 0,
                    2  => data?.DataB ?? 0,
                    3  => data?.DataC ?? 0,
                    4  => data?.Tint.r ?? 1,
                    5  => data?.Tint.g ?? 1,
                    6  => data?.Tint.b ?? 1,
                    7  => data?.Tint.a ?? 1,
                    8  => (data?.FlipX ?? false) ? 1 : 0,
                    9  => (data?.FlipY ?? false) ? 1 : 0,
                    10 => (int)(data?.SplitDrawMode ?? 0),
                    11 => data?.FrameIndex ?? 0,
                    _  => throw new NotImplementedException()
                };

            // GameObject
            case 3:
                return prop switch
                {
                    1 => go.activeSelf ? 1 : 0,
                    _ => throw new NotImplementedException()
                };

            // PawnBody.cs
            case 4:
                return prop switch
                {
                    1 => (int)(body?.Direction ?? 0),
                    _ => throw new NotImplementedException()
                };

            default:
                Debug.LogError($"Invalid type {type} (prop {prop}, GO {go})");
                return 0;
        }
    }

    private static void WriteCurve(BinaryWriter writer, AnimationCurve curve, Type curveDataType)
    {
        // UNITY BUG (in modern engine versions): AnimationCurves store enum data as floats, but do not correctly cast from int to float.
        // Instead, it simply converts the int bits into float bits i.e. float floatVar = *((float*)&intVar)
        // This breaks enum curves when exporting.
        bool fixEnumBug = curveDataType?.IsEnum ?? false;

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

            if (fixEnumBug)
            {
                // See above for explanation (it's a Unity issue)
                int asInt = BitConverter.ToInt32(BitConverter.GetBytes(key.value));
                writer.Write((float)asInt);
            }
            else
            {
                writer.Write(key.value);
            }

            writer.Write(key.inTangent);
            writer.Write(key.outTangent);

            writer.Write(key.inWeight);
            writer.Write(key.outWeight);
            writer.Write((byte)key.weightedMode);
        }
    }

    public enum SplitDrawMode
    {
        None,
        Before,
        After,
        BeforeAndAfter,
    }

#if UNITY_EDITOR
    public static void Save(BinaryWriter writer, AnimationClip clip, AnimationDataCreator creator, Rect bounds)
    {
        HashSet<string> paths = new HashSet<string>();

        var bindings = AnimationUtility.GetCurveBindings(clip);
        int count = 0;
        foreach (var binding in bindings)
        {
            if (EncodeType(binding.type) != 0)
            {
                paths.Add(binding.path);
                count++;
            }
            else
            {
                Debug.LogError($"Ignoring binding: {binding.propertyName} ({binding.type})");
            }
        }

        var animatorRoot = creator.gameObject;
        foreach (var go in EnumerateChildrenDeep(animatorRoot))
        {
            string path = MakeRelativePath(animatorRoot, go);
            if (paths.Add(path))
            {
                //Debug.LogWarning($"Saved {path} from being stripped from the output.");
            }
        }        

        var pathList = new List<string>(paths);

        // Format Version.
        writer.Write(0);

        // Name.
        writer.Write(clip.name);

        // Length.
        writer.Write(clip.length);

        // Part count.
        writer.Write(pathList.Count);

        // Bounds.
        writer.Write(bounds.x);
        writer.Write(bounds.y);
        writer.Write(bounds.width);
        writer.Write(bounds.height);

        // Animation events.
        var events = new List<(string data, float time)>();
        foreach (var raw in clip.events)
        {
            if (raw.functionName != "AnimEvent")
            {
                Debug.LogWarning($"Ignoring animation event for function '{raw.functionName}'");
                continue;
            }

            var obj = raw.objectReferenceParameter as EventBase;
            if(obj == null)
            {
                Debug.LogWarning($"Null object in event at {raw.time}s.");
                continue;
            }

            events.Add((obj.MakeSaveData(), raw.time));
        }
        writer.Write(events.Count);
        foreach (var e in events)
        {
            writer.Write(e.data);
            writer.Write(e.time);
        }

        // Object paths, parents & textures.
        foreach (var path in pathList)
        {
            var go = FindGO(animatorRoot, path);
            Debug.Assert(go != null);
            var parentGO = go.transform.parent.gameObject;
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

            // Use default transparency.
            writer.Write(comp?.TransparentByDefault ?? false);

            // Split draw mode and pivot.
            writer.Write(comp?.SplitDrawPivot != null);
            if (comp?.SplitDrawPivot != null)
            {
                int foundIndex = -1;
                foreach (var path2 in pathList)
                {
                    foundIndex++;
                    var go2 = FindGO(animatorRoot, path2);
                    if (go2.TryGetComponent<AnimatedPart>(out var found) && found == comp.SplitDrawPivot)
                    {
                        writer.Write(foundIndex);
                        break;
                    }
                }
            }
        }

        // Curve count.
        writer.Write(count);

        int leftOver = pathList.Count * propMap.Count;
        bool[][][] hasValue = new bool[pathList.Count][][];
        for (int i = 0; i < hasValue.Length; i++)
        {
            bool[][] temp = new bool[4][];
            temp[0] = new bool[9];  // Transform
            temp[1] = new bool[11]; // Data
            temp[2] = new bool[1];  // IsActive
            temp[3] = new bool[1];  // Direction
            Debug.Assert(temp.Select(arr => arr.Length).Sum() == propMap.Count);
            hasValue[i] = temp;
        }

        // Write active curves.
        foreach (var binding in bindings)
        {
            if (EncodeType(binding.type) == 0)
                continue;

            var curve = AnimationUtility.GetEditorCurve(clip, binding);
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
            Type propType = binding.type.GetField(binding.propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.FieldType;
            WriteCurve(writer, curve, propType);

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

        // Write sweeps.
        var anchors = creator.SweepAnchors;
        writer.Write(anchors.Length); // Count.
        for (int i = 0; i < anchors.Length; i++)
        {
            var anchor = anchors[i];
            var sweep = creator.Sweeps[i];

            // Find anim part that this is for.
            var go = anchor.ForPart == null ? anchor.gameObject : anchor.ForPart;

            int sweepObjIndex = pathList.IndexOf(MakeRelativePath(animatorRoot, go));
            if (sweepObjIndex < 0)
                throw new Exception("Failed to find sweep object in animation");

            // Sweep object index.
            writer.Write(sweepObjIndex);

            // Write data.
            sweep.Write(writer);
        }
    }

#endif

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
}

public class SweepPointCollection
{
    public int Count => points?.Length ?? 0;
    public IReadOnlyList<SweepPoint> Points => points ?? (IReadOnlyList<SweepPoint>)writePoints;

    private readonly List<SweepPoint> writePoints = new List<SweepPoint>();
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
        foreach (var p in points)
            p.Read(reader);
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
    public float Time;
    public float X, Z;
    public float DX, DZ;
    public bool Disable;

    public SweepPoint(float time, Vector3 position, float dx, float dz, bool disable = false)
    {
        Time = time;
        X = position.x;
        Z = position.z;
        DX = dx;
        DZ = dz;
        Disable = disable;
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

    public void GetEndPoints(float radius, out Vector3 up, out Vector3 down)
    {
        up = new Vector3(X, 0, Z) + new Vector3(DX, 0, DZ) * radius;
        down = new Vector3(X, 0, Z) - new Vector3(DX, 0, DZ) * radius;
    }
}
