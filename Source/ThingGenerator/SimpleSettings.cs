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
        public static Func<MemberWrapper, DrawHandler> SelectDrawHandler = DefaultDrawHandlerSelector;
        public static Dictionary<Type, DrawHandler> DrawHandlers = new Dictionary<Type, DrawHandler>()
        {
            { typeof(string), DrawStringField },
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
        
        private static readonly Dictionary<Type, FieldHolder> settingsFields = new Dictionary<Type, FieldHolder>();
        private static Stack<ScribeSaver> saverStack = new Stack<ScribeSaver>();
        private static Stack<ScribeLoader> loaderStack = new Stack<ScribeLoader>();
        private static Stack<LoadSaveMode> modeStack = new Stack<LoadSaveMode>();

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
                object old = member.Get<object>(settings);
                member.Expose(settings);
                object now = member.Get<object>(settings);
                Core.Log($"[{Scribe.mode}] {member.Name}: {old} -> {now}");
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
            var created = Activator.CreateInstance(type) as IExposable;
            if (created == null)
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

            var holder = GetHolder(settings);

            Widgets.BeginScrollView(inRect, ref holder.UI_Scroll, new Rect(0, 0, holder.UI_LastSize.x, holder.UI_LastSize.y));
            Vector2 size = Vector2.zero;
            Vector2 pos = Vector2.zero;

            foreach (var member in holder.Members.Values)
            {
                var handler = SelectDrawHandler(member);
                if (handler == null)
                    continue;

                // TODO Check if it should be drawn...
                var area = new Rect(pos, new Vector2(inRect.width, inRect.height - pos.y));
                Rect drawn = handler(settings, member, area);
                pos.y += drawn.height;
                size.y += drawn.height;
                size.x = Mathf.Max(size.x, drawn.width);
            }

            Widgets.EndScrollView();
            holder.UI_LastSize = size;
        }

        public static DrawHandler DefaultDrawHandlerSelector(MemberWrapper wrapper)
        {
            if (wrapper == null)
                return null;

            var type = wrapper.MemberType;
            if (DrawHandlers.TryGetValue(type, out var found))
                return found;

            return null;

        }

        private static Rect DrawSlider(ModSettings settings, MemberWrapper member, Rect area, float min, float max)
        {
            Type type = member.MemberType;
            bool isFloatType = type == typeof(float) || type == typeof(double) || type == typeof(decimal);

            var value = member.Get<float>(settings);
            var defaultValue = member.GetDefault<float>();
            string name = member.Name;
            area.height = 40;
            var ret = area;
            area = area.ExpandedBy(-10);

            float updated = Widgets.HorizontalSlider(area, value, min, max, label: $"{name} : {value} (def: {defaultValue})", leftAlignedLabel: min.ToString(), rightAlignedLabel: max.ToString());
            if (updated != value)
            {

                updated = Mathf.Clamp(updated, min, max);
                object writeBack = updated;
                if (!isFloatType)
                    writeBack = (long)Math.Round(updated);
                Core.Log($"Updating {name} from {value} to {writeBack}");
                member.Set(settings, writeBack);
            }

            return ret;
        }

        private static float GetNumericMin(Type type)
        {
            return (float)Convert.ChangeType(type.GetField("MinValue", BindingFlags.Public | BindingFlags.Static).GetValue(null), typeof(float));
        }

        private static float GetNumericMax(Type type)
        {
            return (float)Convert.ChangeType(type.GetField("MaxValue", BindingFlags.Public | BindingFlags.Static).GetValue(null), typeof(float));
        }

        private static Rect DrawNumeric(ModSettings settings, MemberWrapper wrapper, Rect area)
        {
            // Default: do a slider.
            var range = wrapper.TryGetCustomAttribute<UnityEngine.RangeAttribute>();
            float min = float.MinValue;
            float max = float.MaxValue;

            if (range != null)
                min = Mathf.Max(min, range.min);
            else
                min = Mathf.Max(min, 0);
            min = Mathf.Max(min, GetNumericMin(wrapper.MemberType));

            if (range != null)
                max = Mathf.Min(max, range.max);
            else
                max = Mathf.Min(max, 100);
            max = Mathf.Min(max, GetNumericMax(wrapper.MemberType));

            return DrawSlider(settings, wrapper, area, min, max);
        }

        private static Rect DrawStringField(ModSettings settings, MemberWrapper member, Rect area)
        {
            string value = member.Get<string>(settings);
            string defaultValue = member.GetDefault<string>();
            string name = member.Name;
            area.height = 40;
            var ret = area;
            area = area.ExpandedBy(-10);

            string updated = Widgets.TextField(area, value);
            //string updated = Widgets.TextEntryLabeled(area, name + " :  ", value);
            if (updated != value)
                member.Set(settings, updated);

            return ret;
        }

        private static Rect DrawToggle(ModSettings settings, MemberWrapper member, Rect area)
        {
            var value = member.Get<bool>(settings);
            var defaultValue = member.GetDefault<bool>();
            string name = member.Name;
            area.height = 40;
            var ret = area;
            area = area.ExpandedBy(-10);

            bool changed = value;
            Widgets.CheckboxLabeled(area, name, ref changed, placeCheckboxNearText: true);
            if (changed != value)
                member.Set(settings, changed);

            return ret;
        }

        public delegate Rect DrawHandler(ModSettings settings, MemberWrapper member, Rect area);

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
            public readonly Dictionary<MemberInfo, MemberWrapper> Members = new Dictionary<MemberInfo, MemberWrapper>();
            public Vector2 UI_LastSize;
            public Vector2 UI_Scroll;

            public FieldHolder(ModSettings settings, Type forType)
            {
                this.ForSettingsObject = settings;
                this.ForType = forType;

                foreach (var member in ForType.GetMembers(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (member is not FieldInfo && member is not PropertyInfo)
                        continue;

                    bool isInvalidProp = member is PropertyInfo pi && (!pi.CanWrite || !pi.CanRead);
                    if (isInvalidProp)
                        continue;

                    Members.Add(member, MakeWrapperFor(settings, member));
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
            public string Name => field?.Name ?? prop?.Name;
            public readonly object DefaultValue;
            public Type MemberType => field?.FieldType ?? prop.PropertyType;
            public bool IsIExposable => MemberType.GetInterfaces().Contains(typeof(IExposable));
            public bool IsValueType => MemberType.IsValueType;
            public bool IsDefType => typeof(Def).IsAssignableFrom(MemberType);
            public bool IsStatic => field?.IsStatic ?? prop.GetMethod.IsStatic;
            public string NameInXML => Name;
            public IEnumerable<Attribute> CustomAttributes => field?.GetCustomAttributes() ?? prop.GetCustomAttributes();

            protected readonly FieldInfo field;
            protected readonly PropertyInfo prop;

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

                DefaultValue = GetDefaultValue(obj);
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
}
