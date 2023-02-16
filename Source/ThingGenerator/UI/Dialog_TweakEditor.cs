using AAM.Reqs;
using AAM.Retexture;
using AAM.Tweaks;
using ColourPicker;
using RimWorld;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace AAM.UI
{
    public class Dialog_TweakEditor : Window
    {
        private static ItemTweakData clipboard;

        [DebugAction("Advanced Animation Mod", "Open Tweak Editor", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        private static void OpenInt() => Open();
        [DebugAction("Advanced Animation Mod", "Open Tweak Editor", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Entry)]
        private static void OpenInt2() => Open();

        public static Dialog_TweakEditor Open()
        {
            var instance = new Dialog_TweakEditor();
            Find.WindowStack?.Add(instance);
            return instance;
        }

        public override Vector2 InitialSize => new(512, 700);
        public ModContentPack Mod;
        public ThingDef Def;

        private Vector2[] scrolls = new Vector2[32];
        private string[] strings = new string[32];
        private int scrollIndex, stringIndex;
        private Camera camera;
        private RenderTexture rt;
        private Texture2D rtTex;
        private Vector3 mousePos;
        private Vector3? startPos;
        private Vector2 tweakStart;
        private byte trsMode;

        public Dialog_TweakEditor()
        {
            closeOnClickedOutside = false;
            doCloseX = true;
            doCloseButton = false;
            preventCameraMotion = false;
            resizeable = true;
            draggable = true;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            rt = new RenderTexture(1024, 1024, 0);
            camera = new GameObject("TEMP CAMERA").AddComponent<Camera>();
            camera.orthographic = true;
            camera.targetTexture = rt;
            camera.forceIntoRenderTexture = true;
            camera.gameObject.SetActive(false);
            camera.orthographicSize = 2;

            camera.transform.position = new Vector3(0, 10, 0);
            camera.transform.localEulerAngles = new Vector3(90, 0, 0);

            // Get full retexture report.
            RetextureUtility.PreCacheAllTextureReports(_ => { }, true);
        }

        public override void PostClose()
        {
            base.PostClose();
            Object.Destroy(camera.gameObject);
            Object.Destroy(rt);
            Object.Destroy(rtTex);
            camera = null;
            rt = null;
            rtTex = null;
        }

        private ref Vector2 GetScroll()
        {
            return ref scrolls[stringIndex++];
        }

        private ref string GetBuffer(object value)
        {
            int i = scrollIndex++;
            if (strings[i] == null)
                strings[i] = value?.ToString();
            return ref strings[i];
        }

        private IEnumerable<ThingDef> AllMeleeWeapons()
        {
            foreach (var rep in RetextureUtility.GetModWeaponReports(Mod))
            {
                yield return rep.Weapon;
            }
        }

        private void ResetBuffers()
        {
            for (int i = 0; i < strings.Length; i++)
                strings[i] = null;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (camera == null)
                return;

            scrollIndex = 0;
            stringIndex = 0;

            var ui = new Listing_Standard();
            ui.Begin(inRect);

            if (ui.ButtonText("(RE)LOAD ALL"))
            {
                foreach (var _ in TweakDataManager.LoadAllForActiveMods(true)) { }
            }

            if (ui.ButtonText($"Select mod{(Mod == null ? null : $" ({Mod.Name})")}"))
            {
                string MakeName(ModTweakContainer container)
                {
                    int total = container.Reports.Count();
                    int done = 0;
                    foreach (var rep in container.Reports)
                    {
                        var tweak = TweakDataManager.TryGetTweak(rep.Weapon, container.Mod);
                        if (tweak != null)
                            done++;
                    }

                    string color = "red";
                    if (done > 0)
                    {
                        color = done >= total ? "green" : "yellow";
                    }

                    return $"<color={color}>{container.Mod.Name} ({done}/{total})</color>";
                }

                var source = TweakDataManager.GetTweaksReportForActiveMods();

                FloatMenuUtility.MakeMenu(source, MakeName, m => () =>
                {
                    ResetBuffers();
                    Mod = m.Mod;
                    Def = null;
                });
            }

            if (Mod == null)
            {
                ui.End();
                return;
            }

            if (ui.ButtonText("SAVE MOD CHANGES"))
            {
                bool saveAll = Input.GetKey(KeyCode.LeftShift);

                IEnumerable<ItemTweakData> toSave;

                if (saveAll)
                {
                    toSave = from pair in TweakDataManager.GetTweaksReportForActiveMods()
                             from report in pair.Reports
                             let tweak = TweakDataManager.TryGetTweak(report.Weapon, pair.Mod)
                             where tweak is not null
                             select tweak;
                }
                else
                {
                    toSave = from rep in RetextureUtility.GetModWeaponReports(Mod)
                             let tweak = TweakDataManager.TryGetTweak(rep.Weapon, Mod)
                             where tweak != null
                             select tweak;
                }

                int c = 0;
                foreach (var item in toSave)
                {
                    var file = TweakDataManager.GetFileForTweak(item.GetDef(), item.GetMod());
                    item.SaveTo(file.FullName);
                    c++;

                    var mapFile = new FileInfo(Path.Combine(file.Directory.FullName, $"{item.TextureModID}.txt"));
                    if (!mapFile.Exists)
                    {
                        File.WriteAllText(mapFile.FullName, item.GetMod().Name);
                    }
                }

                Messages.Message($"Saved {c} tweak data.", MessageTypeDefOf.PositiveEvent, false);
            }

            if (ui.ButtonText($"Editing: {Def?.LabelCap ?? "nothing"}"))
            {
                List<FloatMenuOption> list = new();
                foreach (var def in AllMeleeWeapons())
                {
                    bool hasTweak = TweakDataManager.TryGetTweak(def, Mod) != null;
                    bool hasFallback = false;
                    if (!hasTweak)
                    {
                        // Find fallback tweak.
                        var report = RetextureUtility.GetTextureReport(def);
                        foreach (var retex in report.AllRetextures)
                        {
                            if (retex.mod == Mod)
                                continue;

                            var fallback = TweakDataManager.TryGetTweak(def, retex.mod);
                            if (fallback != null)
                            {
                                hasFallback = true;
                                break;
                            }
                        }
                    }
                    list.Add(new FloatMenuOption($"<color={(hasTweak ? "#97ff87" : hasFallback ? "#fff082" : "#ff828a")}>{def.LabelCap}</color> ", () =>
                    {
                        Def = def;
                        ResetBuffers();

                    },
                    MenuOptionPriority.Default, extraPartWidth: 22, extraPartOnGUI: r =>
                    {
                        Widgets.DefIcon(r, def);
                        return false;
                    },
                    orderInPriority: hasTweak ? -1 : 0) { extraPartRightJustified = true });
                }
                Find.WindowStack.Add(new FloatMenu(list));
            }

            if (Def != null)
            {
                var tweak = TweakDataManager.TryGetTweak(Def, Mod) ?? TweakDataManager.CreateNew(Def, Mod);

                var copyPasta = ui.GetRect(28);
                copyPasta.width = 120;
                if (Widgets.ButtonText(copyPasta, "COPY"))
                    clipboard = tweak;

                copyPasta.x += 130;
                if (clipboard != null && Widgets.ButtonText(copyPasta, "PASTE"))
                {
                    ResetBuffers();
                    tweak.CopyTransformFrom(clipboard);
                }

                var rep = RetextureUtility.GetTextureReport(Def);
                
                copyPasta.x += 130;
                if (Widgets.ButtonText(copyPasta, "PASTE PARENT"))
                {
                    var src = from retex in rep.AllRetextures
                        where retex.mod != Mod && TweakDataManager.TryGetTweak(Def, retex.mod) != null
                        orderby retex.mod.loadOrder
                        select (retex.mod, Def);

                    var src2 = from r in RetextureUtility.AllCachedReports
                        where r.TexturePath == rep.TexturePath && r.Weapon != Def
                        from pair in r.AllRetextures
                        where pair.mod != Mod && TweakDataManager.TryGetTweak(r.Weapon, pair.mod) != null
                        orderby pair.mod.loadOrder
                        select (pair.mod, r.Weapon);

                    IEnumerable<(ModContentPack mod, ThingDef def)> final = src.Concat(src2).Distinct();

                    string MakeLabel((ModContentPack mcp, ThingDef def) pair)
                    {
                        string prefix = null;
                        if (Def != pair.def)
                            prefix = $"<{pair.def.LabelCap}> ";

                        if (Def.modContentPack == pair.mcp)
                            return $"{prefix}[SRC] {pair.mcp.Name}";
                        return prefix + pair.mcp.Name;
                    }

                    FloatMenuUtility.MakeMenu(final, MakeLabel, sel => () =>
                    {
                        var toCopyFrom = TweakDataManager.TryGetTweak(sel.def, sel.mod);

                        ResetBuffers();
                        tweak.CopyTransformFrom(toCopyFrom);
                    });
                    
                }
                copyPasta.x += 130;
                Widgets.DefLabelWithIcon(copyPasta, Def);
                copyPasta.x += 130;
                copyPasta.y += 3;
                Rect capsRect = copyPasta;
                Widgets.Label(copyPasta, "Caps");
                copyPasta.x += 80;
                copyPasta.width = 200;
                tweak.CustomRendererClass = Widgets.TextField(copyPasta, tweak.CustomRendererClass);
                copyPasta.x += 200;
                copyPasta.width = 200;
                Widgets.DrawBoxSolidWithOutline(copyPasta, tweak.TrailTint ?? Color.black, Color.white, 2);
                if (Widgets.ButtonInvisible(copyPasta))
                {
                    tweak.TrailTint ??= default(Color);
                    var picker = new Dialog_ColourPicker(tweak.TrailTint.Value, c =>
                    {
                        if (c.a <= 0)
                            tweak.TrailTint = null;
                        else
                            tweak.TrailTint = c;
                    });
                    Find.WindowStack.Add(picker);
                }

                if (Def.tools != null)
                {
                    string[] caps = new string[Def.tools.Count];
                    for (int i = 0; i < Def.tools.Count; i++)
                    {
                        var tool = Def.tools[i];
                        string s = $"{tool.LabelCap}: {tool.capacities[0].LabelCap}";
                        if ((tool.extraMeleeDamages?.Count) > 0)
                        {
                            s += " + (";
                            foreach (var extra in tool.extraMeleeDamages)
                                s += $"{extra.def.LabelCap}, ";
                            s = s.Substring(0, s.Length - 2);
                            s += ")";
                        }
                        caps[i] = s;
                    }
                    TooltipHandler.TipRegion(capsRect, string.Join(",\n", caps) + $"\nTexture path:{RetextureUtility.GetTextureReport(Def).TexturePath}");
                }

                ui.Gap();
                ui.TextFieldNumericLabeled("Scale X:  ", ref tweak.ScaleX, ref GetBuffer(tweak.ScaleX));
                ui.TextFieldNumericLabeled("Scale Y:  ", ref tweak.ScaleY, ref GetBuffer(tweak.ScaleY));

                ui.Gap();
                tweak.OffX = Widgets.HorizontalSlider(ui.GetRect(28), tweak.OffX, -2, 2, label: $"Offset X <b>[G]</b>: {tweak.OffX:F3}");
                tweak.OffY = Widgets.HorizontalSlider(ui.GetRect(28), tweak.OffY, -2, 2, label: $"Offset Y <b>[Shift+G]</b>: {tweak.OffY:F3}");
                ui.Gap();
                ref string tweakRotBuf = ref GetBuffer(tweak.Rotation);
                ui.TextFieldNumericLabeled("Rotation <b>[R]</b>:  ", ref tweak.Rotation, ref tweakRotBuf, -360, 360);

                // Drawer class, sweep class.
                var otherRect = ui.GetRect(28);
                Widgets.DrawBox(otherRect);

                ui.Gap();
                var flips = ui.GetRect(28);
                flips.width = 200;
                Widgets.CheckboxLabeled(flips, "Mirror X: ", ref tweak.FlipX, placeCheckboxNearText: true);
                flips.x += 210;
                Widgets.CheckboxLabeled(flips, "Mirror Y: ", ref tweak.FlipY, placeCheckboxNearText: true);
                // TODO material mode (use world material, custom material, or transparent material).

                ui.Gap();
                if (ui.ButtonText($"Hands mode: <b>{tweak.HandsMode.ToString().Replace('_', ' ')}</b>"))
                {
                    var options = new HandsMode[] { HandsMode.Default, HandsMode.No_Hands, HandsMode.Only_Main_Hand };
                    FloatMenuUtility.MakeMenu(options, t => t.ToString().Replace('_', ' '), t => () =>
                    {
                        tweak.HandsMode = t;
                    });
                }
                ui.Gap();
                var tagsArea = ui.GetRect(28);
                Widgets.DrawBox(tagsArea.RightPart(0.2f));
                Widgets.Label(tagsArea.RightPart(0.2f), "Allowed animations...");
                string allowedAnimations = "";
                foreach (var anim in AnimDef.AllDefs)
                    if (anim.AllowsWeapon(new ReqInput(tweak)))
                        allowedAnimations += $"[{anim.type}] {anim.defName}\n";
                TooltipHandler.TipRegion(tagsArea.RightPart(0.2f), allowedAnimations);

                if (Widgets.ButtonText(tagsArea.LeftPart(0.75f), $"Tags: <b>{tweak.MeleeWeaponType}</b>"))
                {
                    string MakeTagLabel(MeleeWeaponType tag)
                    {
                        bool flag = tweak.MeleeWeaponType.HasFlag(tag);
                        return $"<color={(flag ? "#97ff87" : "#ff828a")}>{tag.ToString().Replace('_', ' ')}</color>";
                    }
                    var list = System.Enum.GetValues(typeof(MeleeWeaponType)).Cast<MeleeWeaponType>();
                    FloatMenuUtility.MakeMenu(list, MakeTagLabel, t => () =>
                    {
                        if (Input.GetKey(KeyCode.LeftShift))
                        {
                            tweak.MeleeWeaponType = t;
                            return;
                        }
                        bool flag = tweak.MeleeWeaponType.HasFlag(t);
                        if (flag)
                            tweak.MeleeWeaponType &= ~t;
                        else
                            tweak.MeleeWeaponType |= t;
                    });
                }

                var startSlider = ui.GetRect(20);
                var endSlider = ui.GetRect(20);
                tweak.BladeStart = Widgets.HorizontalSlider(startSlider, tweak.BladeStart, -2, 2, label: "Blade Start <b>[Q]</b>");
                tweak.BladeEnd = Widgets.HorizontalSlider(endSlider, tweak.BladeEnd, -2, 2, label: "Blade End <b>[E]</b>");

                ui.GapLine();

                if (Event.current.type == EventType.Repaint)
                {
                    try
                    {
                        const int FRAME_INTERVAL = 1;
                        if ((uint)(Time.unscaledTime * 60) % FRAME_INTERVAL == 0)
                            RenderPreview(tweak);
                    }
                    catch { }
                }

                if (rtTex != null)
                {
                    var view = ui.GetRect(inRect.height - ui.CurHeight - 30);
                    if ((int)view.width != rt.width || (int)view.height != rt.height)
                    {
                        rt.Release();
                        rt.width = (int)view.width;
                        rt.height = (int)view.height;
                        rt.Create();
                    }

                    var mp = Event.current.mousePosition;
                    var localMousePos = mp - view.center;
                    localMousePos.y *= -1;
                    float unitsPerPixel = (camera.orthographicSize * 2) / view.height;
                    mousePos = localMousePos * unitsPerPixel;
                    camera.backgroundColor = Color.grey * 0.25f;

                    GUI.DrawTexture(view, rtTex);
                    Widgets.DrawLine(new Vector2(view.xMin, view.center.y), new Vector2(view.xMax, view.center.y), new Color(0f, 1f, 0f, 0.333f), 1);
                    Widgets.DrawLine(new Vector2(view.center.x, view.yMin), new Vector2(view.center.x, view.yMax), new Color(0f, 1f, 0f, 0.333f), 1);
                    Widgets.Label(view, $"{localMousePos}, {mousePos}, {rt.width}x{rt.height} vs {(int)view.width}x{(int)view.height}");

                    if (Widgets.ButtonInvisible(view, false))
                    {
                        GUI.FocusControl(null);
                    }

                    // Modify start and end.
                    if (startPos == null)
                    {
                        if (Input.GetKey(KeyCode.E))
                            tweak.BladeEnd = mousePos.x;
                        else if (Input.GetKey(KeyCode.Q))
                            tweak.BladeStart = mousePos.x;
                        else if (Input.GetKey(KeyCode.G))
                        {
                            startPos = mousePos;
                            tweakStart = new Vector2(tweak.OffX, tweak.OffY);
                            trsMode = 0;
                        }
                        else if (Input.GetKey(KeyCode.R))
                        {
                            startPos = mousePos;
                            tweakStart = new Vector2(tweak.Rotation, 0);
                            trsMode = 1;
                        }
                    }
                    else
                    {
                        switch (trsMode)
                        {
                            case 0:
                                // Move.
                                var offset = mousePos - startPos.Value;
                                var newPos = tweakStart + offset * new Vector2(tweak.FlipX ? -1 : 1, 1);

                                tweak.OffX = newPos.x;
                                if (Input.GetKey(KeyCode.LeftShift))
                                    tweak.OffY = newPos.y;


                                if (!Input.GetKey(KeyCode.G))
                                    startPos = null;
                                break;

                            case 1:
                                // Rotate.
                                float a = Vector2.SignedAngle(startPos.Value, mousePos);
                                tweak.Rotation = (tweakStart.x + (a * (tweak.FlipX ^ tweak.FlipY ? 1f : -1f))) % 360f;

                                if (!Input.GetKey(KeyCode.LeftShift))
                                    tweak.Rotation = Mathf.Round(tweak.Rotation / 5f) * 5f;
                                tweakRotBuf = tweak.Rotation.ToString(CultureInfo.InvariantCulture);

                                if (!Input.GetKey(KeyCode.R))
                                    startPos = null;
                                break;

                            default:
                                Core.Error("Bad");
                                trsMode = 0;
                                break;
                        }
                    }
                }

                const float MIN = 0.1f;
                const float MAX = 5f;

                camera.orthographicSize = Mathf.Lerp(MIN, MAX, 1f - Widgets.HorizontalSlider(ui.GetRect(30), 1f - Mathf.InverseLerp(MIN, MAX, camera.orthographicSize), 0f, 1f, label: "Camera zoom"));
            }

            ui.End();
        }

        private void RenderPreview(ItemTweakData tweak)
        {
            var block = new MaterialPropertyBlock();
            var itemTex = tweak.GetTexture(false, false);
            if (itemTex == null || Mathf.Abs(tweak.ScaleX) < 0.1f || Mathf.Abs(tweak.ScaleY) < 0.1f)
            {
                Core.Error("WHOA");
            }
            block.SetTexture("_MainTex", itemTex ?? Widgets.CheckboxOffTex);
            var pos = new Vector3(tweak.OffX, 0f, tweak.OffY);
            if (tweak.FlipX)
                pos.x *= -1;
            if (tweak.FlipY)
                pos.y *= -1;
            float offRot = tweak.FlipX ^ tweak.FlipY ? -tweak.Rotation : tweak.Rotation;
            itemTex.wrapMode = TextureWrapMode.Repeat;
            Graphics.DrawMesh(AnimData.GetMesh(tweak.FlipX, tweak.FlipY), Matrix4x4.TRS(pos, Quaternion.Euler(0f, offRot, 0f), new Vector3(tweak.ScaleX, 1f, tweak.ScaleY)), AnimRenderer.DefaultCutout, 0, camera, 0, block);

            var handScale = new Vector3(0.175f, 1f, 0.175f);
            var handAPos = new Vector3(0f, 1f, 0f);
            var handBPos = new Vector3(-0.146f, -1f, -0.011f);
            var handTex = ContentFinder<Texture2D>.Get("AAM/Hand");
            block.SetTexture("_MainTex", handTex);

            if (tweak.HandsMode != HandsMode.No_Hands)
                Graphics.DrawMesh(AnimData.GetMesh(false, false), Matrix4x4.TRS(handAPos, Quaternion.identity, handScale), AnimRenderer.DefaultCutout, 0, camera, 0, block);
            if (tweak.HandsMode == HandsMode.Default)
                Graphics.DrawMesh(AnimData.GetMesh(false, false), Matrix4x4.TRS(handBPos, Quaternion.identity, handScale), AnimRenderer.DefaultCutout, 0, camera, 0, block);

            Vector3 start = new(tweak.BladeStart, 0, 1);
            Vector3 end = new(tweak.BladeStart, 0, -1);
            for (int i = 0; i < 3; i++)
                GenDraw.DrawLineBetween(start, end, SimpleColor.Green, 0.1f);

            start = new Vector3(tweak.BladeEnd, 0, 1);
            end = new Vector3(tweak.BladeEnd, 0, -1);
            for (int i = 0; i < 3; i++)
                GenDraw.DrawLineBetween(start, end, SimpleColor.Red, 0.1f);

            camera.Render();

            if (rtTex != null)
                Object.Destroy(rtTex);

            rtTex = rt.ToTexture2D();
        }
    }
}
