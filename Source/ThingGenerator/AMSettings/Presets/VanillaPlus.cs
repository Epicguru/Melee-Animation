namespace AM.AMSettings.Presets;

public class VanillaPlus : Settings
{
    public override string PresetName => "Vanilla+";
    public override string PresetDescription => @"<i>Keeps things very close to vanilla combat. Your pawns will not do executions or use lassos unless you manually instruct them to. Enemies will never do executions or use lassos.</i>

 * Adds melee attack animations.
 * Adds melee weapon idle animations";

    public VanillaPlus()
    {
        EnemiesCanExecute = false;
        EnemiesCanGrapple = false;
        AutoGrapple = false;
        AutoExecute = false;
        ShowHands = false;
        LassoSpawnChance = 0f;
    }
}