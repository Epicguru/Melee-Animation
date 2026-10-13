using RimWorld;
using Verse;

namespace AM;

[DefOf]
public static class AM_DefOf
{
    static AM_DefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(AM_DefOf));
    }

    public static JobDef AM_InAnimation;
    public static JobDef AM_GrapplePawn;
    public static JobDef AM_WalkToExecution;
    public static JobDef AM_DoFriendlyDuel;
    public static JobDef AM_SpectateFriendlyDuel;
    public static JobDef AM_ChannelAnimation;

    public static RulePackDef AM_Execution_Generic;

    public static ThingDef AM_GrappleFlyer;
    public static ThingDef AM_KnockbackFlyer;

    public static StatDef AM_GrappleSpeed;
    public static StatDef AM_GrappleCooldown;
    public static StatDef AM_ExecutionCooldown;
    public static StatDef AM_GrappleRadius;
    public static StatDef AM_Lethality;
    public static StatDef AM_DuelAbility;

    public static AnimDef AM_Duel_WinFriendlyDuel;
    public static AnimDef AM_Duel_WinFriendlyDuel_Reject;
    public static AnimDef AM_Execution_Fail;

    public static SoundDef AM_MetalSwordClash;
    public static SoundDef AM_StoneSwordClash;
    public static SoundDef AM_WoodSwordClash;

    public static ToolCapacityDef Blunt;
    public static ToolCapacityDef Cut;
    public static ToolCapacityDef Stab;

    public static ThoughtDef AM_FriendlyDuel_Win;
    public static ThoughtDef AM_FriendlyDuel_Lose;

    public static HediffDef AM_KnockedOut;

    public static RenderSkipFlagDef Body;

    public static BodyPartGroupDef Hands;
}