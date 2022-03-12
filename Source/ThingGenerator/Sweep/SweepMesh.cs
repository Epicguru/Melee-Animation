using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace AAM.Sweep
{
    public class SweepMesh : IDisposable
    {
        public readonly Mesh Mesh;

        private readonly List<Vector3> vertices = new List<Vector3>(512);
        private readonly List<Color> colors = new List<Color>(512);
        private readonly List<ushort> indices = new List<ushort>(512);
        private LineData? last;

        public SweepMesh()
        {
            Mesh = new Mesh();
        }

        public void AddLine(in Vector3 down, in Vector3 up, in Color downColor, in Color upColor)
        {
            var current = new LineData()
            {
                UpPos = up,
                DownPos = down,
                UpColor = upColor,
                DownColor = downColor
            };

            if (last != null)
                AddQuad(last.Value, current);

            last = current;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddQuad(in LineData last, in LineData current)
        {
            indices.Add((ushort)vertices.Count);
            vertices.Add(last.UpPos);
            colors.Add(last.UpColor);

            indices.Add((ushort)vertices.Count);
            vertices.Add(last.DownPos);
            colors.Add(last.DownColor);

            indices.Add((ushort)vertices.Count);
            vertices.Add(current.DownPos);
            colors.Add(current.DownColor);

            indices.Add((ushort)vertices.Count);
            vertices.Add(current.UpPos);
            colors.Add(current.UpColor);

            // TEMP: double sided by doubling geometry.
            indices.Add((ushort)vertices.Count);
            vertices.Add(current.UpPos);
            colors.Add(current.UpColor);

            indices.Add((ushort)vertices.Count);
            vertices.Add(current.DownPos);
            colors.Add(current.DownColor);

            indices.Add((ushort)vertices.Count);
            vertices.Add(last.DownPos);
            colors.Add(last.DownColor);

            indices.Add((ushort)vertices.Count);
            vertices.Add(last.UpPos);
            colors.Add(last.UpColor);
        }

        public void Rebuild()
        {
            Mesh.SetVertices(vertices);
            Mesh.SetIndices(indices, MeshTopology.Quads, 0);
            Mesh.SetColors(colors);
            Mesh.RecalculateNormals();
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(Mesh);
        }

        private struct LineData
        {
            public Color DownColor, UpColor;
            public Vector3 DownPos, UpPos;
        }
    }
}
