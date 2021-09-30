using System;
using UnityEngine;
using Verse;

namespace ThingGenerator
{
    [HotSwappable]
    public class GenericDialog : Window
    {
        public override Vector2 InitialSize => new Vector2(500, 150);

        public Action<Rect> Draw;

        public GenericDialog()
        {
            doCloseX = true;
            draggable = true;
            layer = WindowLayer.Super;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Draw(inRect);
        }
    }
}
