using System;
using UnityEngine;

namespace AM.Data.Model;

public sealed class CurveModel
{
    public static CurveModel FromAnimationCurve(AnimationCurve curve)
    {
        // Copy keys array to void modification later.
        var keys = new Keyframe[curve.keys.Length];
        Array.Copy(curve.keys, keys, keys.Length);

        return new CurveModel
        {
            Keyframes = keys,
            PreWrapMode = curve.preWrapMode,
            PostWrapMode = curve.postWrapMode
        };
    }

    public Keyframe[] Keyframes { get; set; }
    public WrapMode PreWrapMode { get; set; }
    public WrapMode PostWrapMode { get; set; }

    public AnimationCurve ToAnimationCurve()
    {
        return new AnimationCurve(Keyframes)
        {
            preWrapMode = PreWrapMode,
            postWrapMode = PostWrapMode
        };
    }
}
