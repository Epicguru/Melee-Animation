using Assets.Editor;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

public class AnimData
{
    private static Mesh m, mfx, mfy, mfxy;

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

    private static float GetDefaultValue(string prop, GameObject go)
    {
        var trs = go.transform;
        var data = go.GetComponent<AnimatedPart>();
        var body = go.GetComponent<PawnBody>();

        return prop switch
        {
            // GameObject
            "GameObject.m_IsActive" => go.activeSelf ? 1 : 0,

            // Transform.
            "Transform.m_LocalPosition.x" => trs.localPosition.x,
            "Transform.m_LocalPosition.y" => trs.localPosition.y,
            "Transform.m_LocalPosition.z" => trs.localPosition.z,
            "Transform.localEulerAnglesRaw.x" => trs.localEulerAngles.x,
            "Transform.localEulerAnglesRaw.y" => trs.localEulerAngles.y,
            "Transform.localEulerAnglesRaw.z" => trs.localEulerAngles.z,
            "Transform.m_LocalScale.x" => trs.localScale.x,
            "Transform.m_LocalScale.y" => trs.localScale.y,
            "Transform.m_LocalScale.z" => trs.localScale.z,

            // AnimatedPart.cs
            "AnimatedPart.DataA" => data?.DataA ?? 0,
            "AnimatedPart.DataB" => data?.DataB ?? 0,
            "AnimatedPart.DataC" => data?.DataC ?? 0,
            "AnimatedPart.Tint.r" => data?.Tint.r ?? 0,
            "AnimatedPart.Tint.g" => data?.Tint.g ?? 0,
            "AnimatedPart.Tint.b" => data?.Tint.b ?? 0,
            "AnimatedPart.Tint.a" => data?.Tint.a ?? 0,
            "AnimatedPart.FlipX" => (data?.FlipX ?? false) ? 1 : 0,
            "AnimatedPart.FlipY" => (data?.FlipY ?? false) ? 1 : 0,
            "AnimatedPart.SplitDrawMode" => (int)(data?.SplitDrawMode ?? 0),
            "AnimatedPart.FrameIndex" => data?.FrameIndex ?? 0,

            // PawnBody.cs
            "PawnBody.Direction" => (int)(body?.Direction ?? 0),

            _ => float.NaN
        };
    }

    private static readonly HashSet<string> AllProps = new HashSet<string>()
    {
        // GameObject
        "GameObject.m_IsActive",

        // Transform.
        "Transform.m_LocalPosition.x",
        "Transform.m_LocalPosition.y",
        "Transform.m_LocalPosition.z",
        "Transform.localEulerAnglesRaw.x",
        "Transform.localEulerAnglesRaw.y",
        "Transform.localEulerAnglesRaw.z",
        "Transform.m_LocalScale.x",
        "Transform.m_LocalScale.y",
        "Transform.m_LocalScale.z",

        // AnimatedPart.cs
        "AnimatedPart.DataA",
        "AnimatedPart.DataB",
        "AnimatedPart.DataC",
        "AnimatedPart.Tint.r",
        "AnimatedPart.Tint.g",
        "AnimatedPart.Tint.b",
        "AnimatedPart.Tint.a",
        "AnimatedPart.FlipX",
        "AnimatedPart.FlipY",
        "AnimatedPart.SplitDrawMode",
        "AnimatedPart.FrameIndex",

        // PawnBody.cs
        "PawnBody.Direction"
    };

