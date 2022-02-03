using System;
using System.Collections.Generic;
using UnityEngine;

namespace AAM.Events;

public abstract class EventBase : ScriptableObject
{
    public static Dictionary<Type, Func<string, object>> Parsers = new Dictionary<Type, Func<string, object>>()
    {
        { typeof(string), s => s },
        { typeof(float), s => float.Parse(s) },
        { typeof(int), s => int.Parse(s) },
        { typeof(bool), s => bool.Parse(s) },
        { typeof(Vector2), s => new Vector2(FloatParse(s.Split(',')[0]), FloatParse(s.Split(',')[1])) },
        { typeof(Vector3), s => new Vector3(FloatParse(s.Split(',')[0]), FloatParse(s.Split(',')[1]), FloatParse(s.Split(',')[2])) },
        { typeof(Color), s => new Color(FloatParse(s.Split(',')[0]), FloatParse(s.Split(',')[1]), FloatParse(s.Split(',')[2]),  FloatParse(s.Split(',')[3])) },
    };

    private static Dictionary<string, Func<EventBase>> allBases;

    public static EventBase CreateFromSaveData(string data)
    {
        var split = data.Split(';');
        Core.Log($"Split: {string.Join(", ", split)}");
        string id = split[0];
        if (!allBases.TryGetValue(id, out var maker))
            return null;

        var e = maker();
        e.LoadFromData(data);
        return e;
    }

    private static float FloatParse(string s)
    {
        return float.Parse(s.Replace("(", "").Replace(")", ""));
    }

    static EventBase()
    {
        allBases = new Dictionary<string, Func<EventBase>>();
        var types = Verse.GenTypes.AllSubclassesNonAbstract(typeof(EventBase));
        foreach (var type in types)
        {
            var instance = Activator.CreateInstance(type) as EventBase;
            string id = instance.EventID;
            allBases.Add(id, () => Activator.CreateInstance(type) as EventBase);
        }

        Core.Log($"Loaded {allBases.Count} EventBases.");
    }

    public abstract string EventID { get; }
    public float Time;
    public int Index;

    public abstract void Expose();

    private string[] split;
    private int splitIndex;
    private string saveDataInt;
    private bool reading;

    public virtual bool IsInTimeWindow(Vector2 timeWindow)
    {
        return (Time == 0 ? Time >= timeWindow.x : Time > timeWindow.x) && Time <= timeWindow.y;
    }

    private void Write<T>(T obj)
    {
        if (saveDataInt.Length != 0)
            saveDataInt += ';';

        saveDataInt += (obj switch
        {
            Vector2 vec2 => $"{vec2.x},{vec2.y}",
            Vector3 vec3 => $"{vec3.x},{vec3.y},{vec3.z}",
            Color    col => $"{col.r},{col.g},{col.b},{col.a}",

            _ => obj.ToString()
        }).Trim();
    }

    private void Read<T>(out T variable, T fallback = default)
    {
        string s = ReadNext();
        if (Parsers.TryGetValue(typeof(T), out var parser))
        {
            var parsed = (T)parser(s);
            variable = parsed ?? fallback;
            return;
        }
        variable = fallback;
    }

    protected void Look<T>(ref T obj, T fallback = default)
    {
        if (reading)
            Read(out obj, fallback);
        else
            Write(obj);
    }

    public string MakeSaveData()
    {
        saveDataInt = EventID;
        if (this is TimedEvent te)
        {
            saveDataInt += $";{te.When}";
        }
        reading = false;
        Expose();
        return saveDataInt;
    }

    public void LoadFromData(string saveData)
    {
        reading = true;
        split = saveData.Split(';');
        splitIndex = 1;
        if (this is TimedEvent te)
        {
            string when = ReadNext();
            te.When = (EventTime)Enum.Parse(typeof(EventTime), when);
        }
        Expose();
    }

    private string ReadNext()
    {
        return split[splitIndex++];
    }
}