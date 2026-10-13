using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using AM.UniqueSkills;
using JetBrains.Annotations;
using Verse;

namespace AM;

[UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public class UniqueSkillDef : Def
{
    public SkillType type = SkillType.Invalid;
    public AnimDef animation;
    public Type instanceClass;
    public int baseCooldownTicks = 60 * 60 * 10; // 10 minutes.

    [XmlIgnore]
    private Dictionary<string, DataRow> dataMap;
    private List<DataRow> data = new List<DataRow>();

    public T GetData<T>(string key, Func<string, T> parse, in T defaultValue = default)
    {
        if (!dataMap.TryGetValue(key, out var found))
            return defaultValue;

        if (found.CachedValue is T t)
            return t;

        T parsed;
        try
        {
            parsed = parse(found.Value);
        }
        catch
        {
            Core.Error($"Failed to parse '{found.Value}' as a {typeof(T).FullName}. Returning default value {defaultValue}");
            parsed = defaultValue;
        }

        found.CachedValue = parsed;
        return parsed;
    }

    public override void PostLoad()
    {
        base.PostLoad();
        dataMap = data.ToDictionary(row => row.Key);
    }

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (var error in base.ConfigErrors())
            yield return error;

        if (type == SkillType.Invalid)
            yield return "<type> must be specified.";

        if (animation == null)
            yield return "<animation> must be specified.";

        if (instanceClass == null)
            yield return "<instanceClass> must be specified.";
        else if (!instanceClass.IsSubclassOf(typeof(UniqueSkillInstance)))
            yield return $"<instanceClass> '{instanceClass}' does not inherit from UniqueSkillInstance!";
    }

    public class DataRow
    {
        public string Key;
        public string Value;
        public object CachedValue;

        [UsedImplicitly] // Needs to be a public method (not private!) as of Rimworld 1.6.
        public void LoadDataFromXmlCustom(XmlNode node)
        {
            Key = node.Name;
            Value = node.InnerText;
        }
    }
}
