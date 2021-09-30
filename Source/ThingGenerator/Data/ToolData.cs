using System.Collections.Generic;
using Verse;

namespace ThingGenerator.Data
{
    public class ToolData : IExposable
    {
        public string label;

        public float power = 20f;
        public float cooldownTime = 1f;
        public float armorPenetration = 0f;
        public float chanceFactor = 1f;

        public List<ToolCapacityDef> capacities = new List<ToolCapacityDef>();
        public int? overrideIndex;
        public bool delete;

        // Temp - runtime only.
        public string powerBuffer;
        public string cooldownBuffer;
        public string armorPenetrationBuffer;
        public string chanceFactorBuffer;

        public ToolData()
        {
            SetBuffers();
        }

        public ToolData(Tool tool, int? overrideIndex) : this()
        {
            label = tool.label;
            power = tool.power;
            cooldownTime = tool.cooldownTime;
            armorPenetration = tool.armorPenetration;
            chanceFactor = tool.chanceFactor;

            this.overrideIndex = overrideIndex;

            if (tool.capacities != null)
                capacities.AddRange(tool.capacities);
        }

        private void SetBuffers()
        {
            powerBuffer = power.ToString();
            cooldownBuffer = cooldownTime.ToString();
            armorPenetrationBuffer = armorPenetration.ToString();
            chanceFactorBuffer = chanceFactor.ToString();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref delete, "delete", false);
            Scribe_Values.Look(ref label, "label", "my capacity");
            Scribe_Values.Look(ref power, "power", 20f);
            Scribe_Values.Look(ref chanceFactor, "chanceFactor", 1f);
            Scribe_Values.Look(ref armorPenetration, "armorPenetration", 0f);
            Scribe_Values.Look(ref cooldownTime, "cooldownTime", 1f);
            Scribe_Values.Look(ref overrideIndex, "overrideIndex", null);

            Scribe_Collections.Look(ref capacities, "capacities", LookMode.Def);
            capacities ??= new List<ToolCapacityDef>();
        }
    }
}
