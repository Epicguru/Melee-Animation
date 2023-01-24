using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Verse;

namespace AAM
{
    public static class SimpleSettings
    {
        public delegate float DrawHandler(ModSettings settings, MemberWrapper member, Rect area);

        public static Func<MemberWrapper, DrawHandler> SelectDrawHandler = DefaultDrawHandlerSelector;
        public static Dictionary<Type, DrawHandler> DrawHandlers = new()
        {
            //{ typeof(string), DrawStringField },
            { typeof(byte), DrawNumeric },
            { typeof(sbyte), DrawNumeric },
            { typeof(short), DrawNumeric },
            { typeof(ushort), DrawNumeric },
            { typeof(int), DrawNumeric },
            { typeof(uint), DrawNumeric },
            { typeof(long), DrawNumeric },
            { typeof(ulong), DrawNumeric },
            { typeof(float), DrawNumeric },
            { typeof(double), DrawNumeric },
            { typeof(decimal), DrawNumeric },
            { typeof(bool), DrawToggle },
        };
        
        private static readonly Dictionary<Type, FieldHolder> settingsFields = new();
        private static readonly Stack<ScribeSaver> saverStack = new();
        private static readonly Stack<ScribeLoader> loaderStack = new();
        private static readonly Stack<LoadSaveMode> modeStack = new();
        private static readonly HashSet<string> allHeaders = new();
        private static MemberWrapper highlightedMember;

        internal static string TranslateOrSelf(this string str) => str.TryTranslate(out var found) ? found : str;

        public static void Init(ModSettings settings)
        {
            if (settings == null)
                return;

            var type = settings.GetType();
            if (settingsFields.ContainsKey(type))
            {
                Log.Error($"Already called Init() for settings class: {type.FullName}");
                return;
            }

            var def = new FieldHolder(settings, type);
            settingsFields.Add(type, def);
        }

        private static FieldHolder GetHolder(ModSettings settings)
        {
            if (settingsFields.TryGetValue(settings.GetType(), out var found))
                return found;

            Init(settings);
            return settingsFields[settings.GetType()];
        }

        public static void AutoExpose(ModSettings settings)
        {
            var holder = GetHolder(settings);

            foreach (var member in holder.Members.Values)
            {
                if (member.ShouldExpose)
                    member.Expose(settings);
            }
        }

        public static object SmartClone(object obj)
        {
            if (obj == null)
                return null;

            // Values types are just fine being passed back, since it will be a copy by the time it is assigned back.
            if (obj.GetType().IsValueType)
                return obj;

            // Lists...
            var type = obj.GetType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                // Make new list of appropriate type.
                var param = type.GenericTypeArguments[0];
                var generic = typeof(List<>).MakeGenericType(param);
                var list = Activator.CreateInstance(generic) as IList;

                // Copy over each item from the original list.
                var origList = obj as IList;
                foreach (var item in origList)
                    list.Add(SmartClone(item));
                return list;
            }

            if (obj is ICloneable cl)
            {
                return cl.Clone();
            }

            if (obj is not IExposable exp)
            {
                Log.Warning($"Cannot create clone of type '{type.FullName}' since it is neither IExposable nor ICloneable.");
                return obj;
            }

            // Try to make clone using IExposable.
            if (Activator.CreateInstance(type) is not IExposable created)
            {
                Log.Error($"Failed to create new instance of '{type.FullName}' because it lacks a public zero argument constructor or is abstract.");
                return null;
            }

            // Write to temp file. It would be nice if this could be done to a memory stream - unfortunately the fields are not exposed and I don't want to
            // make a hacky workaround using reflection, with it being such an important class.
            PushNewScribeState();
            try
            {
                string tempFilePath = Path.GetTempFileName();
                Scribe.saver.InitSaving(tempFilePath, "ROOT");
                exp.ExposeData();
                Scribe.saver.FinalizeSaving();

                // Load back from the temporary file.
                Scribe.loader.InitLoading(tempFilePath);
                created.ExposeData();
                Scribe.loader.FinalizeLoading();
            }
            catch (Exception e)
            {
                Log.Error($"Exception when cloning using custom scribe: {e}");
            }
            finally
            {
                PopScribeState();
            }
            

