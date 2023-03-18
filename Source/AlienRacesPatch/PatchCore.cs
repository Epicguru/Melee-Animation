using System;
using AlienRace;
using AM;
using Verse;

namespace AM.AlienRacesPatch;

[HotSwapAll]
public class PatchCore : Mod
{
    public static void Log(string msg)
    {
        Core.Log($"<color=#ffa8fc>[Alien Races Patch]</color> {msg}");
    }

    public PatchCore(ModContentPack content) : base(content)
    {
        Log("Loaded alien races patch!");

        Core.GetBodyDrawSizeFactor = GetBodySizeFactor;
    }

    public static float GetBodySizeFactor(Pawn pawn)
    {
        try
        {
            if (pawn?.def is not ThingDef_AlienRace race)
                return 1f;

            return race.alienRace.generalSettings.alienPartGenerator.customDrawSize.x;
        }
        catch (Exception e)
        {
            Log($"Error getting body size factor:\n{e}");
            return 1f;
        }
    }
}
