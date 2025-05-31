using JetBrains.Annotations;
using SWSaber;
using UnityEngine;
using Verse;

namespace AM.LightsaberPatch;

[UsedImplicitly]
public class SaberRenderer : PartRenderer
{
    public override bool Draw()
    {
        // Draw lightsaber base.
        Graphics.DrawMesh(Mesh, TRS, MaterialWithoutSplitMode, 0);

        var weapon = Item;
        
        var compActivator = weapon?.GetComp<CompLightsaberActivatableEffect>();
        if (compActivator == null)
            return true;
        
        // If the lightsaber is not activated, do not draw the blade.
        if (!compActivator.IsActive())
            return true;
        
        var saberMat = compActivator.Graphic?.MatSingle;
        if (saberMat == null)
            return true;
        
        if (saberMat.mainTexture == null)
        {
            Core.Error("Lightsaber material has no main texture, cannot draw lightsaber blade.");
            return true;
        }
        
        float lenFactor = Rand.Range(0.99f, 1.01f);
        Vector3 scale = new Vector3(lenFactor, 1, 1);
        var scaleMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, scale);
        Graphics.DrawMesh(Mesh, TRS * scaleMatrix, saberMat, 0);
        
        return true;
    }
}