            return created;
        }

        public static string MakeDebugString(ModSettings settings)
        {
            if (settings == null)
                return null;

            var holder = GetHolder(settings);
            var str = new StringBuilder(1024);

            foreach (var member in holder.Members.Values)
            {
                str.Append('[').Append(member.MemberType.Name).Append("] ");
                str.Append(member.Name).Append(" : ").AppendLine(member.DefaultValue?.ToString() ?? "<null>");
            }

            return str.ToString();
        }

        public static void DrawWindow(ModSettings settings, Rect inRect)
        {
            if (settings == null)
                return;

            Rect tabBar = inRect;
            tabBar.height = 28;

            highlightedMember = null;
            float totalWidth = inRect.width;
            inRect.width *= 0.7f;
            inRect.y += 28;
            inRect.height -= 28;

            var holder = GetHolder(settings);

            Widgets.BeginScrollView(inRect, ref holder.UI_Scroll, new Rect(0, 0, holder.UI_LastSize.x, holder.UI_LastSize.y));
            Vector2 size = new(inRect.width - 20, 0);
            Vector2 pos = Vector2.zero;
            string currentHeader = null;
            string selectedHeader = holder.UI_SelectedTab;
            allHeaders.Clear();

            foreach (var member in holder.Members.Values)
            {
                bool isCurrentTab = currentHeader == selectedHeader;

                // TODO draw header.
                var header = member.TryGetCustomAttribute<HeaderAttribute>();
                bool didHeader = false;
                if (header != null)
                {
                    currentHeader = header.header;
                    allHeaders.Add(header.header);
                    isCurrentTab = currentHeader == selectedHeader;

                    float headerHeight = 32;

                    if(isCurrentTab)
                    {
                        var headerRect = new Rect(new Vector2(pos.x, pos.y + 12), new Vector2(inRect.width - 20, headerHeight));
                        string headerText = $"{settings.GetType().FullName}.{header.header}".TryTranslate(out var found) ? found : header.header;
                        Widgets.Label(headerRect, $"<color=cyan><b><size=22>{headerText}</size></b></color>");

                        pos.y += headerHeight + 12;
                        size.y += headerHeight + 12;
                        didHeader = true;
                    }
                }

                if (!isCurrentTab)
                    continue;

                var handler = SelectDrawHandler(member);
                if (handler == null)
                    continue;

                if (!didHeader)
                {
                    pos.y += 6;
                    size.y += 6;
                    GUI.color = new Color(1, 1, 1, 0.25f);
                    Widgets.DrawLineHorizontal(pos.x + 20, pos.y, inRect.width);
                    GUI.color = Color.white;
                    pos.y += 6;
                    size.y += 6;
                }
                else
                {
                    pos.y += 12;
                    size.y += 12;
                }

                var area = new Rect(new Vector2(pos.x + 20, pos.y), new Vector2(inRect.width - 40, inRect.height - pos.y));
                float height = handler(settings, member, area);

                var finalArea = area;
                finalArea.height = height;

                if (member.Options.DrawHoverHighlight)
                    Widgets.DrawHighlightIfMouseover(finalArea);
                if (Mouse.IsOver(finalArea))
                    highlightedMember = member;

                pos.y += height;
                size.y += height;
            }

            Widgets.EndScrollView();
            holder.UI_LastSize = size;
            holder.UI_SelectedTab ??= allHeaders.First();

            float tabWidth = (1f / allHeaders.Count) * tabBar.width;
            int i = 0;
            foreach(string tab in allHeaders)
            {
                var area = new Rect(tabBar.x + tabWidth * i, tabBar.y, tabWidth, tabBar.height);
                bool active = holder.UI_SelectedTab == tab;
                GUI.color = active ? Color.grey : Color.white;
                string tabText = $"{settings.GetType().FullName}.{tab}".TryTranslate(out var found) ? found : tab;
                if (Widgets.ButtonText(area.ExpandedBy(-2, 0), $"<b><color=lightblue>{tabText}</color></b>"))
                    holder.UI_SelectedTab = tab;
                if (active)
                    Widgets.DrawBox(area.ExpandedBy(-2, 0));
                GUI.color = Color.white;
                i++;
            }

            inRect.x = inRect.xMax;
            inRect.width = totalWidth * 0.3f;
            GUI.color = Color.white * 0.5f;
            Widgets.DrawBox(inRect, 1);
            GUI.color = Color.white;
            inRect = inRect.ExpandedBy(-5, -5);

            if (highlightedMember == null)
                return;

            Text.Anchor = TextAnchor.UpperCenter;
            Text.Font = GameFont.Medium;
            Widgets.Label(inRect, $"<b><color=lightblue>{highlightedMember.DisplayName}</color></b>");
            float titleHeight = Text.CalcHeight($"<b>{highlightedMember.DisplayName}</b>", inRect.width);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            if (highlightedMember.Options.DrawDescription)
            {
                string description = highlightedMember.GetDescription() ?? "<i>No description</i>";
                inRect.y += titleHeight + 14;
                Widgets.Label(inRect, description);

                inRect.y += Text.CalcHeight(description, inRect.width) + 32;
            }            

            if (highlightedMember.Options.AllowReset)
            {
                string defaultValue = highlightedMember.ValueToString(highlightedMember.GetDefault<object>());
                Widgets.Label(inRect, $"<color=grey><i>Default value: </i>{defaultValue}\n\nRight-click to reset to default.</color>");

                if (Input.GetMouseButtonUp(1))
                {
                    highlightedMember.Set(settings, highlightedMember.DefaultValue);
                    highlightedMember.TextBuffer = highlightedMember.DefaultValue.ToString();
                }
            }            
        }

