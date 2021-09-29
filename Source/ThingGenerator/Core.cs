using System;
using ReloaderAPI;
using Verse;

namespace ThingGenerator
{
    [Reloadable]
    public class Core : Mod
    {
        public static void Log(string msg)
        {
            Verse.Log.Message($"<color=green>[ThingGenerator]</color> {msg}");
        }

        public static void Warn(string msg)
        {
            Verse.Log.Warning($"<color=green>[ThingGenerator]</color> {msg}");
        }

        public static void Error(string msg, Exception e = null)
        {
            Verse.Log.Error($"<color=green>[ThingGenerator]</color> {msg}");
            if (e != null)
                Verse.Log.Error(e.ToString());
        }

        public Core(ModContentPack content) : base(content)
        {
            Log("Hello, world!");
        }
    }
}
