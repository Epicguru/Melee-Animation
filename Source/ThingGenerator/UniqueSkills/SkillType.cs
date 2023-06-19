namespace AM.UniqueSkills;

public enum SkillType
{
    /// <summary>
    /// An invalid skill type.
    /// </summary>
    Invalid,

    /// <summary>
    /// This unique skill is a special execution.
    /// </summary>
    Execution,

    /// <summary>
    /// This unique skill applies an animation to a target, and the attacking pawn enters a channeling state
    /// until the animation has finished.
    /// </summary>
    ChanneledAnimation
}
