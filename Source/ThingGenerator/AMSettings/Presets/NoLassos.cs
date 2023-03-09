namespace AM.AMSettings.Presets;

public class NoLassos : Settings
{
    public override string PresetName => "No Lassos";
    public override string PresetDescription => @"<i>Everything is enabled except lassos. You can still craft and use lassos manually if you want but enemies will not spawn with or use lassos.</i>

 * Adds melee attack animations.
 * Adds melee weapon idle animations
 * Adds duels
 * Adds execution animations";

    public NoLassos()
    {
        EnemiesCanGrapple = false;
        AutoGrapple = false;
        LassoSpawnChance = 0f;
    }
}