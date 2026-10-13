using UnityEngine;
using Verse;

namespace AM
{
    public static class Bezier
    {
        public static Vector2 Evaluate(float t, in Vector2 p0, in Vector2 p1, in Vector2 p2, in Vector2 p3)
        {
            // This hot garbage is from stack overflow and I really can't be arsed to clean it up.
            // It works, and that's enough.

            float t2 = t * t;
            float t3 = t * t * t;
            float u = 1 - t;
            float q3 = u * u * u;
            float q2 = 3f * t3 - 6f * t2 + 3f * t;
            float q1 = -3f * t3 + 3f * t2;
            return (p0 * q3 +
                    p1 * q2 +
                    p2 * q1 +
                    p3 * t3);
        }
    }

    public struct BezierCurve : IExposable
    {
        public Vector2 P0, P1, P2, P3;

        public BezierCurve(in Vector2 p0, in Vector2 p1, in Vector2 p2, in Vector2 p3)
        {
            P0 = p0;
            P1 = p1;
            P2 = p2;
            P3 = p3;
        }

        public Vector2 Evaluate(float t) => Bezier.Evaluate(t, P0, P1, P2, P3); 

        public void ExposeData()
        {
            Scribe_Values.Look(ref P0, nameof(P0));
            Scribe_Values.Look(ref P1, nameof(P1));
            Scribe_Values.Look(ref P2, nameof(P2));
            Scribe_Values.Look(ref P3, nameof(P3));
        }
    }
}
