using Verse;

namespace ThingGenerator
{
    public class XmlOverride : IExposable
    {
        public string ID;
        public string XPath;
        public string Value;

        public XmlOverride() { }

        public XmlOverride(string id)
        {
            this.ID = id;
        }

        public XmlOverride Set(string path, string value)
        {
            this.XPath = path;
            this.Value = value;
            return this;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref XPath, "xpath");
            Scribe_Values.Look(ref Value, "value");
            Scribe_Values.Look(ref ID, "id");
        }
    }
}
