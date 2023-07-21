using HarmonyLib;
using RimWorld;
using System;
using System.Diagnostics;
using System.Text;
using Verse;

namespace AM.Patches;

// Patched manually below, see PatchAll
public static class Patch_Verb_MeleeAttack_ApplyMeleeDamageToTarget
{
    public static Thing lastTarget;

    private static readonly Type[] methodParams = { typeof(LocalTargetInfo) };
    private static readonly HarmonyMethod postfix = new HarmonyMethod(typeof(Patch_Verb_MeleeAttack_ApplyMeleeDamageToTarget), nameof(Postfix));

    public static void PatchAll()
    {
        var log = new StringBuilder();
        var timer = Stopwatch.StartNew();
        int count = 0;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var klass in asm.GetTypes())
            {
                if (klass.IsAbstract)
                    continue;

                if (klass.BaseType != typeof(Verb_MeleeAttack))
                    continue;

                try
                {
                    var method = AccessTools.Method(klass, nameof(Verb_MeleeAttack.ApplyMeleeDamageToTarget), methodParams);
                    if (method == null)
                        throw new Exception($"Failed to find {nameof(Verb_MeleeAttack.ApplyMeleeDamageToTarget)} method in {klass.FullName}: failed to find method");

                    Core.Harmony.Patch(method, postfix: postfix);

                    count++;
                    log.Append(klass.FullName).Append(" from ").AppendLine(asm.GetName().Name);
                }
                catch (Exception e)
                {
                    Core.Error($"Failed to patch {klass.FullName}'s {nameof(Verb_MeleeAttack.ApplyMeleeDamageToTarget)}:", e);
                }
            }
        }

        timer.Stop();
        Core.Log($"Patched {count} classes that directly inherit from {nameof(Verb_MeleeAttack)} in {timer.Elapsed.TotalMilliseconds:F1} ms to detect hits:\n{log}");
    }

    public static void Postfix(LocalTargetInfo target)
    {
        lastTarget = target.Thing;
    }
}
