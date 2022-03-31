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
        public static RulePackDef AAM_Execution_Generic;
        public static ThingDef AAM_GrappleFlyer;
    }
}
