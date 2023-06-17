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

    public abstract bool IsEnabledForPawn();
}