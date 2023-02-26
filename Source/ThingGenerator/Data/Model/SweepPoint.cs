using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AAM.Data.Model;

[StructLayout(LayoutKind.Sequential)]
public struct SweepPoint
{
    public static SweepPoint Lerp(in SweepPoint a, in SweepPoint b, float t) => new SweepPoint
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
        up = new Vector3(X, 0, Z) + new Vector3(DX, 0, DZ) * upDst;
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