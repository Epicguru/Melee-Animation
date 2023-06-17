using Verse;

namespace AM.UniqueSkills;

public abstract class UniqueSkillInstance : IExposable
{
    public UniqueSkillDef Def;
    public Pawn Pawn;

    public virtual void ExposeData()
    {
        Scribe_Defs.Look(ref Def, "def");
        Scribe_References.Look(ref Pawn, "pawn");
    }

    public virtual void Tick() { }

    public abstract bool IsEnabledForPawn(out string reasonWhyNot);

    public abstract bool TryTrigger(in LocalTargetInfo target);

    public abstract string CanTriggerOn(in LocalTargetInfo target);

    public override string ToString() => $"{Def}\n" +
                                         $"{Pawn}\n" +
                                         $"Enabled: {IsEnabledForPawn(out var reason)} ({reason})";
}