        public static DrawHandler DefaultDrawHandlerSelector(MemberWrapper wrapper)
        {
            if (wrapper == null)
                return null;

            if (wrapper.OverrideDrawHandler != null)
                return wrapper.OverrideDrawHandler;

            var type = wrapper.MemberType;

            // Enum.
            if (type.IsEnum)
                return DrawEnum;

            // Everything else.
            if (DrawHandlers.TryGetValue(type, out var found))
                return found;

            return null;
        }

        private static float GetNumericMin(Type type)
        {
            return (float)Convert.ChangeType(type.GetField("MinValue", BindingFlags.Public | BindingFlags.Static).GetValue(null), typeof(float));
        }

        private static float GetNumericMax(Type type)
        {
            return (float)Convert.ChangeType(type.GetField("MaxValue", BindingFlags.Public | BindingFlags.Static).GetValue(null), typeof(float));
        }

        public static float DrawFieldHeader(ModSettings settings, MemberWrapper member, Rect area)
        {
            float height = 26;

            Rect labelrect = area;
            labelrect.height = height;
            var value = member.Get<object>(settings);
            var old = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;

            string label = $"<b>{member.DisplayName}</b>";
            if (member.Options.DrawValue)
                label += $": {member.ValueToString(value)}";
            if (member.Options.AllowReset)
                label = HighlightIfNotDefault(settings, member, label);

            Widgets.Label(labelrect, label);
            Text.Anchor = old;

            return height;
        }

        private static string HighlightIfNotDefault(ModSettings settings, MemberWrapper member, string str)
        {
            if (member.IsDefault(settings))
                return str;

            return $"<color=yellow>{str}</color>";
        }

        private static void TryGetBounds(MemberWrapper member, out float? min, out float? max)
        {
            var range = member.TryGetCustomAttribute<RangeAttribute>();
            var minAtr = member.TryGetCustomAttribute<MinAttribute>();
            var percentage = member.TryGetCustomAttribute<PercentageAttribute>();

            min = null;
            max = null;

            if(range != null)
            {
                min = range.min;
                max = range.max;
            }
            else if (minAtr != null)
            {
                min = minAtr.min;
            }
            else if (percentage != null)
            {
                min = 0;
                max = 1;
            }

            if (min != null)
                min = Mathf.Max(min.Value, GetNumericMin(member.MemberType));
            if (max != null)
                max = Mathf.Min(max.Value, GetNumericMax(member.MemberType));            
        }

