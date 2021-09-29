using System.Collections.Generic;
using Verse;

namespace ThingGenerator.Data
{
    public class ToolData : IExposable
    {
        public string label;

        public float power;
        public float cooldownTime;
        public float armorPenetration;

        public List<ToolCapacityDef> capacities = new List<ToolCapacityDef>();
        public int? overrideIndex;
        public bool delete;

        // Temp - runtime only.
        public string powerBuffer;
        public string cooldownBuffer;
        public string armorPenetrationBuffer;

        public ToolData() { }

        public ToolData(Tool tool, int? overrideIndex)
        {
            label = tool.label;
            power = tool.power;
            cooldownTime = tool.cooldownTime;
            armorPenetration = tool.armorPenetration;

            this.overrideIndex = overrideIndex;

            if (tool.capacities != null)
                capacities.AddRange(tool.capacities);

            powerBuffer = power.ToString();
            cooldownBuffer = cooldownTime.ToString();
            armorPenetrationBuffer = armorPenetration.ToString();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref delete, "delete");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref power, "power");
            Scribe_Values.Look(ref armorPenetration, "armorPenetration");
            Scribe_Values.Look(ref cooldownTime, "cooldownTime");
            Scribe_Values.Look(ref overrideIndex, "overrideIndex");
        }
    }
}
