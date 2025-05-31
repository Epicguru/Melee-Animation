using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AM.Sweep
{
    public class SweepMesh<T> : IDisposable
    {
        public delegate (Color downColor, Color upColor) MakeColors(in T data);

        public readonly Mesh Mesh;

        private readonly List<Vector3> vertices = new(256);
        private readonly List<Color> colors = new(256);
        private readonly List<ushort> indices = new(256);
        private readonly List<(T data, int downIndex, int upIndex)> metaData = new(256);
        private LineData? last;
        private T lastT;

        public SweepMesh()
        {
            Mesh = new Mesh();
        }

        public void AddLine(in Vector3 down, in Vector3 up, in T metaData)
        {
            var current = new LineData()
            {
                UpPos = up,
                DownPos = down
            };

            if (last != null)
                AddQuad(last.Value, current, metaData);

            last = current;
            lastT = metaData;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddQuad(in LineData last, in LineData current, in T t)
        {
            indices.Add((ushort)vertices.Count);
            vertices.Add(last.UpPos);
            colors.Add(default);

            indices.Add((ushort)vertices.Count);
            vertices.Add(last.DownPos);
            colors.Add(default);
            metaData.Add((lastT, colors.Count - 1, colors.Count - 2));

            indices.Add((ushort)vertices.Count);
            vertices.Add(current.DownPos);
            colors.Add(default);

            indices.Add((ushort)vertices.Count);
            vertices.Add(current.UpPos);
            colors.Add(default);
            metaData.Add((t, colors.Count - 2, colors.Count - 1));

            // TEMP: double sided by doubling geometry.
            indices.Add((ushort)vertices.Count);
            vertices.Add(current.UpPos);
            colors.Add(default);

            indices.Add((ushort)vertices.Count);
            vertices.Add(current.DownPos);
            colors.Add(default);
            metaData.Add((t, colors.Count - 1, colors.Count - 2));

            indices.Add((ushort)vertices.Count);
            vertices.Add(last.DownPos);
            colors.Add(default);

            indices.Add((ushort)vertices.Count);
            vertices.Add(last.UpPos);
            colors.Add(default);
            metaData.Add((lastT, colors.Count - 2, colors.Count - 1));
        }

        public void Rebuild()
        {
            Mesh.SetVertices(vertices);
            Mesh.SetIndices(indices, MeshTopology.Quads, 0);
            Mesh.SetColors(colors);
            Mesh.RecalculateNormals();
        }

        public void UpdateColors(MakeColors function)
        {
            foreach ((T data, int downIndex, int upIndex) in metaData)
            {
                (Color downColor, Color upColor) = function(data);
                colors[downIndex] = downColor;
                colors[upIndex] = upColor;
            }
        }

        public void Clear()
        {
            vertices.Clear();
            colors.Clear();
            indices.Clear();
            metaData.Clear();
            last = null;
        }

        public void Dispose()
        {
            Object.Destroy(Mesh);
        }

        private struct LineData
        {
            public Vector3 DownPos, UpPos;
        }
    }
}