        private static float DrawNumeric(ModSettings settings, MemberWrapper member, Rect area)
        {
            float height = DrawFieldHeader(settings, member, area);
            float value = member.Get<float>(settings);

            // Min and max.
            TryGetBounds(member, out var min, out var max);
            if (min == null || max == null)
            {
                return DrawNumericTextBased(settings, member, area, min, max);
            }

            float sliderHeight = 18;
            Rect sliderArea = new(area.x, area.y + height, area.width, sliderHeight);
            height += sliderHeight;

            float step = member.TryGetCustomAttribute<StepAttribute>()?.Step ?? -1;

            // Simple slider for now.
#if V13
            float changed = Widgets.HorizontalSlider(sliderArea, value, min.Value, max.Value, roundTo: step);
#else
            float changed = Widgets.HorizontalSlider_NewTemp(sliderArea, value, min.Value, max.Value, roundTo: step);
#endif
            if (changed != value)
            {
                Type type = member.MemberType;
                bool isFloatType = type == typeof(float) || type == typeof(double) || type == typeof(decimal);
                object writeBack = changed;
                if (!isFloatType)
                    writeBack = (long)Mathf.Round(changed);
                member.Set(settings, writeBack);
            }

            return height;
        }

        private static float DrawNumericTextBased(ModSettings settings, MemberWrapper member, Rect area, float? min, float? max)
        {
            float height = DrawFieldHeader(settings, member, area);
            float value = member.Get<float>(settings);

            const float TEXTBOX_HEIGHT = 28;
            Rect field = area;
            field.y += height;
            field.height = TEXTBOX_HEIGHT;

            float minF = min ?? float.MinValue;
            float maxF = max ?? float.MaxValue;
            float newValue = value;
            if (member.TextBuffer.Length == 0)
                member.TextBuffer = member.DefaultValue.ToString();
            //Core.Log($"Drawing {member.Name}, {newValue}, {member.TextBuffer}, {minF}, {maxF}");
            Widgets.TextFieldNumeric(field, ref newValue, ref member.TextBuffer, minF, maxF);
            if (newValue != value)
                member.Set(settings, newValue);

            return TEXTBOX_HEIGHT + height;
        }

        private static float DrawToggle(ModSettings settings, MemberWrapper member, Rect area)
        {
            Rect toggleRect = area;
            toggleRect.height = 28;

            bool enabled = member.Get<bool>(settings);
            bool old = enabled;
            string txt = HighlightIfNotDefault(settings, member, $"<b>{member.DisplayName}:</b> ");
            toggleRect.width = Text.CalcSize(txt).x + 24f + 24f;
            Widgets.CheckboxLabeled(toggleRect, txt, ref enabled, placeCheckboxNearText: false);

            if (old != enabled)
                member.Set(settings, enabled);

            return toggleRect.height;
        }

        private static float DrawEnum(ModSettings settings, MemberWrapper member, Rect area)
        {
            float height = 28;

            Rect rect = area;
            rect.height = height;

            string txt = HighlightIfNotDefault(settings, member, $"<b>{member.DisplayName}: </b>");
            float labelWidth = Text.CalcSize(txt).x;
            Widgets.Label(rect, txt);

            var value = member.Get<object>(settings);

            if (Widgets.ButtonText(new Rect(rect.x + labelWidth + 10, rect.y, 240, height), member.ValueToString(value)))
            {
                var values = Enum.GetValues(member.MemberType).OfType<object>();
                FloatMenuUtility.MakeMenu(values, member.ValueToString, o =>
                () =>
                {
                    member.Set(settings, o);
                });
            }

            return height;
        }

        public static void PushNewScribeState()
        {
            saverStack.Push(Scribe.saver);
            loaderStack.Push(Scribe.loader);
            modeStack.Push(Scribe.mode);

            Scribe.saver = new ScribeSaver();
            Scribe.loader = new ScribeLoader();
            Scribe.mode = LoadSaveMode.Inactive;
        }

        public static void PopScribeState()
        {
            Scribe.saver = saverStack.Pop();
            Scribe.loader = loaderStack.Pop();
            Scribe.mode = modeStack.Pop();
        }

        public class FieldHolder
        {
            public readonly ModSettings ForSettingsObject;
            public readonly Type ForType;
            public readonly Dictionary<MemberInfo, MemberWrapper> Members = new();
            public Vector2 UI_LastSize;
            public Vector2 UI_Scroll;
            public string UI_SelectedTab;

