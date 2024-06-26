﻿#if UNITY_EDITOR
using Assets.Editor;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AnimationDataCreator))]
public class EditorRenderer : Editor
{
    private AnimationWindow window;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        AnimationDataCreator t = (AnimationDataCreator)target;

        if (GUILayout.Button("Clear texture cache"))
        {
            t.ClearTextureCache();
        }

        window ??= EditorWindow.GetWindow<AnimationWindow>("Animation", false);
        if (window?.animationClip != null)
            t.Clip = window.animationClip;

        if (t.Clip != null && window != null)
        {
            if (GUILayout.Button("Calculate sweeps"))
            {
                PopulateSweeps(t.Clip, t);
            }

            if (GUILayout.Button("Save to file."))
            {
                EditorCoroutineUtility.StartCoroutine(SaveCoroutine(t, t.Clip), this);
            }

            if (GUILayout.Button("Save ALL to file."))
            {
                EditorCoroutineUtility.StartCoroutine(SaveAllCoroutine(t), this);
            }

            if (t.AllowLoadingFromJson && GUILayout.Button("Load Current From Json"))
            {
                var clip = t.Clip;
                string FILE = @$"../..\Animations\{clip.name}.json";

                var settings = new JsonSerializerSettings();
                settings.Converters.Add(new RectConverter());
                settings.DefaultValueHandling = DefaultValueHandling.Ignore;
                var loaded = JsonConvert.DeserializeObject<AnimDataModel>(File.ReadAllText(FILE), settings);

                foreach (var part in loaded.Parts)
                {
                    foreach (var pair in part.Curves)
                    {
                        string[] parts = pair.Key.Split('.');
                        Type propType = GetType(parts[0]);
                        string prop = pair.Key[(pair.Key.IndexOf('.') + 1)..];
                        var binding = new EditorCurveBinding
                        {
                            path = part.Path,
                            propertyName = prop,
                            type = propType
                        };

                        Debug.Log($"Path: '{binding.path}', prop name: '{prop}'");

                        Type fieldType = binding.type.GetField(binding.propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.FieldType
                                        ?? binding.type.GetProperty(binding.propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.PropertyType;

                        if (fieldType is { IsEnum: true })
                        {
                            for (int i = 0; i < pair.Value.Keyframes.Length; i++)
                            {
                                var key = pair.Value.Keyframes[i];
                                key.value = BitConverter.ToSingle(BitConverter.GetBytes((int)key.value));
                                pair.Value.Keyframes[i] = key;
                            }
                        }
                        AnimationUtility.SetEditorCurve(clip, binding, pair.Value.ToAnimationCurve());
                    }
                }
            }

            if (!t.InspectAllCurves)
                return;

            var bindings = AnimationUtility.GetCurveBindings(t.Clip);
            foreach (var binding in bindings)
            {
                var curve = AnimationUtility.GetEditorCurve(t.Clip, binding);
                GUILayout.Label($"Prop: {binding.type.Name}.{binding.propertyName} ({binding.path})");
                EditorGUILayout.CurveField(curve);
            }
        }
    }

    private static Type GetType(string name)
    {
        foreach (var ass in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = ass.GetType(name, false);
            if (t != null)
                return t;
        }
        foreach (var t in AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()))
        {
            if (t.Name == name)
                return t;
        }
        return null;
    }

    private IEnumerator SaveCoroutine(AnimationDataCreator t, AnimationClip clip)
    {
        if (!EnsureSaveLocation())
        {
            Debug.LogWarning("Export cancelled.");
            yield break;
        }

        yield return null;

        window.recording = false;
        window.time = 0;
        window.animationClip = clip;
        window.previewing = true;
        window.Repaint();

        yield return null;

        var bounds = Save(t, t.Clip);
        t.MaxBounds = bounds;
    }

    private IEnumerator SaveAllCoroutine(AnimationDataCreator t)
    {
        if (!EnsureSaveLocation())
        {
            Debug.LogWarning("Export cancelled.");
            yield break;
        }

        yield return null;

        var controller = t.GetComponent<Animator>();
        var clips = controller.runtimeAnimatorController.animationClips;

        foreach (var clip in clips)
        {
            window.recording = false;
            window.time = 0;
            window.animationClip = clip;
            window.previewing = true;
            window.Repaint();

            yield return null;

            t.MaxBounds = Save(t, clip);

            //yield return null;
        }
    }

    private static bool EnsureSaveLocation()
    {
        if (IsValid())
            return true;

        EditorUtility.DisplayDialog("Set Output Folder", "You must pick an output folder for the animations.\nPlease select the 'Animations' folder in your mod.", "Ok");

        ExportHelper.ShowChangeExportLocationDialogue();
        return IsValid();

        static bool IsValid() => !string.IsNullOrEmpty(ExportHelper.OutputDirectoryPath) && Directory.Exists(ExportHelper.OutputDirectoryPath);
    }

