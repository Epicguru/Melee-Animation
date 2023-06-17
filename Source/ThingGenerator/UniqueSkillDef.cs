using AM.UniqueSkills;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Verse;

namespace AM;

[UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public class UniqueSkillDef : Def
{
    public SkillType type = SkillType.Invalid;
    public AnimDef animation;
    public Type instanceClass;

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
}