            public FieldHolder(ModSettings settings, Type forType)
            {
                ForSettingsObject = settings;
                ForType = forType;

                foreach (var member in ForType.GetMembers(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (member is not FieldInfo && member is not PropertyInfo)
                        continue;

                    bool isInvalidProp = member is PropertyInfo pi && (!pi.CanWrite || !pi.CanRead);
                    if (isInvalidProp)
                        continue;

                    var wrapper = MakeWrapperFor(settings, member);
                    SetOverrideDrawHandler(settings, wrapper);

                    Members.Add(member, wrapper);
                }
            }

            private MemberWrapper MakeWrapperFor(object obj, MemberInfo member)
            {
                var type = member switch
                {
                    FieldInfo fi => fi.FieldType,
                    PropertyInfo pi => pi.PropertyType,
                    _ => throw new ArgumentException(nameof(member), $"Unexpected type: {member.GetType().FullName}")
                };

                // Dictionaries.
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    return MakeGenericWrapper(typeof(MemberWrapperDict<,>), obj, member, type.GetGenericArguments());
                }

                // Lists.
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                {
                    return MakeGenericWrapper(typeof(MemberWrapperList<>), type.GetGenericArguments()[0], obj, member);
                }

                // Defs.
                if (typeof(Def).IsAssignableFrom(type))
                {
                    return MakeGenericWrapper(typeof(MemberWrapperDef<>), type, obj, member);
                }

                // IExposable or regular value type.
                return MakeGenericWrapper(typeof(MemberWrapperGen<>), type, obj, member);
            }

            private MemberWrapper MakeGenericWrapper(Type baseGenericType, Type genericParam, object obj, MemberInfo member)
            {
                var generic = baseGenericType.MakeGenericType(genericParam);
                return Activator.CreateInstance(generic, obj, member) as MemberWrapper;
            }

            private MemberWrapper MakeGenericWrapper(Type baseGenericType, object obj, MemberInfo member, params Type[] genericParams)
            {
                var generic = baseGenericType.MakeGenericType(genericParams);
                return Activator.CreateInstance(generic, obj, member) as MemberWrapper;
            }

            private void SetOverrideDrawHandler(ModSettings settings, MemberWrapper member)
            {
                if (member == null)
                    return;

                var attr = member.TryGetCustomAttribute<DrawMethodAttribute>();
                if (attr == null)
                    return;

                member.ShouldExpose = attr.SerializeField;

                const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
                var methodInfo = settings.GetType().GetMethod(attr.MethodName, FLAGS);
                if (methodInfo == null)
                {
                    Core.Error($"Failed to find method '{attr.MethodName}' in class {settings.GetType()}!");
                    return;
                }

                DrawHandler handler;
                if (methodInfo.IsStatic)
                    handler = Delegate.CreateDelegate(typeof(DrawHandler), methodInfo, false) as DrawHandler;
                else
                    handler = Delegate.CreateDelegate(typeof(DrawHandler), settings, methodInfo, false) as DrawHandler;

                if (handler == null)
                {
                    Core.Error($"Method '{attr.MethodName}' does not match the {nameof(DrawHandler)} signiture!");
                    return;
                }

                member.OverrideDrawHandler = handler;
            }
        }

        private class MemberWrapperDict<K, V> : MemberWrapper
        {
            public MemberWrapperDict(object obj, FieldInfo field) : base(obj, field) { }
            public MemberWrapperDict(object obj, PropertyInfo prop) : base(obj, prop) { }

            private LookMode GetLookMode<T>()
            {
                if (typeof(T).GetInterfaces().Contains(typeof(IExposable)))
                    return LookMode.Deep;

                if (typeof(Def).IsAssignableFrom(typeof(T)))
                    return LookMode.Def;

                return LookMode.Undefined;
            }

            public override void Expose(object obj)
            {
                var current = Get<Dictionary<K, V>>(obj);
                Scribe_Collections.Look(ref current, NameInXML, GetLookMode<K>(), GetLookMode<V>());
                Set(obj, current);
            }
        }

