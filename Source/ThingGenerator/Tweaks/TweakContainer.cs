using System.Collections.Generic;
using Verse;

namespace AAM.Tweaks
{
    public class TweakContainer : IExposable
    {
        public string ModID;
        public string ModName;
        public string Author;
        public List<ItemTweakData> Items = new List<ItemTweakData>();

        public TweakContainer() { }

        public TweakContainer(ModContentPack mcp)
        {
            ModID = ItemTweakData.MakeModID(mcp);
            ModName = mcp.Name;
            Author = SteamUtility.SteamPersonaName ?? "(anon)";
            Items = new List<ItemTweakData>();
        }

        public ModContentPack TryResolveMod() => LoadedModManager.RunningModsListForReading.FirstOrFallback(m => ItemTweakData.MakeModID(m) == ModID);

        public void PullActive()
        {
            Items.Clear();
            Items.AddRange(TweakDataManager.GetTweaksForMod(TryResolveMod()));
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref ModID, "mod");
            Scribe_Values.Look(ref ModName, "modName");
            Scribe_Values.Look(ref Author, "author");
            Scribe_Collections.Look(ref Items, "items", LookMode.Deep);
        }
    }
}
