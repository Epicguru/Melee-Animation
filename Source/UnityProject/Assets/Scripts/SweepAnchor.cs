using Assets.Editor;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

[ExecuteAlways]
public class SweepAnchor : MonoBehaviour
{
    public GameObject ForPart;
    public Mesh Mesh;
    public float UpDst = 1f;
    public float DownDst = 1f;
    public bool MakeMesh = false;
    public MeshFilter Filter;
    public float GhostTime = 1f;

    private float[] speeds;
    private float[] times;

#if UNITY_EDITOR
    private AnimationWindow window;
    
    private void LateUpdate()
    {
        tag = ForPart != null ? "AnimIgnore" : "Untagged";

        if (MakeMesh)
        {
            MakeMesh = false;
            TryMakeMesh();
        }

        UpdateColors();
    }

    private void TryMakeMesh()
    {
        var comp = GetComponentInParent<AnimationDataCreator>();
        if (comp == null)
            return;

        int index = Array.IndexOf(comp.SweepAnchors, this);
        if (index < 0)
            return;

        var coll = comp.Sweeps[index];

        MakeMeshData(coll, DownDst, UpDst, out var points, out var tris, out speeds);
        Mesh = new Mesh();
        Mesh.vertices = points;
        Mesh.triangles = tris;
        
        Mesh.RecalculateNormals();

        if (Filter != null)
            Filter.mesh = Mesh;
    }

    private void UpdateColors()
    {
        if (speeds == null || Mesh == null)
            return;

        window ??= EditorWindow.GetWindow<AnimationWindow>("Animation", false);
        if (window == null || !window.previewing)
            return;

        float wt = window.time;
        Color low = Color.green;
        low.a = 0.0f;
        Color high = Color.red;
        high.a = 0.1f;

        Color[] colors = new Color[speeds.Length];
        for (int i = 0; i < speeds.Length; i++)
        {
            float speed = speeds[i];
            float time = times[i];

            Color col;
            if(time < wt - GhostTime)
            {
                col = default;
            }
            else if (time > wt)
            {
                col = default;
            }
            else
            {
                float p = Mathf.Clamp01((wt - time) / GhostTime);
                float speedComp = (speed / 25f);
                float alpha = p;
                col = new Color(alpha, speedComp, 0, 1);
            }

            colors[i] = col;
        }

        Mesh.colors = colors;
    }

    private void MakeMeshData(SweepPointCollection coll, float downDst, float upDst, out Vector3[] points, out int[] triangles, out float[] speeds)
    {
        points = new Vector3[coll.Count * 2];
        speeds = new float[coll.Count * 2];
        times = new float[coll.Count * 2];
        triangles = new int[(coll.Count - 1) * 6];
        Vector3 lastUp = default, lastDown = default;
        float lastTime = -1;

        for (int i = 0; i < coll.Count; i++)
        {
            var p = coll.Points[i];
            var up = new Vector3(p.X, 0, p.Z) + new Vector3(p.DX, 0, p.DZ) * upDst;
            var down = new Vector3(p.X, 0, p.Z) - new Vector3(p.DX, 0, p.DZ) * downDst;
            float velUp = 0, velDown = 0;

            if (lastTime < 0)
            {
                lastTime = p.Time;
            }
            else
            {
                var du = (lastUp - up);
                var dd = (lastDown - down);
                du.y = dd.y = 0;
                velUp = du.magnitude / (p.Time - lastTime);
                velDown = dd.magnitude / (p.Time - lastTime);
            }

            if (p.Disable)            
                lastTime = -1;            
            else            
                lastTime = p.Time;            

            float t = i / (coll.Count - 1f);

            up.y = (t) * 0.5f;
            down.y = (t) * 0.5f;

            points[i * 2] = up;
            points[i * 2 + 1] = down;
            times[i * 2] = p.Time;
            times[i * 2 + 1] = p.Time;

            if(p.Disable)            
                velUp = velDown = 0;            

            speeds[i * 2] = velUp;
            speeds[i * 2 + 1] = velDown;

            lastUp = up;
            lastDown = down;
        }

        for (int i = 0; i < triangles.Length / 6; i++)
        {
            int j = i * 6;
            int s = i * 2;

            triangles[j + 0] = s + 0;
            triangles[j + 1] = s + 3;
            triangles[j + 2] = s + 1;

            triangles[j + 3] = s + 0;
            triangles[j + 4] = s + 2;
            triangles[j + 5] = s + 3;
        }

        //Array.Reverse(triangles);
    }
#endif
}

