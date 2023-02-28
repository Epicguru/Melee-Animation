using UnityEngine;
using Verse;

namespace AAM.UI;

public static class BGRenderer
{
    private class Layer
    {
        public Texture2D Texture;
        public Vector2 BaseOffset;
        public float Scale = 1f;
        public Vector2 ParallaxFactor;
        public Vector2 ParallaxLimits;
    }

    private static Layer[] layers;

    public static void DrawMainMenuBackground()
    {
        if (layers == null)
            InitLayers();

        var screenArea = new Rect(0, 0, Verse.UI.screenWidth, Verse.UI.screenHeight);
        DrawLayers(screenArea);
        //DrawEditor(new Rect(200, 200, 200, 1000));

        //var texture = Content.BGSketch1;
        //if (texture == null)
        //    return;

        //var screenArea = new Rect(0, 0, Verse.UI.screenWidth, Verse.UI.screenHeight);
        //Rect uv = new Rect(0, 0, 1, 1);

        //var area = FitRect(texture, screenArea, 1f);

        //Widgets.DrawTexturePart(area, uv, texture);
    }

    private static void InitLayers()
    {
        const int LAYER_COUNT = 9;
        layers = new Layer[LAYER_COUNT];

        for (int i = 0; i < LAYER_COUNT; i++)
        {
            string name = $"AAM/UI/BG/parallax ({i + 1})";
            var tex = ContentFinder<Texture2D>.Get(name);

            float diff = i - 6.5f;


            layers[i] = new Layer
            {
                Texture = tex,
                ParallaxFactor = new Vector2(diff * 0.15f, diff * 0.1f)
            };
        }
    }

    private static void DrawLayers(Rect screen)
    {
        var uv = new Rect(0, 0, 1, 1);
        var middle = screen.center;
        var mouseOffset = Verse.UI.MousePositionOnUIInverted - middle;


        for (int i = layers.Length - 1; i >= 0; i--)
        {
            var layer = layers[i];

            var fit = FitRect(layer.Texture, screen, layer.Scale * 1.1f, true);
            var offset = mouseOffset;
            fit.position += offset * 0.05f * layer.ParallaxFactor;

            Widgets.DrawTexturePart(fit, uv, layer.Texture);
        }
    }

    private static void DrawEditor(Rect screen)
    {
        var ui = new Listing_Standard();
        ui.Begin(screen);

        for (int i = layers.Length - 1; i >= 0; i--)
        {
            var layer = layers[i];

            ui.Label($"Layer {i}");
            layer.ParallaxFactor.x = ui.SliderLabeled("FX", layer.ParallaxFactor.x, -2f, 2f);
            layer.ParallaxFactor.y = ui.SliderLabeled("FY", layer.ParallaxFactor.y, -2f, 2f);
        }

        

        ui.End();
    }

    public static Rect FitRect(Texture tex, Rect area, float scale, bool shrink = false)
    {
        float w = tex.width;
        float h = tex.height;

        if (shrink)
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

        return new Rect(0, 0, w, h).CenteredOnXIn(area).CenteredOnYIn(area).ScaledBy(scale);
    }
}
