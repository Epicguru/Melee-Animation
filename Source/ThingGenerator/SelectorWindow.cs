using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ThingGenerator
{
    [HotSwappable]
    public class SelectorWindow : Window
    {
        public delegate float DrawItemFunc(object item, Listing_Standard ui, SelectorWindow window);

        public static void Open(string title, IEnumerable<object> options, DrawItemFunc draw)
        {
            var window = new SelectorWindow(title);
            window.Options = options;
            window.DrawItem = draw;
            window.title = title;
            Find.WindowStack.Add(window);
        }

        public IEnumerable<object> Options;
        public DrawItemFunc DrawItem;

        private string searchText = "";
        private Vector2 scroll;
        private string title;
        private float lastHeight;

        public SelectorWindow() : this(null) { }

        public SelectorWindow(string title)
        {
            this.title = title;
            doCloseX = true;
            draggable = true;
            resizeable = false;
            closeOnClickedOutside = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (Options == null || DrawItem == null)
            {
                Core.Error("Options supplier or drawer is null!!");
                Close();
                return;
            }

            if (!string.IsNullOrWhiteSpace(title))
            {
                Widgets.Label(inRect, title);
                inRect.yMin += 32;
            }

            Rect textFieldBounds = inRect;
            textFieldBounds.height = 26;
            Widgets.Label(textFieldBounds, "Search:");
            textFieldBounds.xMin += 60;
            searchText = Widgets.TextField(textFieldBounds, searchText);

            Listing_Standard ui = new Listing_Standard();
            var viewArea = inRect;
            viewArea.yMin += 30;
            Widgets.BeginScrollView(viewArea, ref scroll, new Rect(0, 0, 0, lastHeight));
            ui.Begin(new Rect(0, 0, viewArea.width, lastHeight));

            lastHeight = 0f;
            foreach (var item in Options)
            {
                if (searchText.Length == 0 || (item is Def td && td.label.ToLower().Contains(searchText)))
                {
                    lastHeight += DrawItem(item, ui, this);
                }
            }

            ui.End();
            Widgets.EndScrollView();
        }
    }
}