    private Rect Save(AnimationDataCreator t, AnimationClip clip)
    {
        string fileName = $"{clip.name}.json";
        string file = Path.Combine(ExportHelper.OutputDirectoryPath, fileName);

        var bounds = PopulateSweeps(clip, t);
        string json = AnimData.Save(clip, t, bounds);

        File.WriteAllText(file, json);

        Debug.Log($"Wrote {clip.name} in {new FileInfo(file).Length / 1024f:F1} Kb to {new FileInfo(file).FullName}. Bounds: {bounds}");
        return bounds;
    }

    private Transform FindRecursive(Transform parent, string name)
    {
        if (parent.name == name)
            return parent;

        foreach (Transform child in parent)
        {
            var found = FindRecursive(child, name);
            if (found != null)
                return found;
        }

        return null;
    }

    private Rect PopulateSweeps(AnimationClip clip, AnimationDataCreator t)
    {
        var anim = t.GetComponent<Animator>();
        var anchors = t.SweepAnchors;
        var sw = new System.Diagnostics.Stopwatch();

        t.Sweeps.Clear();

        Rect bounds = default;
        void GetBounds(Rect b)
        {
            bounds = b;
        }

        foreach (var anchor in anchors)
        {
            var coll = new SweepPointCollection();
            sw.Start();

            foreach (var point in MakeSamples(clip, anim, anchor.transform, t, GetBounds))
                coll.Add(point);

            coll.EndAdd();

            sw.Stop();
            Debug.Log($"Generated {coll.Count} sweep points in {sw.Elapsed.TotalMilliseconds:F1}ms for {(anchor.ForPart == null ? anchor.gameObject.name : anchor.ForPart.name)} in {clip.name}.");
            t.Sweeps.Add(coll);
        }

        return bounds;
    }

    private IEnumerable<SweepPoint> MakeSamples(AnimationClip clip, Animator anim, Transform targ, AnimationDataCreator t, Action<Rect> bounds)
    {
        string tempName = targ.name;
        targ.name = "<SPECIAL>";

        var clone = Instantiate(anim);
        targ.name = tempName;
        clone.gameObject.SetActive(true);

        void Add(Transform trs)
        {
            var part = trs.GetComponent<AnimatedPart>();
            if (part != null && part.TexturePath.Length > 0)
            {
                SweepGenerator.AllPartsWithTexture.Add(part);
            }

            foreach (Transform sub in trs)
                Add(sub);
        }

        SweepGenerator.Root = clone.gameObject;
        SweepGenerator.AllPartsWithTexture.Clear();
        Add(clone.transform);
        SweepGenerator.Clip = clip;
        SweepGenerator.Target = FindRecursive(clone.transform, "<SPECIAL>");
        SweepGenerator.Target.name = tempName;
        SweepGenerator.Params = t.SweepParams;

        Vector4 edges = default;
        t.Points.Clear();

        static void Min(ref float x, in float x2)
        {
            if (x2 < x)
                x = x2;
        }
        static void Max(ref float x, in float x2)
        {
            if (x2 > x)
                x = x2;
        }
        foreach (var p in SweepGenerator.SampleAllCorners(0.01f))
        {
            Min(ref edges.x, p.x);
            Max(ref edges.z, p.x);

            Min(ref edges.y, p.z);
            Max(ref edges.w, p.z);
            //t.Points.Add(p);
        }

        foreach (var sp in SweepGenerator.MakeSamplesWithPP())
            yield return sp;

        bounds(new Rect(edges.x, edges.y, edges.z - edges.x, edges.w - edges.y));
        DestroyImmediate(clone.gameObject);
    }

    private void OnSceneGUI()
    {
        float time = window?.time ?? float.MaxValue;
        AnimationDataCreator t = (AnimationDataCreator)target;

        foreach (var collection in t.Sweeps)
        {
            int i = 0;
            foreach (var point in collection.Points)
            {
                if (!GetDrawData(collection, t, i++, time, point, out var color))
                    continue;

                point.GetEndPoints(1f, out var up, out var down);
                Handles.color = color;
                Handles.DrawLine(up, down);
            }
        }
    }

    private bool GetDrawData(SweepPointCollection collection, AnimationDataCreator t, int i, float windowTime, in SweepPoint point, out Color color)
    {
        var mode = t.SweepDisplayMode;
        //color = Color.Lerp(Color.green, Color.red, point.Velocity / 25f);
        color = Color.green;
        if (point.Disable)
            color = Color.cyan;

        switch (mode)
        {
            case AnimationDataCreator.DisplayMode.None:
                color = default;
                return false;

            case AnimationDataCreator.DisplayMode.All:
                return true;

            case AnimationDataCreator.DisplayMode.Previous:
                return point.Time <= windowTime;

            case AnimationDataCreator.DisplayMode.Ghost:
                if (point.Time < windowTime - t.SweepGhostTime)
                    return false;
                if (point.Time > windowTime)
                    return false;

                float p = Mathf.Clamp01((windowTime - point.Time) / t.SweepGhostTime);
                color.a = 1f - p;
                return true;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
#endif