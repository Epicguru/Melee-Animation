using System.Collections.Generic;
using System.IO;
using Verse;

namespace ThingGenerator
{
    [HotSwappable]
    public static class IO
    {
        private static string linksFile = @"C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\ThingGenerator\Save Data\Links.xml";
        private static Dictionary<string, string> links;

        public static void WriteLink(string fileName, string label)
        {
            if (links == null)
                InitLinks();

            links[fileName] = label;

            Scribe.saver.InitSaving(linksFile, "LinkData");

            Scribe_Collections.Look(ref links, "links", LookMode.Value, LookMode.Value);

            Scribe.saver.FinalizeSaving();
        }

        public static string TryGetLink(string fileName)
        {
            if (links == null)
                InitLinks();

            return links.TryGetValue(fileName);
        }

        private static void InitLinks()
        {
            if (!File.Exists(linksFile))
            {
                links = new Dictionary<string, string>();
                return;
            }

            Scribe.loader.InitLoading(linksFile);
            Scribe.loader.EnterNode("LinkData");

            Scribe_Collections.Look(ref links, "links", LookMode.Value, LookMode.Value);
            links ??= new Dictionary<string, string>();

            Scribe.loader.FinalizeLoading();
        }

        public static void SaveToFile(IExposable item, string filePath, string label)
        {
            Scribe.saver.InitSaving(filePath, "CustomItemData");

            item.ExposeData();

            Scribe.saver.FinalizeSaving();

            WriteLink(new FileInfo(filePath).Name, label);
        }

        public static void LoadFromFile(IExposable item, string filePath)
        {
            Scribe.loader.InitLoading(filePath);

            Scribe.loader.EnterNode("CustomItemData");
            item.ExposeData();

            Scribe.loader.FinalizeLoading();
        }

        public static IEnumerable<(FileInfo file, string label)> ListXmlFiles(string directory)
        {
            var dir = new DirectoryInfo(directory);
            if (!dir.Exists)
                yield break;

            foreach (var file in dir.EnumerateFiles("*.xml", SearchOption.TopDirectoryOnly))
            {
                if (file.Name == "Links.xml")
                    continue;

                string name = TryGetLink(file.Name) ?? file.Name;
                yield return (file, name);
            }
        }
    }
}
