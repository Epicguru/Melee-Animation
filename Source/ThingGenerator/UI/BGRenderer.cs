using System;
using UnityEngine;
using Verse;

namespace AM.UI;

public static class BGRenderer
{
    private static readonly int errorCode = Guid.NewGuid().GetHashCode();

    [TweakValue("Melee Animation", 0f, 12f)]
    private static float BGFocusPoint = 4.5f;
    [TweakValue("Melee Animation", 1, 2)]
    private static float BGZoomLevel = 1.06f;
    private static Texture2D[] layers;

    public static void DrawMainMenuBackground()
    {
        try
        {
            layers ??= InitLayers();
            var screenArea = new Rect(0, 0, Verse.UI.screenWidth, Verse.UI.screenHeight);
            var mouseOffset = Verse.UI.MousePositionOnUIInverted - screenArea.center;
            var mouseOffsetNormalized = mouseOffset / (screenArea.size * 0.5f);


            var maxOffset = screenArea.size * (1f - BGZoomLevel) * 0.5f;

            for (int i = layers.Length - 1; i >= 0; i--)
            {
                var layer = layers[i];

                float diff = (i - BGFocusPoint) /
                             Mathf.Abs(Mathf.Max(BGFocusPoint, (layers.Length - 1) - BGFocusPoint));
                var pos = layer.FitRect(screenArea, ScaleMode.Cover, BGZoomLevel);
                pos.position += diff * mouseOffsetNormalized * maxOffset;

                GUI.color = Color.white;
                Widgets.DrawTexturePart(pos, new Rect(0, 0, 1, 1), layer);
            }
        }
        catch (Exception e)
        {
            Log.ErrorOnce($"Exception rendering melee animation background:\n{e}", errorCode);
        }
    }

    private static Texture2D[] InitLayers()
    {
        const int LAYER_COUNT = 13;

        var loaded = new Texture2D[LAYER_COUNT];
        for (int i = 0; i < LAYER_COUNT; i++)
            loaded[i] = ContentFinder<Texture2D>.Get($"AM/UI/BG/{i + 1}");
        
        return loaded;
    }

    public static Rect FitRect(this Texture tex, Rect area, ScaleMode mode, float scale = 1f)
    {
        var size = new Vector2(tex.width, tex.height);
        return FitRect(size, area, mode, scale);
    }

    public static Rect FitRect(this Vector2 texSize, Rect area, ScaleMode mode, float scale = 1f)
    {
        float w = texSize.x;
        float h = texSize.y;

        void Shrink()
        {
            if (w > area.width)
            {
                float inc = area.width / w;
                w = area.width;
                h *= inc;
            }
            if (h > area.height)
            {
                float inc = area.height / h;
                h = area.height;
                w *= inc;
            }
        }

        void Expand()
        {
            if (w < area.width)
            {
                float inc = area.width / w;
                w = area.width;
                h *= inc;
            }
            if (h < area.height)
            {
                float inc = area.height / h;
                h = area.height;
                w *= inc;
            }
        }

        switch (mode)
        {
            case ScaleMode.Expand:
                Expand();
                break;

            case ScaleMode.Shrink:
                Shrink();
                break;
            case ScaleMode.Cover:
                Shrink();
                Expand();
                break;
            case ScaleMode.Fit:
                Expand();
                Shrink();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }

        return new Rect(0, 0, w, h).CenteredOnXIn(area).CenteredOnYIn(area).ScaledBy(scale);
    }
}

public enum ScaleMode
{
    /// <summary>
    /// The texture expands until it covers the entire rect.
    /// If it is already larger than the rect nothing happens.
    /// </summary>
    Expand,

    /// <summary>
    /// The texture is shrunk until it fits entirely within the rect.
    /// If it is already smaller than the rect nothing happens.
    /// </summary>
    Shrink,

    /// <summary>
    /// The same as <see cref="Expand"/> but the texture is scaled such that it is as small as possible while still covering the entire rect.
    /// The texture may still expand beyond the rect.
    /// </summary>
    Cover,

    /// <summary>
    /// The texture is scaled such that it expands as large as possible within the rect
    /// without spilling outside the rect.
    /// </summary>
    Fit
}
