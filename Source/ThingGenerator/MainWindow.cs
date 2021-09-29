using RimWorld;
using System;
using System.Linq;
using ThingGenerator.Data;
using UnityEngine;
using Verse;

namespace ThingGenerator
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class HotSwappableAttribute : Attribute
    {
    }

    [HotSwappable]
    public class MainWindow : Window
    {
        [DebugAction("ThingGenerator", "Open Editor", allowedGameStates = AllowedGameStates.Entry)]
        private static void OpenMainMenu()
        {
            Messages.Message("Hello world", MessageTypeDefOf.CautionInput, false);
            Open();
        }

        private static void Open()
        {
            Find.WindowStack.Add(new MainWindow());
        }

        public CurrentItem Item;
        public override Vector2 InitialSize => new Vector2(700, 900);

        private Vector2[] scrolls = new Vector2[64];
        private int si; // Scroll Index, used with scrolls array above
        private bool editingName;
        private bool editingDescription;

        public MainWindow()
        {
            doCloseX = true;
            closeOnClickedOutside = false;
            doCloseButton = false;
            resizeable = false;
            preventCameraMotion = false;
            draggable = true;
            drawShadow = true;
            closeOnAccept = false;
            closeOnCancel = false;
            resizeable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Reset.
            si = 0;

            var ui = new Listing_Standard();
            ui.Begin(inRect);

            // If we aren't editing an item, give option to create a new one. Mostly for testing purposes.
            #region Create new item
            if (Item == null)
            {
                ui.Label("No item. Click button to create anew one!");
                if (ui.ButtonText("Create new item!"))
                {
                    Item = new CurrentItem();
                }
                ui.End();
                return;
            }
            #endregion

            // Label and icon of item.
            #region Label and icon
            string label = Item.TryGetOverride("Label")?.Value;
            if (label != null)
            {
                var labelArea = ui.GetRect(42);

                if (Item.BaseThingDef != null)
                {
                    var iconArea = labelArea;
                    iconArea.width = 64;
                    iconArea.height = 64;

                    Widgets.ButtonImageFitted(iconArea, Item.BaseThingDef.uiIcon);
                }

                labelArea.xMin += 75;
                if (editingName)
                {
                    var entryArea = labelArea;
                    entryArea.height = 28;

                    label = Widgets.TextField(entryArea, label, 64);
                    Item.TryGetOverride("Label").Value = label;

                    var buttonArea = labelArea;
                    buttonArea.y += 42;
                    buttonArea.width = 130;
                    buttonArea.height = 20;
                    if (Widgets.ButtonText(buttonArea, "Confirm new name"))
                        editingName = false;
                }
                else
                {
                    var drawArea = labelArea;
                    drawArea.y += 12;

                    Widgets.DrawHighlightIfMouseover(drawArea);

                    Widgets.Label(drawArea, $"<b><size=32>{label.CapitalizeFirst()}</size></b>");
                    TooltipHandler.TipRegion(labelArea, "The name of your item, as seen in-game.\nClick to change.");
                    if (Widgets.ButtonInvisible(labelArea))
                    {
                        editingName = true;
                    }
                }

                ui.GapLine(60);
            }
            #endregion

            // Description of item.
            #region Description
            string desc = Item.TryGetOverride("Desc")?.Value;
            if (desc != null)
            {
                ui.Label("<b>Description:</b>");
                ui.Gap(5);
                var descriptionArea = ui.GetRect(24 * 3);

                if (!editingDescription)
                {
                    Widgets.DrawHighlightIfMouseover(descriptionArea);
                    Widgets.DrawBoxSolidWithOutline(descriptionArea.ExpandedBy(0, 5), default, Color.white * 0.5f, 1);
                    Widgets.LabelScrollable(descriptionArea.ExpandedBy(-5, 0), desc, ref scrolls[si++], true, false, true);
                    TooltipHandler.TipRegion(descriptionArea, "The description of your item.\nClick to change.");
                    if (Widgets.ButtonInvisible(descriptionArea))
                    {
                        editingDescription = true;
                    }
                }
                else
                {
                    desc = Widgets.TextAreaScrollable(descriptionArea, desc, ref scrolls[si++]);
                    Item.TryGetOverride("Desc").Value = desc;
                    ui.Gap(5);
                    if (ui.ButtonText("Confirm new description"))
                        editingDescription = false;
                }

                ui.Gap(20);

                if (!editingDescription && Widgets.ButtonInvisible(descriptionArea))
                {
                    editingDescription = true;
                }
            }
            #endregion

            // Change or set what item this new item is based on.
            #region Based on display and selection
            bool changeBased = false;
            if (Item.BaseThingDef != null)
            {
                var defRect = ui.GetRect(30);
                float w = defRect.width;
                Widgets.Label(defRect, "<b>Based on item:</b>");
                defRect.xMin += 108;
                defRect.xMax -= 100;
                Widgets.DefLabelWithIcon(defRect, Item.BaseThingDef);
                defRect = new Rect(defRect.xMax, defRect.y, w - defRect.xMax, defRect.height);
                changeBased = Widgets.ButtonText(defRect, "Change");
            }
            else
            {
                changeBased = ui.ButtonText("Select base item");
            }

            // Code to handle selecting a new item base.
            if (changeBased)
            {
                SelectorWindow.Open("Select Base Item", DefDatabase<ThingDef>.AllDefsListForReading.Where(t => t.IsWeapon), (obj, listing, window) =>
                {
                    var t = obj as ThingDef; 
                    var rect = listing.GetRect(32);
                    rect = rect.ExpandedBy(0, -4);
                    Widgets.DefLabelWithIcon(rect, t, textOffsetX: 12);
                    bool clicked = Widgets.ButtonInvisible(rect);
                    if (clicked)
                    {
                        // Base item was changed! There are some things to handle...
                        Item = new CurrentItem();
                        Item.BaseThingDef = t;
                        SetupNewItem();

                        window.Close();
                    }
                    return 32;
                });
            }
            #endregion

            // Show item type if not invalid.
            if (Item.Type != ItemType.Invalid)
                ui.Label($"<b>Item type:</b> {Item.Type}");

            if (Item.Type != ItemType.Apparel)
                DrawMeleeDamage(ui);

            ui.End();
        }

        public void SetupNewItem()
        {
            var t = Item.BaseThingDef;

            Item.GetOrAddOverride("Label").Set("label", $"my {t.label}");
            Item.GetOrAddOverride("Desc").Set("description", t.description);
            Item.GetOrAddOverride("DefName").Set("defName", $"{t.defName}_custom_{Rand.Range(int.MinValue, int.MaxValue)}");

            // Tools
            if (t.tools != null)
            {
                int i = 0;
                foreach (var tool in t.tools)
                {
                    var customTool = new ToolData(tool, i++);
                    Item.AddToolData(customTool);
                }
            }
        }

        public void DrawMeleeDamage(Listing_Standard ui)
        {
            if (Item == null)
                return;

            ToolData toBin = null;

            void FloatField(ref Rect area, string label, ref float value, ref string buffer, float minValue = 0, float maxValue = 9999)
            {
                Widgets.Label(area, $"{label}:");
                area.xMin += 80;
                Widgets.TextFieldNumeric(area, ref value, ref buffer, minValue, maxValue);
                area.xMin -= 80;
                area.y += 28;
            }

            void DrawTool(ToolData d)
            {
                Rect area;
                if (d.delete)
                {
                    area = ui.GetRect(32);
                    ui.Gap(10);
                    Widgets.DrawBox(area);
                    area = area.ExpandedBy(-5);
                    Widgets.Label(area, $"<i>[DELETED] {d.label.CapitalizeFirst()}</i>");
                    area.xMin += area.xMax - 70;
                    if (Widgets.ButtonText(area, "Restore"))
                        d.delete = false;
                    return;
                }

                area = ui.GetRect(90);

                var deleteRect = area;
                deleteRect.x = deleteRect.xMax - 26;
                deleteRect.y += 2;
                deleteRect.width = 24;
                deleteRect.height = 24;
                bool delete = Widgets.ButtonText(deleteRect, " <color=#ff7b6b><b>X</b></color>");
                if (delete)
                    toBin = d;

                ui.Gap(10);
                Widgets.DrawBox(area);
                area = area.ExpandedBy(-5);

                // Label.
                Rect current = area;
                current.width = 250;
                current.width = Mathf.Max(current.width, 250);
                current.height = 24;
                Rect left = current;
                Widgets.Label(current, $"<i>{d.label.CapitalizeFirst()}</i>");
                current.y += 24;

                // Float fields.
                FloatField(ref current, "Damage", ref d.power, ref d.powerBuffer);
                FloatField(ref current, "Cooldown", ref d.cooldownTime, ref d.cooldownBuffer);

                // Capacities.
                current = area;
                current.xMin = left.xMax + 9;
                current.xMax = area.xMax - 25;

                Widgets.DrawLineVertical(current.x - 5, current.y, current.height);

                Widgets.Label(current, "<b>Damage capacities:</b>");

                current.yMin += 24;
                ToolCapacityDef deletedCap = null;
                Rect global = current;
                for(int i = 0; i < d.capacities.Count + 1; i++)
                {
                    const float WIDTH = 130;
                    const float HEIGHT = 24;
                    const int MAX_ROWS = 2;
                    int ix = i / MAX_ROWS;
                    int iy = i % MAX_ROWS;

                    float x = global.x + WIDTH * ix;
                    float y = global.y + HEIGHT * iy;
                    current = new Rect(x, y, WIDTH, HEIGHT);

                    if (i == d.capacities.Count)
                    {
                        if (Widgets.ButtonText(current, "Add new"))
                        {
                            SelectorWindow.Open($"Select melee capacity for '{Item.TryGetOverride("Label").Value.CapitalizeFirst()}'", DefDatabase<ToolCapacityDef>.AllDefsListForReading, (obj, listing, window) =>
                            {
                                //window.closeOnClickedOutside = true;
                                var cap = obj as ToolCapacityDef;
                                var rect = listing.GetRect(32);
                                rect = rect.ExpandedBy(0, -4);
                                Widgets.DefLabelWithIcon(rect, cap, textOffsetX: 12);
                                bool clicked = Widgets.ButtonInvisible(rect);
                                if (clicked)
                                {
                                    // Add capacity.
                                    d.capacities.Add(cap);

                                    window.Close();
                                }
                                return 32;
                            });
                        }
                        break;
                    }

                    var cap = d.capacities[i];

                    string name = cap?.label.CapitalizeFirst() ?? "<color=red><error:missing></color>\n";
                    Rect delRect = current;
                    delRect.width = 24;
                    delRect.height = 22;
                    if (Widgets.ButtonText(delRect, " <color=#ff7b6b><b>X</b></color>"))
                        deletedCap = cap;
                    Rect capLabelRect = current;
                    capLabelRect.xMin += 30;
                    Widgets.Label(capLabelRect, name);
                    current.yMin += 26;
                }

                if (deletedCap != null)
                {
                    if (d.capacities.Count > 1)
                    {
                        d.capacities.Remove(deletedCap);
                    }
                    else
                    {
                        Messages.Message($"Cannot remove '{deletedCap.label.CapitalizeFirst()}': Add another capacity first to replace it.", MessageTypeDefOf.RejectInput, false);
                    }
                }
            }


            foreach (var tool in Item.GetAllToolData())
            {
                DrawTool(tool);
            }

            // Handle deleting a tool.
            // The original tools should not be fully deleted.
            if (toBin != null)
            {
                toBin.delete = true;
                if (toBin.overrideIndex == null)
                    Item.RemoveToolData(toBin);
            }
        }
    }
}
