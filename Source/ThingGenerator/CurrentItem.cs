using System.Collections.Generic;
using ThingGenerator.Data;
using Verse;

namespace ThingGenerator
{
    public class CurrentItem : IExposable
    {
        public bool IsValid => BaseThingDef != null;

        public ItemType Type
        {
            get
            {
                if (!IsValid)
                    return ItemType.Invalid;

                if (BaseThingDef.IsMeleeWeapon)
                    return ItemType.Melee;
                if (BaseThingDef.IsRangedWeapon)
                    return ItemType.Gun;

                return ItemType.Apparel;
            }
        }

        public string DefName
        {
            get
            {
                return TryGetOverride("DefName")?.Value;
            }
            set
            {
                GetOrAddOverride("DefName").Value = value;
            }
        }

        public string Label
        {
            get
            {
                return TryGetOverride("Label")?.Value;
            }
            set
            {
                GetOrAddOverride("Label").Value = value;
            }
        }

        public string LabelCap => Label?.CapitalizeFirst();

        public ThingDef BaseThingDef;

        private List<ToolData> toolData = new List<ToolData>();
        private List<XmlOverride> xmlOverrides = new List<XmlOverride>();
        private readonly Dictionary<string, XmlOverride> xmlOverridesDict = new Dictionary<string, XmlOverride>();

        public XmlOverride TryGetOverride(string id) => xmlOverridesDict.TryGetValue(id);

        public XmlOverride GetOrAddOverride(string id)
        {
            var found = TryGetOverride(id);
            if (found != null)
                return found;
            var created = new XmlOverride(id);
            AddOverride(created);
            return created;
        }

        public void AddOverride(XmlOverride xo)
        {
            if (xo?.ID == null || xmlOverridesDict.ContainsKey(xo.ID))
            {
                Core.Error("Bad xml override! Check stack trace!");
                return;
            }

            xmlOverrides.Add(xo);
            xmlOverridesDict.Add(xo.ID, xo);
        }

        public IEnumerable<ToolData> GetAllToolData()
        {
            return toolData;
        }

        public void AddToolData(ToolData data)
        {
            if(data != null)
                toolData.Add(data);
        }

        public void RemoveToolData(ToolData data)
        {
            if (data != null && toolData.Contains(data))
                toolData.Remove(data);
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref BaseThingDef, "baseThingDef");
            Scribe_Collections.Look(ref toolData, "toolData", LookMode.Deep);
            Scribe_Collections.Look(ref xmlOverrides, "xmlOverrides", LookMode.Deep);


            xmlOverrides ??= new List<XmlOverride>();
            toolData ??= new List<ToolData>();
            RebuildOverridesDictionary();
        }

        private void RebuildOverridesDictionary()
        {
            xmlOverridesDict.Clear();
            foreach (var item in xmlOverrides)
            {
                xmlOverridesDict.Add(item.ID, item);
            }
        }
    }
}
