using System;
using AM.Data.Model;
using UnityEngine;

namespace AM.Sweep;

public class PartWithSweep
{
    public readonly AnimPartData Part;
    public readonly SweepPointCollection PointCollection;
    public readonly SweepMesh<Data> Mesh;
    public readonly AnimRenderer Renderer;
    public readonly float DownDst, UpDst;
    public bool MirrorHorizontal;
    public ISweepProvider ColorProvider;

    private readonly SweepPoint[] points;
    private float lastTime = -1;
    private int lastIndex = -1;

    public struct Data
    {
        public float Time;
        public float DownVel;
        public float UpVel;
    }

    public PartWithSweep(AnimRenderer renderer, AnimPartData part, SweepPointCollection pointCollection, SweepMesh<Data> mesh, ISweepProvider colorProvider, float upDst, float downDst)
    {
        Renderer = renderer;
        Part = part;
        PointCollection = pointCollection;
        Mesh = mesh;
        ColorProvider = colorProvider;
        UpDst = upDst;
        DownDst = downDst;
        points = pointCollection.CloneWithVelocities(DownDst, UpDst);
    }

    public void Draw(float time)
    {
        DrawInt(time);

        Graphics.DrawMesh(Mesh.Mesh, Renderer.RootTransform, AnimRenderer.DefaultTransparent, 0);
    }

    private bool DrawInt(float time)
    {
        if (Math.Abs(time - lastTime) < 0.0001f)
            return false;

        if (time < lastTime)
        {
            Rebuild(time);
            return true;
        }

        for (int i = lastIndex + 1; i < points.Length; i++)
        {
            var point = points[i];
            if (point.Time > time)
                break;

            point.GetEndPoints(DownDst, UpDst, out var down, out var up);

            if (MirrorHorizontal)
            {
                down.x *= -1;
                up.x *= -1;
            }

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

    private (Color low, Color high) MakeColors(in Data data)
    {
        return ColorProvider.GetTrailColors(new SweepProviderArgs()
        {
            DownVel = data.DownVel,
            UpVel = data.UpVel,
            LastTime = lastTime,
            Time = data.Time,
            Part = Part,
            Renderer = Renderer
        });
    }

    private void Rebuild(float upTo)
    {
        Mesh.Clear();
        for (int i = 0; i < points.Length; i++)
        {
            var point = points[i];
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

    private void AddInterpolatedPos(int lastIndex, float currentTime)
    {
        if (lastIndex < 0)
            return;
        if (lastIndex >= PointCollection.Count - 1)
            return; // Can't interpolate if we don't have the end.

        var lastPoint = points[lastIndex];
        if (Mathf.Abs(lastPoint.Time - currentTime) < 0.001f)
            return;

        var nextPoint = points[lastIndex + 1];

        float t = Mathf.InverseLerp(lastPoint.Time, nextPoint.Time, currentTime);
        var newPoint = SweepPoint.Lerp(lastPoint, nextPoint, t);
        newPoint.GetEndPoints(DownDst, UpDst, out var down, out var up);

        if (MirrorHorizontal)
        {
            down.x *= -1;
            up.x *= -1;
        }

        Mesh.AddLine(down, up, new Data()
        {
            Time = currentTime,
            UpVel = newPoint.VelocityTop,
            DownVel = newPoint.VelocityBottom
        });
    }
}