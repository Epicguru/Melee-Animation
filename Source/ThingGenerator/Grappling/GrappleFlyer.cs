using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace AM.Grappling;

/// <summary>
/// Closely based on Royalty's PawnJumper.
/// Look, I know Tynan doesn't like people taking DLC code and putting it in mods, but...
/// This is by far the most compatible and straightforward way of doing things.
/// There's no sense in re-inventing the wheel.
/// </summary>
public class GrappleFlyer : PawnFlyer
{
    public static GrappleFlyer MakeGrappleFlyer(Pawn grappler, Pawn victim, in IntVec3 targetPos)
    {
        if (victim.Position == targetPos || !victim.Spawned)
            return null;

        if (grappler.Map == null)
        {
            // I can't fathom how this is possible, but it happened once and it's not cool.
            Core.Error($"Null grappler ({grappler.LabelShortCap}) map!");
            return null;
        }

#if V13
        GrappleFlyer flyer = MakeFlyer(AM_DefOf.AM_GrappleFlyer, victim, targetPos) as GrappleFlyer;
#else
        GrappleFlyer flyer = MakeFlyer(AM_DefOf.AM_GrappleFlyer, victim, targetPos, null, null) as GrappleFlyer;
#endif
        if (flyer?.FlyingPawn != null)
        {
            victim.Rotation = Rot4.South;
            flyer.Grappler = grappler;
            GenSpawn.Spawn(flyer, targetPos, grappler.Map, WipeMode.Vanish);
            return flyer;
        }

        return null;
    }

    private static readonly Func<float, float> flightSpeed;
    private static readonly Func<float, float> flightCurveHeight = GenMath.InverseParabola;
    private static readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();

    public int TotalDurationTicks => ticksFlightTime;
    public Pawn Grappler;

    private Material cachedShadowMaterial;
    private Effecter flightEffecter = null;
    private int positionLastComputedTick = -1;
    private Vector3 groundPos;
    private Vector3 effectivePos;
    private float effectiveHeight;

    private Material ShadowMaterial
    {
        get
        {
            if (this.cachedShadowMaterial == null && !this.def.pawnFlyer.shadow.NullOrEmpty())
            {
                this.cachedShadowMaterial =
                    MaterialPool.MatFrom(this.def.pawnFlyer.shadow, ShaderDatabase.Transparent);
            }

            return this.cachedShadowMaterial;
        }
    }

    static GrappleFlyer()
    {
        AnimationCurve animationCurve = new();
        animationCurve.AddKey(0f, 0f);
        animationCurve.AddKey(0.1f, 0.15f);
        animationCurve.AddKey(1f, 1f);
        flightSpeed = animationCurve.Evaluate;
    }

    public override Vector3 DrawPos
    {
        get
        {
            this.RecomputePosition();
            return this.effectivePos;
        }
    }

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);
        if (respawningAfterLoad)
            return;

        if (Grappler == null)
        {
            Core.Error("Null grappler, cannot determine flight speed factor.");
            return;
        }

        // Adjust flight time based on grapple speed.
        float speed = Grappler.GetStatValue(AM_DefOf.AM_GrappleSpeed);
        ticksFlightTime = Mathf.Max(2, (int)(ticksFlightTime / (speed * Core.Settings.GrappleSpeed)));
    }

    private void RecomputePosition()
    {
        if (this.positionLastComputedTick == this.ticksFlying)
        {
            return;
        }

        this.positionLastComputedTick = this.ticksFlying;
        float arg = (float)this.ticksFlying / (float)this.ticksFlightTime;
        float num = flightSpeed(arg);
        this.effectiveHeight = flightCurveHeight(num) * Mathf.Clamp(ticksFlightTime / 60f * 0.5f, 0.3f, 2f);
        this.groundPos = Vector3.Lerp(this.startVec, base.DestinationPos, num);
        Vector3 a = new(0f, 0f, 2f);
        Vector3 b = Altitudes.AltIncVect * this.effectiveHeight;
        Vector3 b2 = a * this.effectiveHeight;
        this.effectivePos = this.groundPos + b + b2;
    }

    public override void DrawAt(Vector3 drawLoc, bool flip = false)
    {
        RecomputePosition();
        DrawShadow(groundPos, effectiveHeight);
        FlyingPawn.DrawAt(effectivePos, flip); // For Pawns, flip does nothing, it's just an inherited param.

        Color ropeColor = Grappler?.TryGetLasso()?.def.graphicData.color ?? Color.magenta;
        DrawBoundTexture(FlyingPawn, effectivePos, ropeColor);
        DrawGrappleLine(ropeColor);
    }

    public void DrawGrappleLine(Color ropeColor)
    {
        Vector3 from = Grappler.DrawPos;
        from += Grappler.Rotation.AsVector2.ToWorld() * 0.4f;
        from.y = AltitudeLayer.PawnUnused.AltitudeFor();

        Vector3 to = effectivePos;
        to.y = from.y;

        float bumpMag = Mathf.Clamp(flightCurveHeight(ticksFlying / 15f) * 1.25f, 0f, 1.25f);
        float bumpMag2 = Mathf.Clamp(flightCurveHeight(ticksFlying / 10f) * 0.25f, 0f, 0.25f);
        Vector3 bump = (Grappler.DrawPos - from).normalized;
        Vector3 bump2 = Vector2.Perpendicular(to.ToFlat() - Grappler.DrawPos.ToFlat()).normalized.ToWorld();
        bump.y = 0;
        from += bump * bumpMag + bump2 * bumpMag2;

        GrabUtility.DrawRopeFromTo(from, to, ropeColor);
    }

    public static void DrawBoundTexture(Pawn pawn, Vector3 drawLoc, Color ropeColor)
    {
        drawLoc.y += 0.05f;
        var tex = GrabUtility.GetBoundPawnTexture(pawn);
        if (tex == null)
        {
            if (pawn.RaceProps.Humanlike)
                tex = Content.BoundMaleRope;
            else
                return;
        }

        var mat = AnimRenderer.DefaultCutout;
        var trs = Matrix4x4.TRS(drawLoc, Quaternion.identity, Vector3.one * 1.5f * Core.GetBodyDrawSizeFactor(pawn)); // TODO use actual pawn size such as from alien races.

        mpb.SetTexture("_MainTex", tex); // TODO cache id.
        mpb.SetColor("_Color", ropeColor);

        Graphics.DrawMesh(MeshPool.plane10, trs, mat, 0, null, 0, mpb);
    }

    private void DrawShadow(Vector3 drawLoc, float height)
    {
        Material shadowMaterial = this.ShadowMaterial;
        if (shadowMaterial == null)
        {
            return;
        }
        float num = Mathf.Lerp(1f, 0.6f, height);
        Vector3 s = new(num, 1f, num);
        Matrix4x4 matrix = default(Matrix4x4);
        matrix.SetTRS(drawLoc, Quaternion.identity, s);
        Graphics.DrawMesh(MeshPool.plane10, matrix, shadowMaterial, 0);
    }

    public override void RespawnPawn()
    {
        FleckMaker.ThrowDustPuff(base.DestinationPos + Gen.RandomHorizontalVector(0.5f), base.Map, 2f);
        base.RespawnPawn();
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref Grappler, "AM_grappler", true);
    }

    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        Effecter effecter = this.flightEffecter;
        if (effecter != null)
        {
            effecter.Cleanup();
        }

        base.Destroy(mode);
    }
}