using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace AAM.Patches
{
    /// <summary>
    /// Temporarily removes the aggressive patching done by Yayo's Animation
    /// when a melee animation is playing.
    /// </summary>
    public static class YayoKiller
    {
        public static void Init(Harmony harmony)
        {
            // From early testing, it seems that my patches take priority over Yayo's in almost all situations.
            return;

            const string TARGET = "com.yayo.yayoAni";

            int count = 0;
            foreach (var method in Harmony.GetAllPatchedMethods())
            {
                var patches = Harmony.GetPatchInfo(method);

                foreach ((PatchType type, Patch patch) in patches.GetAllPatches(p => p.owner.Contains(TARGET)))
                {
                    AddPatchKiller(harmony, method, type, patch);
                    count++;
                }
            }

            Core.Log($"Found {count} Yayo Animation patch methods. Prepare to be nuked, aggressive patches!");
        }

        private static void AddPatchKiller(Harmony harmony, MethodBase original, PatchType type, Patch patch)
        {
            switch (type)
            {
                //case PatchType.Prefix:
                //    break;

                default:
                    Core.Warn($"Yayo patches '{original.Name}' with a {type}.");
                    //Core.Error($"Unhandled Yayo patch of type '{type}': {original.Name}");
                    break;
            }
        }

        private static IEnumerable<(PatchType type, Patch patch)> GetAllPatches(this HarmonyLib.Patches patches, Func<Patch, bool> selector = null)
        {
            selector ??= _ => true;

            // Prefixes
            foreach (var patch in patches.Prefixes)            
                if (selector(patch))
                    yield return (PatchType.Prefix, patch);

            // Transpilers
            foreach (var patch in patches.Transpilers)
                if (selector(patch))
                    yield return (PatchType.Transpiler, patch);

            // Postfix.
            foreach (var patch in patches.Postfixes)
                if (selector(patch))
                    yield return (PatchType.Postfix, patch);

            // Finalizers.
            foreach (var patch in patches.Finalizers)
                if (selector(patch))
                    yield return (PatchType.Finalizer, patch);
        }

        private enum PatchType
        {
            Prefix,
            Transpiler,
            Postfix,
            Finalizer
        }
    }
}
