namespace AM.AMSettings.Presets;

public class VanillaPlus : Settings
{
    public override string PresetName => "Vanilla+";
    public override string PresetDescription => @"<i>Keeps things very close to vanilla combat. Your pawns will not use lassos unless you manually instruct them to. Your pawns can not do executions, ever. Enemies will never do executions or use lassos.</i>

 * Adds melee attack animations.
 * Adds melee weapon idle animations
 * Lassos can be used by friendly pawns, but must be equipped and triggered manually.";

    public VanillaPlus()
    {
        EnemiesCanExecute = false;
        EnemiesCanGrapple = false;
        AutoGrapple = false;
        AutoExecute = false;
        ShowHands = false;
        LassoSpawnChance = 0f;
        EnableUniqueSkills = false;
        EnableExecutions = false;
    }
}