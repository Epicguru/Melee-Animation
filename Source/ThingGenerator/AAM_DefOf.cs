using RimWorld;
using Verse;

namespace AAM
{
    [DefOf]
    public static class AAM_DefOf
    {
        static AAM_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(AAM_DefOf));
        }

        public static JobDef AAM_InAnimation;
        public static JobDef AAM_GrapplePawn;
        public static JobDef AAM_WalkToExecution;
        public static RulePackDef AAM_Execution_Generic;
        public static ThingDef AAM_GrappleFlyer;
        public static ThingDef AAM_KnockbackFlyer;
        public static StatDef AAM_GrappleSpeed;
        public static StatDef AAM_GrappleCooldown;
        public static StatDef AAM_ExecutionCooldown;
        public static StatDef AAM_GrappleRadius;
        public static StatDef AAM_Lethality;
        public static StatDef AAM_DuelAbility;

        public static ToolCapacityDef Blunt;
        public static ToolCapacityDef Cut;
        public static ToolCapacityDef Stab;
    }
}