        private class MemberWrapperList<T> : MemberWrapper
        {
            public MemberWrapperList(object obj, FieldInfo field) : base(obj, field) { }
            public MemberWrapperList(object obj, PropertyInfo prop) : base(obj, prop) { }

            public LookMode GetLookMode()
            {
                if (typeof(T).GetInterfaces().Contains(typeof(IExposable)))
                    return LookMode.Deep;

                if (typeof(Def).IsAssignableFrom(typeof(T)))
                    return LookMode.Def;

                return LookMode.Undefined;
            }

            public override void Expose(object obj)
            {
                var current = Get<List<T>>(obj);
                Scribe_Collections.Look(ref current, NameInXML, GetLookMode());
                Set(obj, current);
            }
        }

        private class MemberWrapperDef<T> : MemberWrapperGen<T> where T : Def, new()
        {
            public MemberWrapperDef(object obj, FieldInfo field) : base(obj, field) { }
            public MemberWrapperDef(object obj, PropertyInfo prop) : base(obj, prop) { }

            public override void Expose(object obj)
            {
                // Do not call base.
                // Here we only have to handle defs using the Scribe_Defs.
                T current = Get<T>(obj);
                Scribe_Defs.Look(ref current, NameInXML);
                Set(obj, current);
            }
        }

        private class MemberWrapperGen<T> : MemberWrapper
        {
            public MemberWrapperGen(object obj, FieldInfo field) : base(obj, field) { }
            public MemberWrapperGen(object obj, PropertyInfo prop) : base(obj, prop) { }

            public override void Expose(object obj)
            {
                T current = Get<T>(obj);
                T defaultValue = (T)DefaultValue;

                // IExposable: use Scribe_Deep.
                if (IsIExposable)
                {
                    Scribe_Deep.Look(ref current, NameInXML);
                    Set(obj, current);
                    return;
                }

                // Default: use Scribe_Values
                Scribe_Values.Look(ref current, NameInXML, defaultValue);
                Set(obj, current);
            }

            public override K Get<K>(object obj)
            {
                // When unboxing, we need to cast to the exact type.
                // However, it is nice to be able to call Get<T> where T is assignable from the real type,
                // such as Get<float> when the field is actually a double.
                // The cast will fail unless we unbox to the T first before re-boxing, then unboxing into 
                object temp = base.Get<T>(obj);
                if (temp == null)
                    return default;

                if (typeof(T) != typeof(K) && temp is IConvertible)
                {
                    return (K)Convert.ChangeType(temp, typeof(K));
                }

                return (K)temp;
            }

            public override K GetDefault<K>()
            {
                object temp = (T)DefaultValue;
                if (temp == null)
                    return default;

                if (typeof(T) != typeof(K))
                    return (K)Convert.ChangeType(temp, typeof(K));

                return (K)temp;
            }
        }

        public abstract class MemberWrapper
        {
            public string DisplayName => _displayName ??= MakeDisplayName();
            public string Name => field?.Name ?? prop?.Name;
            public readonly object DefaultValue;
            public Type MemberType => field?.FieldType ?? prop.PropertyType;
            public Type DeclaringType => field?.DeclaringType ?? prop.DeclaringType;
            public string TranslationName => $"{DeclaringType.FullName}.{Name}";
            public bool IsIExposable => MemberType.GetInterfaces().Contains(typeof(IExposable));
            public bool IsValueType => MemberType.IsValueType;
            public bool IsDefType => typeof(Def).IsAssignableFrom(MemberType);
            public bool IsStatic => field?.IsStatic ?? prop.GetMethod.IsStatic;
            public string NameInXML => Name;
            public IEnumerable<Attribute> CustomAttributes => field?.GetCustomAttributes() ?? prop.GetCustomAttributes();
            public string TextBuffer = "";
            public DrawHandler OverrideDrawHandler { get; set; }
            public bool ShouldExpose { get; set; } = true;
            public SettingOptionsAttribute Options { get; protected set; }

            protected readonly FieldInfo field;
            protected readonly PropertyInfo prop;
            private string _displayName;