    public enum SplitDrawMode
    {
        None,
        Before,
        After,
        BeforeAndAfter,
    }

#if UNITY_EDITOR
    public static string Save(AnimationClip clip, AnimationDataCreator creator, Rect bounds)
    {
        var model = new AnimDataModel
        {
            ExportTimeUTC = DateTime.UtcNow,
            Length = clip.length,
            Name = clip.name,
            Bounds = bounds
        };

        // Events.
        foreach (var raw in clip.events)
        {
            if (raw.functionName != "AnimEvent")
            {
                Debug.LogWarning($"Ignoring animation event for function '{raw.functionName}'");
                continue;
            }

            var obj = raw.objectReferenceParameter as EventBase;
            if (obj == null)
            {
                Debug.LogWarning($"Null object in event at {raw.time}s.");
                continue;
            }

            model.Events.Add(new EventModel
            {
                Time = raw.time,
                Data = obj.MakeSaveData()
            });
        }

        var paths = new HashSet<string>();
        var bindings = AnimationUtility.GetCurveBindings(clip);
        foreach (var binding in bindings)
        {
            if (AllProps.Contains($"{binding.type.Name}.{binding.propertyName}"))
            {
                paths.Add(binding.path);
            }
            else
            {
                Debug.LogError($"Unexpected binding: {binding.propertyName} ({binding.type})");
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

        // Object paths, parents & textures.
        var parts = new List<AnimPartModel>();
        var bindingsWritten = new HashSet<string>();
        model.Parts = parts;
        foreach (var path in pathList)
        {
            bindingsWritten.Clear();

            var go = FindGO(animatorRoot, path);
            Debug.Assert(go != null, $"Failed to find {path}");
            var parentGO = go.transform.parent.gameObject;
            Debug.Assert(parentGO != null);

            var comp = go.GetComponent<AnimatedPart>();

            var part = new AnimPartModel()
            {
                ID = go.GetInstanceID(),
                ParentID = parentGO == null || parentGO == animatorRoot ? 0 : parentGO.GetInstanceID(),
                Path = path,
                CustomName = (comp?.HasCustomName ?? false) ? comp.CustomName : null,
                TexturePath = (comp?.HasTexturePath ?? false) ? comp.TexturePath : null,
                TransparentByDefault = comp?.TransparentByDefault ?? false,
                SplitDrawPivotPartID = comp?.SplitDrawPivot == null || parentGO == animatorRoot ? 0 : comp.SplitDrawPivot.gameObject.GetInstanceID()
            };

            // Curves.
            foreach (var binding in bindings)
            {
                if (binding.path != path)
                    continue;

                Type propType = binding.type.GetField(binding.propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.FieldType
                          ?? binding.type.GetProperty(binding.propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.PropertyType;
                string key = $"{binding.type.Name}.{binding.propertyName}";
                var animCurve = AnimationUtility.GetEditorCurve(clip, binding);
                var curveModel = CurveModel.FromAnimationCurve(animCurve);
                
                // UNITY BUG (in modern engine versions): AnimationCurves store enum data as floats, but do not correctly cast from int to float.
                // Instead, it simply converts the int bits into float bits i.e. float floatVar = *((float*)&intVar)
                // This breaks enum curves when exporting.
                if (propType is { IsEnum: true })
                {
                    for (int j = 0; j < curveModel.Keyframes.Length; j++)
                    {
                        var old = curveModel.Keyframes[j];
                        // See above for explanation (it's a Unity issue)
                        int asInt = BitConverter.ToInt32(BitConverter.GetBytes(old.value));
                        old.value = asInt;

                        curveModel.Keyframes[j] = old;
                    }
                }
                part.Curves.Add(key, curveModel);
                bindingsWritten.Add(key);
            }

            // Default values.
            foreach (var key in AllProps)
            {
                // No need to write default value if there is already an active curve.
                if (bindingsWritten.Contains(key))
                    continue;

                part.DefaultValues.Add(key, GetDefaultValue(key, go));
            }

            // Sweep curves.
            int i = -1;
            foreach (var anchor in creator.SweepAnchors)
            {
                i++;
                var forPart = anchor.ForPart == null ? anchor.gameObject : anchor.ForPart;
                if (forPart != go)
                    continue;

                part.SweepPaths.Add(creator.Sweeps[i].Points);
            }

            parts.Add(part);
        }

        // To json...
        var settings = new JsonSerializerSettings();
        settings.Converters.Add(new RectConverter());
        settings.DefaultValueHandling = DefaultValueHandling.Ignore;
        return JsonConvert.SerializeObject(model, Formatting.Indented, settings);
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
        mesh.name = $"AM Mesh: {flipX}, {flipY}";
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
    public int Count => Points?.Length ?? 0;
    public SweepPoint[] Points { get; private set; }

    private readonly List<SweepPoint> writePoints = new List<SweepPoint>();

    public void Add(in SweepPoint point)
    {
        writePoints.Add(point);
    }

    public void EndAdd()
    {
        Points = writePoints.ToArray();
        writePoints.Clear();
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

    public void GetEndPoints(float radius, out Vector3 up, out Vector3 down)
    {
        up = new Vector3(X, 0, Z) + new Vector3(DX, 0, DZ) * radius;
        down = new Vector3(X, 0, Z) - new Vector3(DX, 0, DZ) * radius;
    }
}
