using RimWorld;
using System;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace AAM.Grappling
{
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

            GrappleFlyer flyer = PawnFlyer.MakeFlyer(AAM_DefOf.AAM_GrappleFlyer, victim, targetPos) as GrappleFlyer;
            if (flyer?.FlyingPawn != null)
            {
                victim.Rotation = Rot4.South;
                flyer.Grappler = grappler;
                GenSpawn.Spawn(flyer, targetPos, grappler.Map, WipeMode.Vanish);
                return flyer;
            }

            return null;
        }

        private static readonly Func<float, float> FlightSpeed;
        private static readonly Func<float, float> FlightCurveHeight = GenMath.InverseParabola;
        private static readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();

        public int TotalDurationTicks => ticksFlightTime;
        public Pawn Grappler;

        private Material cachedShadowMaterial;
        private Effecter flightEffecter;
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
            AnimationCurve animationCurve = new AnimationCurve();
            animationCurve.AddKey(0f, 0f);
            animationCurve.AddKey(0.1f, 0.15f);
            animationCurve.AddKey(1f, 1f);
            FlightSpeed = animationCurve.Evaluate;
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
            float speed = Grappler.GetStatValue(AAM_DefOf.AAM_GrappleSpeed);
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
            float num = FlightSpeed(arg);
            this.effectiveHeight = FlightCurveHeight(num) * Mathf.Clamp(ticksFlightTime / 60f * 0.5f, 0.3f, 2f);
            this.groundPos = Vector3.Lerp(this.startVec, base.DestinationPos, num);
            Vector3 a = new Vector3(0f, 0f, 2f);
            Vector3 b = Altitudes.AltIncVect * this.effectiveHeight;
            Vector3 b2 = a * this.effectiveHeight;
            this.effectivePos = this.groundPos + b + b2;
        }

        public override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            RecomputePosition();
            DrawShadow(groundPos, effectiveHeight);
            FlyingPawn.DrawAt(effectivePos, flip);

            Color ropeColor = Grappler?.TryGetLasso()?.def.graphicData.color ?? Color.magenta;
            DrawBoundTexture(effectivePos, flip, ropeColor);
            DrawGrappleLineNew(ropeColor);
        }

        public BezierCurve MakeGrappleCurve()
        {
            var curve = new BezierCurve();
            curve.P0 = Grappler.DrawPos.ToFlat();
            curve.P3 = effectivePos.ToFlat();

            Vector2 delta = curve.P3 - curve.P0;
            Vector2 perp = Vector2.Perpendicular(delta.normalized);
            if (curve.P3.x < curve.P0.x)
                perp = -perp;
            float dst = delta.magnitude;

            float t0 = 0.1f;
            float t1 = 0.12f;

            curve.P1 = Vector2.Lerp(curve.P0, curve.P3, t0) + perp * Mathf.Sin(dst * 0.5f) * Mathf.Clamp(dst * 0.25f, 2f, 12f);
            curve.P2 = Vector2.Lerp(curve.P0, curve.P3, t1) + perp * Mathf.Sin(dst * 0.5f) * -Mathf.Clamp(dst * 0.25f, 1f, 6f);

            return curve;
        }

        public void DrawGrappleLineNew(Color ropeColor)
        {
            Vector3 from = Grappler.DrawPos;
            from += Grappler.Rotation.AsVector2.ToWorld() * 0.4f;
            from.y = AltitudeLayer.PawnUnused.AltitudeFor();

            Vector3 to = effectivePos;
            to.y = from.y;

            float bumpMag = Mathf.Clamp(FlightCurveHeight(ticksFlying / 15f) * 1.25f, 0f, 1.25f);
            float bumpMag2 = Mathf.Clamp(FlightCurveHeight(ticksFlying / 10f) * 0.25f, 0f, 0.25f);
            Vector3 bump = (Grappler.DrawPos - from).normalized;
            Vector3 bump2 = Vector2.Perpendicular(to.ToFlat() - Grappler.DrawPos.ToFlat()).normalized.ToWorld();
            bump.y = 0;
            from += bump * bumpMag + bump2 * bumpMag2;


            GrabUtility.DrawRopeFromTo(from, to, ropeColor);
        }

        public void DrawGrappleLine()
        {
            var curve = MakeGrappleCurve();

            int resolution = (int)Mathf.Clamp(10f * Vector2.Distance(curve.P0, curve.P3), 50, 256);
            float y = AltitudeLayer.MoteOverhead.AltitudeFor();

            Vector2 lastPoint = default;
            for (int i = 0; i < resolution; i++)
            {
                float t = i / (resolution - 1f);
                Vector2 point = Bezier.Evaluate(t, curve.P0, curve.P1, curve.P2, curve.P3);

                if (i != 0)
                {
                    Vector3 a = lastPoint.ToWorld(y);
                    Vector3 b = point.ToWorld(y);
                    GenDraw.DrawLineBetween(a, b, SimpleColor.Blue, 0.2f);
                }

                lastPoint = point;
            }
        }

        public void DrawBoundTexture(Vector3 drawLoc, bool flip, Color ropeColor)
        {
            drawLoc.y += 0.0001f;
            var tex = GrabUtility.GetBoundPawnTexture(FlyingPawn);
            if (tex == null)
            {
                if (FlyingPawn.RaceProps.Humanlike)
                    tex = Content.BoundMaleRope;
                else
                    return;
            }

            var mat = AnimRenderer.DefaultCutout;
            var trs = Matrix4x4.TRS(drawLoc, Quaternion.identity, Vector3.one * 1.5f); // TODO use actual pawn size such as from alien races.

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
            Vector3 s = new Vector3(num, 1f, num);
            Matrix4x4 matrix = default(Matrix4x4);
            matrix.SetTRS(drawLoc, Quaternion.identity, s);
            Graphics.DrawMesh(MeshPool.plane10, matrix, shadowMaterial, 0);
        }

        protected override void RespawnPawn()
        {
            this.LandingEffects();
            base.RespawnPawn();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref Grappler, "AAM_grappler", true);
        }

        public override void Tick()
        {
            if (this.flightEffecter == null && this.def.pawnFlyer.flightEffecterDef != null)
            {
                this.flightEffecter = this.def.pawnFlyer.flightEffecterDef.Spawn();
                this.flightEffecter.Trigger(this, TargetInfo.Invalid);
            }
            else
            {
                Effecter effecter = this.flightEffecter;
                if (effecter != null)
                {
                    effecter.EffectTick(this, TargetInfo.Invalid);
                }
            }

            base.Tick();
        }

        private void LandingEffects()
        {
            if (this.def.pawnFlyer.soundLanding != null)
            {
                this.def.pawnFlyer.soundLanding.PlayOneShot(new TargetInfo(base.Position, base.Map, false));
            }

            FleckMaker.ThrowDustPuff(base.DestinationPos + Gen.RandomHorizontalVector(0.5f), base.Map, 2f);
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
}
