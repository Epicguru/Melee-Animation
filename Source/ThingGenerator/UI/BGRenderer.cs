using UnityEngine;
using Verse;

namespace AAM.UI;

public static class BGRenderer
{
    public static void DrawMainMenuBackground()
    {
        var texture = Content.BGSketch1;
        if (texture == null)
            return;

        var screenArea = new Rect(0, 0, Verse.UI.screenWidth, Verse.UI.screenHeight);
        Rect uv = new Rect(0, 0, 1, 1);

        var area = FitRect(texture, screenArea, 1f);

        Widgets.DrawTexturePart(area, uv, texture);
    }

    public static Rect FitRect(Texture tex, Rect area, float scale)
    {
        float w = tex.width;
        float h = tex.height;

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

        return new Rect(0, 0, w, h).CenteredOnXIn(area).CenteredOnYIn(area).ScaledBy(scale);
    }
}