            protected MemberWrapper(object obj, MemberInfo member)
            {
                switch (member)
                {
                    case FieldInfo fi:
                        field = fi;
                        break;
                    case PropertyInfo pi:
                        prop = pi;
                        break;
                    default:
                        throw new ArgumentException(nameof(member), $"Unexpected type: {member.GetType().FullName}");
                }

                Options = member.TryGetAttribute<SettingOptionsAttribute>() ?? SettingOptionsAttribute.CreateDefault();
                DefaultValue = GetDefaultValue(obj);
            }

            public bool IsDefault(ModSettings settings)
            {
                var current = Get<object>(settings);
                bool bothNull = current == null && DefaultValue == null;

                return bothNull || (current != null && current.Equals(DefaultValue));
            }

            protected virtual string MakeDisplayName()
            {
                var label = TryGetCustomAttribute<LabelAttribute>();
                if (label != null)
                {
                    return label.Label.TranslateOrSelf();
                }

                if (TranslationName.TryTranslate(out var found))
                    return found;

                var str = new StringBuilder();
                bool lastWasLower = true;
                foreach (var c in Name)
                {
                    if (char.IsUpper(c) && lastWasLower)
                        str.Append(' ');

                    lastWasLower = char.IsLower(c);

                    if (c == '_')
                    {
                        str.Append(' ');
                        continue;
                    }

                    str.Append(c);
                }

                return str.ToString().Trim().CapitalizeFirst();
            }

            public virtual string ValueToString(object value)
            {
                if (TryGetCustomAttribute<PercentageAttribute>() != null && value is float f)
                    return $"{f*100f:F0}%";

                return value?.ToString() ?? "<null>";
            }

            public virtual string GetDescription()
            {
                var attr = TryGetCustomAttribute<DescriptionAttribute>();
                if (attr == null)
                    return $"{TranslationName}.Desc".TryTranslate(out var found) ? found : null;

                return attr.Description.TranslateOrSelf();
            }

            public virtual T Get<T>(object obj)
            {
                if (field != null)
                    return (T)field.GetValue(IsStatic ? null : obj);

                return (T)prop.GetValue(IsStatic ? null : obj);
            }

            public virtual T GetDefault<T>() => (T)DefaultValue;

            public virtual T TryGetCustomAttribute<T>() where T : Attribute
            {
                return field != null ? field.TryGetAttribute<T>() : prop.TryGetAttribute<T>();
            }

            public void Set(object obj, object value)
            {
                Type expected = MemberType;
                Type got = value?.GetType();

                if (got != null && got != expected && value is IConvertible)
                    value = Convert.ChangeType(value, expected);

                if (field != null)
                {
                    field.SetValue(IsStatic ? null : obj, value);
                    return;
                }

                prop.SetValue(IsStatic ? null : obj, value);
            }

            public abstract void Expose(object obj);

            private object GetDefaultValue(object obj)
            {
                object current = Get<object>(obj);
                if (current == null)
                    return null;

                return SmartClone(current);
            }
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class LabelAttribute : Attribute
    {
        public readonly string Label;
        public LabelAttribute(string label)
        {
            this.Label = label;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class PercentageAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class StepAttribute : Attribute
    {
        public readonly float Step;
        public StepAttribute(float step) { Step = step; }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class DescriptionAttribute : Attribute
    {
        public readonly string Description;
        public DescriptionAttribute(string description)
        {
            this.Description = description;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class DrawMethodAttribute : Attribute
    {
        public readonly string MethodName;
        public bool SerializeField = true;

        public DrawMethodAttribute(string methodName)
        {
            this.MethodName = methodName;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class SettingOptionsAttribute : Attribute
    {
        public static SettingOptionsAttribute CreateDefault() => new();

        public readonly bool DrawDescription = true;
        public readonly bool DrawValue = true;
        public readonly bool AllowReset = true;
        public readonly bool DrawHoverHighlight = true;

        public SettingOptionsAttribute(bool drawDescription = true, bool drawValue = true,
                                       bool allowReset = true, bool drawHoverHighlight = true)
        {
            DrawDescription = drawDescription;
            DrawValue = drawValue;
            AllowReset = allowReset;
            DrawHoverHighlight = drawHoverHighlight;
        }

        private SettingOptionsAttribute() { }
    }
}
