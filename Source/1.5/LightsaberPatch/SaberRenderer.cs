using SWSaber;
using UnityEngine;
using Verse;

namespace AM.LightsaberPatch;

public class SaberRenderer : PartRenderer
{
    public override bool Draw()
    {
        // Draw lightsaber base.
        Graphics.DrawMesh(Mesh, TRS, Material, 0);

        var weapon = Item;
        if (weapon == null)
            return true;

        var saberMat = weapon.GetComp<CompLightsaberActivatableEffect>()?.Graphic?.MatSingle;
        if (saberMat == null)
            return true;

        float lenFactor = Rand.Range(0.99f, 1.01f);
        Vector3 scale = new Vector3(lenFactor, 1, 1);
        var scaleMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, scale);
        Graphics.DrawMesh(Mesh, TRS * scaleMatrix, saberMat, 0);

        return true;
    }
}