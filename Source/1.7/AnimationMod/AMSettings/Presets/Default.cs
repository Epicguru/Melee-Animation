using JetBrains.Annotations;

namespace AM.AMSettings.Presets;

[UsedImplicitly]
public class Default : Settings
{
    public override string PresetName => "Default";
    public override string PresetDescription => @"<i>Enables all features the mod has to offer</i>";
}
