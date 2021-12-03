using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AAM.Tweaks
{
    public static class TweakDataManager
    {
        private static Dictionary<ThingDef, ItemTweakData> itemTweaks = new Dictionary<ThingDef, ItemTweakData>();

        public static ItemTweakData TryGetTweak(ThingDef def)
        {
            if (def == null)
                return null;

            if (itemTweaks.TryGetValue(def, out var found))            
                return found;
            
            return null;
        }

        public static ItemTweakData GetOrCreateDefaultTweak(ThingDef def)
        {
            var found = TryGetTweak(def);
            if (found != null)
                return found;

            var created = new ItemTweakData(def);
            RegisterTweak(def, created);
            Core.Warn($"Created default item data for missing item: '{def.LabelCap}'");
            return created;
        }

        public static bool TweakExistsFor(ThingDef def) => TryGetTweak(def) != null;

        public static void RegisterTweak(ItemTweakData tweak)
        {
            RegisterTweak(tweak?.GetDef(false, false), tweak);
        }

        public static void RegisterTweak(ThingDef forDef, ItemTweakData tweak)
        {
            if (forDef == null)
                throw new System.ArgumentNullException(nameof(forDef));
            if (tweak == null)
                throw new System.ArgumentNullException(nameof(tweak));

            var target = tweak.GetDef(false, false);
            if (forDef != target)
                Core.Warn($"Adding tweak that does not correspond to tweak data's target: Target of tweak is {target.LabelCap}, but adding for {forDef.LabelCap}");

            itemTweaks[forDef] = tweak;
        }

        public static void Reset()
        {
            itemTweaks.Clear();
        }

        public static Texture2D ToTexture2D(this RenderTexture rTex)
        {
            Texture2D tex = new Texture2D(rTex.width, rTex.height, TextureFormat.RGBA32, false);
            var old_rt = RenderTexture.active;
            RenderTexture.active = rTex;

            tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
            tex.Apply();

            RenderTexture.active = old_rt;
            return tex;
        }

        public static (int done, int total) GetModCompletionStatus(ModContentPack mcp)
        {
            if (mcp == null)
                return (0, 0);

            int done = itemTweaks.Keys.Count(d => d.modContentPack == mcp);
            int total = mcp.AllDefs.Count(d => d is ThingDef td && td.IsMeleeWeapon);

            return (done, total);
        }

        public static IEnumerable<ItemTweakData> GetTweaksForMod(ModContentPack mcp)
        {
            if (mcp == null)
                yield break;

            foreach(var pair in itemTweaks)
            {
                if (pair.Key.modContentPack == mcp)
                    yield return pair.Value;
            }
        }
    }
}
