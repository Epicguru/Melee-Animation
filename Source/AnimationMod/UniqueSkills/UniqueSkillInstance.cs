using Verse;

namespace AM.UniqueSkills;

public abstract class UniqueSkillInstance : IExposable
{
    public UniqueSkillDef Def;
    public Pawn Pawn;

    public int TickLastTriggered = -1000000;

    public virtual void ExposeData()
    {
        Scribe_Defs.Look(ref Def, "def");
        Scribe_References.Look(ref Pawn, "pawn");
        Scribe_Values.Look(ref TickLastTriggered, "tickLastTriggered", -1000000);
    }

    public float GetCooldownSecondsLeft()
    {
        int ticksSinceLast = GenTicks.TicksGame - TickLastTriggered;
        return (Def.baseCooldownTicks - ticksSinceLast) / 60f;
    }

    /// <summary>
    /// Called when an animation has started.
    /// </summary>
    public virtual void OnAnimationStarted(AnimRenderer animation) { }

    public virtual void Tick() { }

    public abstract bool IsEnabledForPawn(out string reasonWhyNot);

    public abstract bool TryTrigger(in LocalTargetInfo target);

    public abstract string CanTriggerOn(in LocalTargetInfo target);

    public override string ToString() => $"{Def}\n" +
                                         $"{Pawn}\n" +
                                         $"Enabled: {IsEnabledForPawn(out var reason)} ({reason})";
}