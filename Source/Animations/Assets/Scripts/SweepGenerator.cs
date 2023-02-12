using System;
using System.Collections.Generic;
using UnityEngine;

public static class SweepGenerator
{
    public static readonly List<AnimatedPart> AllPartsWithTexture = new List<AnimatedPart>();
    public static GameObject Root;
    public static AnimationClip Clip;
    public static Transform Target;
    public static SweepParameters Params;

    public static IEnumerable<SweepPoint> MakeSamplesWithPP()
    {
        bool lastWasDisabled = false;
        float lastTime = -1;

        foreach (var point in MakeSamples())
        {
            if (lastTime < 0)
            {
                lastTime = point.Time;
                lastWasDisabled = point.Disable;
            }
            else
            {
                lastTime = point.Time;
            }

            if (point.Disable && lastWasDisabled)
                continue;

            yield return point;

            lastWasDisabled = point.Disable;
        }
    }

    public static IEnumerable<SweepPoint> MakeSamples()
    {
        float time = 0;
        float defaultLookahead = Params.BaseTimeStep;
        float lookahead = defaultLookahead;

        while (true)
        {
            if (time + lookahead > Clip.length)
                break;

            Sample(time, out var sa, out var sb, out _);
            var found = GetSweepPointAtDst(sa, sb, time, time + lookahead);
            if (found == null)
            {
                float old = lookahead;
                lookahead *= Params.TimeStepCoefficient;
                lookahead += Params.TimeStepOffset;

                if(lookahead >= 3f)
                    Debug.LogError($"Searching very far ahead: ({Mathf.RoundToInt(time * Clip.frameRate)}){time} + {old} -> {lookahead}");
                else if(lookahead >= 1f)
                    Debug.LogWarning($"Searching far ahead: ({Mathf.RoundToInt(time * Clip.frameRate)}){time} + {old} -> {lookahead}");
                continue;
            }

            yield return found.Value;
            time = found.Value.Time;
            lookahead = defaultLookahead;
        }
    }

    private static SweepPoint? GetSweepPointAtDst(in Vector3 absSA, in Vector3 absSB, float startTime, float endTime)
    {
        int i = 0;
        float tolerance = Params.TargetDistance * Params.TargetTolerance;

        while (true)
        {
            // Sample middle.
            float midTime = (startTime + endTime) * 0.5f;
            Sample(midTime, out var midA, out var midB, out var midSp);

            // Distance from start to middle.
            float midDst = MaxDst(absSA, midA, absSB, midB);

            // Is the distance to the middle within the tolerance?
            if (Mathf.Abs(midDst - Params.TargetDistance) <= tolerance)
            {
                //Debug.Log($"Found.");
                return midSp;
            }

            if (Mathf.Abs(midTime - endTime) < Params.QuitTime)
            {
                //Debug.LogWarning($"Quitting, not found.");
                return null;
            }

            // Is the middle distance larger or smaller than it should be?
            if (midDst > Params.TargetDistance)
            {
                // Correct time must be before the middle time.
                endTime = midTime;
                //Debug.Log("Moving left");
            }
            else
            {
                // Correct time must be after the middle time.
                startTime = midTime;
                //Debug.Log("Moving right");
            }

            // Don't kill the editor if it fails.
            if (++i != Params.MaxBinaryIterations)
                continue;

            return null;
        }
    }

    private static float MaxDst(in Vector3 a, in Vector3 a2, in Vector3 b, in Vector3 b2) => Mathf.Max(Dst(a, a2), Dst(b, b2));

    private static float Dst(in Vector3 a, in Vector3 a2) => new Vector2(a.x - a2.x, a.z - a2.z).magnitude;

    public static void Sample(float time, out Vector3 a, out Vector3 b, out SweepPoint sp)
    {
        Clip.SampleAnimation(Root, time);
        a = Target.position + Target.right * Params.Radius;
        b = Target.position - Target.right * Params.Radius;
        sp = new SweepPoint(time, Target.position, Target.right.x, Target.right.z, !Target.gameObject.activeInHierarchy);
    }

    public static IEnumerable<Vector3> SampleAllCorners(float res)
    {
        for (float t = 0f; t <= Clip.length; t += res)
        {
            Clip.SampleAnimation(Root, t);

            foreach (var p in AllPartsWithTexture)
            {
                if (!p.isActiveAndEnabled)
                    continue;

                var trs = p.MakeTrs(out _, out _);
                //yield return trs.MultiplyPoint3x4(default);
                yield return trs.MultiplyPoint3x4(new Vector3( 0.5f, 0f,  0.5f));
                yield return trs.MultiplyPoint3x4(new Vector3(0.5f, 0f, 0.5f));
                yield return trs.MultiplyPoint3x4(new Vector3(-0.5f, 0f, 0.5f));
                yield return trs.MultiplyPoint3x4(new Vector3(0.5f, 0f, -0.5f));
                yield return trs.MultiplyPoint3x4(new Vector3(-0.5f, 0f, -0.5f));
            }
        }
    }
}
