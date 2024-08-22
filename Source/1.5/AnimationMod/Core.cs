using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using AM.AMSettings;
using AM.Data;
using AM.Hands;
using AM.Patches;
using AM.Retexture;
using AM.Tweaks;
using HarmonyLib;
using ModRequestAPI;
using ModRequestAPI.Models;
using RimWorld;
using UnityEngine;
using Verse;

namespace AM;

[HotSwapAll]
public class Core : Mod
{
    public const bool ENABLE_PATCH_REQUEST_API = false;

    public static readonly HashSet<ThingDef> ForceConsiderTheseMeleeWeapons = new HashSet<ThingDef>();
    public static Func<Pawn, float> GetBodyDrawSizeFactor = _ => 1f;
    public static string ModTitle => ModContent?.Name;
    public static ModContentPack ModContent;
    public static Settings Settings;
    public static Harmony Harmony;
    public static bool IsSimpleSidearmsActive;
    public static bool IsFistsOfFuryActive;
    public static bool IsTacticowlActive;

    private readonly Queue<(string title, Action action)> lateLoadActions = new Queue<(string title, Action action)>();
    private readonly Queue<(string title, Action action)> lateLoadActionsSync = new Queue<(string title, Action action)>();

    public static void Log(string msg)
    {
        Verse.Log.Message($"<color=#66ffb5>[MeleeAnim]</color> {msg}");
    }

    public static void Warn(string msg)
    {
        Verse.Log.Warning($"<color=#66ffb5>[MeleeAnim]</color> {msg}");
    }

    public static void Error(string msg, Exception e = null)
    {
        Verse.Log.Error($"<color=#66ffb5>[MeleeAnim]</color> {msg}");
        if (e != null)
            Verse.Log.Error(e.ToString());
    }

    private static void CheckForActiveMods()
    {
        IsSimpleSidearmsActive = ModLister.GetActiveModWithIdentifier("PeteTimesSix.SimpleSidearms") != null;
        IsFistsOfFuryActive = ModLister.GetActiveModWithIdentifier("co.uk.epicguru.fistsoffury") != null;
		IsTacticowlActive = ModLister.GetActiveModWithIdentifier("owlchemist.tacticowl") != null;
    }

    private static DateTime GetBuildDate(Assembly assembly)
    {
        var attribute = assembly.GetCustomAttribute<BuildDateAttribute>();
        return attribute?.DateTime ?? default;
    }

    private static void LogPotentialConflicts(Harmony h)
    {
        bool IsSelf(Patch p)
        {
            return p != null && p.owner == h.Id;
        }

        var str = new StringBuilder();
        var str2 = new StringBuilder();
        var str3 = new StringBuilder();
        int conflicts = 0;
        foreach (var changed in h.GetPatchedMethods())
        {
            int oldConflicts = conflicts;
            var patches = Harmony.GetPatchInfo(changed);
            str.AppendLine();
            str.AppendLine(changed.FullDescription());

            str.AppendLine("Prefixes:");
            foreach (var pre in patches.Prefixes)
            {
                str.AppendLine($"  [{pre.owner}] {pre.PatchMethod.Name}");
                if (!IsSelf(pre))
                    conflicts++;
            }

            str.AppendLine("Transpilers:");
            foreach (var trans in patches.Transpilers)
            {
                str.AppendLine($"  [{trans.owner}] {trans.PatchMethod.Name}");
                if (!IsSelf(trans))
                    conflicts++;
            }

            str.AppendLine("Postfixes:");
            foreach (var post in patches.Postfixes)
            {
                str.AppendLine($"  [{post.owner}] {post.PatchMethod.Name}");
                if (!IsSelf(post))
                    conflicts++;
            }

            str2.Append(str);
            if (oldConflicts != conflicts)
                str3.Append(str);
            str.Clear();
        }

        if (conflicts > 0)
        {
            Warn($"Potential patch conflicts ({conflicts}):");
            Warn(str3.ToString());
        }
        else
        {
            Log("No Harmony patch conflicts were detected.");
        }

        Log("Full patch list:");
        Log(str2.ToString());
    }

    private static void AddParsers()
    {
        AddParser(byte.Parse);
        AddParser(decimal.Parse);
        AddParser(short.Parse);
        AddParser(ushort.Parse);
        AddParser(uint.Parse);
        AddParser(ulong.Parse);
    }

    private static void AddParser<T>(Func<string, T> func)
    {
        if (func == null)
            return;

        // Warnings removed as of 10/03/24: Vanilla rimworld has now added the required uint32 parser, resulting in constant warning.

        // We need to do two checks because of a Rimworld bug in the HandlesType method.
        // If the T is a primitive type, HandlesType returns true, even though it is not actually handled.
        if (typeof(T).IsPrimitive && ParseHelper.CanParse(typeof(T), default(T).ToString()))
        {
            //Warn($"There is already a parser for the type '{typeof(T).FullName}'. I wonder who added it...");
            return;
        }
        if (!typeof(T).IsPrimitive && ParseHelper.HandlesType(typeof(T)))
        {
            //Warn($"There is already a parser for the type '{typeof(T).FullName}'. I wonder who added it...");
            return;
        }

        ParseHelper.Parsers<T>.Register(func);
    }

    private static void PatchVanillaBackgroundsExpanded()
    {
        if (ModLister.GetActiveModWithIdentifier("vanillaexpanded.backgrounds") == null)
            return;

        Patch_VBE_Utils_DrawBG.TryApplyPatch();
    }

    private static void PreCacheAllRetextures()
    {
        var time = RetextureUtility.PreCacheAllTextureReports(rep =>
        {
            if (rep.HasError)
            {
                Error($"Error generating texture report [{rep.Weapon?.LabelCap}]: {rep.ErrorMessage}");
            }
        }, false);
        Log($"PreCached all retexture info in {time.TotalMilliseconds:F1}ms");
    }

    private static async Task UploadMissingModData(IEnumerable<MissingModRequest> list)
    {
        var client = new ModRequestClient();
        await client.TryPostModRequests(list);
    }
    
    public Core(ModContentPack content) : base(content)
    {
        AddParsers();

        RetextureUtility.IsMeleeWeapon = def => def.IsMeleeWeapon();

        string assemblies = string.Join(",\n", from a in content.assemblies.loadedAssemblies select a.FullName);
        Log($"Hello, world!\nBuild date: {GetBuildDate(Assembly.GetExecutingAssembly()):g}\nLoaded assemblies ({content.assemblies.loadedAssemblies.Count}):\n{assemblies}");

        Harmony = new Harmony(content.PackageId);
        Harmony.PatchAll();
        ModContent = content;

        // Initialize settings.
        Settings = GetSettings<Settings>();

        // Sync:
        AddLateLoadAction(true, "Loading default shaders", () =>
        {
            AnimRenderer.DefaultCutout ??= new Material(ThingDefOf.AIPersonaCore.graphic.Shader);
            AnimRenderer.DefaultTransparent ??= new Material(ShaderTypeDefOf.Transparent.Shader);
        });

        AddLateLoadAction(false, "Checking for Simple Sidearms install...", CheckForActiveMods);
        AddLateLoadAction(false, "Checking for patch conflicts...", () => LogPotentialConflicts(Harmony));
        AddLateLoadAction(false, "Finding all lassos...", AM.Content.FindAllLassos);

        // Async:
        AddLateLoadAction(true, "Loading main content...", AM.Content.Load);
        AddLateLoadAction(true, "Initializing anim defs...", AnimDef.Init);
        AddLateLoadAction(true, "Registering def overrides...", RegisterWeaponDefOverrides);
        AddLateLoadAction(true, "Applying settings...", Settings.PostLoadDefs);
        AddLateLoadAction(true, "Matching textures with mods...", PreCacheAllRetextures);
        AddLateLoadAction(true, "Loading weapon tweak data...", LoadAllTweakData);
        AddLateLoadAction(true, "Patch VBE", PatchVanillaBackgroundsExpanded);
        AddLateLoadAction(true, "Apply final patches", Patch_Verb_MeleeAttack_ApplyMeleeDamageToTarget.PatchAll);
        AddLateLoadAction(true, "Cache gloves", HandUtility.DoInitialLoading);

        AddLateLoadEvents();
        
        // This needs to be done now, before defs are loaded.
        try
        {
            AnimDataSourceManager.ScanForDataFiles();
        }
        catch (Exception e)
        {
            Error("Failed to scan for animation data files.", e);
        }
    }

    private static void RegisterWeaponDefOverrides()
    {
        foreach (var def in DefDatabase<MeleeAnimationAdjustmentDef>.AllDefsListForReading)
        {
            def.RegisterData();
        }
    }

    private void LoadAllTweakData()
    {
        var modsAndMissingWeaponCount = new Dictionary<string, (string name, int wc)>();

        // Get all tweak data for active mods.
        foreach (var pair in TweakDataManager.LoadAllForActiveMods(false))
        {
            if (!modsAndMissingWeaponCount.TryGetValue(pair.modPackageID, out var found))
                modsAndMissingWeaponCount.Add(pair.modPackageID, (pair.modName, 0));

            modsAndMissingWeaponCount[pair.modPackageID] = (pair.modName, found.wc + 1);
        }

        Log($"Loaded tweak data for {TweakDataManager.TweakDataLoadedCount} weapons.");

        if (modsAndMissingWeaponCount.Count == 0)
            return;

        foreach (var pair in modsAndMissingWeaponCount)
        {
            Warn($"{pair.Key} '{pair.Value.name}' has {pair.Value.wc} missing weapon tweak data.");
        }

#pragma warning disable CS0162 // Unreachable code detected
        if (Settings.SendStatistics && !Settings.IsFirstTimeRunning && ENABLE_PATCH_REQUEST_API)
        {
            var modBuildTime = GetBuildDate(Assembly.GetExecutingAssembly());

            var toUpload = new List<MissingModRequest>();
            toUpload.AddRange(modsAndMissingWeaponCount.Select(p => new MissingModRequest
            {
                ModID = p.Key,
                ModName = p.Value.name,
                WeaponCount = p.Value.wc,
                ModBuildTimeUtc = modBuildTime
            }));

            Task.Run(() => UploadMissingModData(toUpload)).ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                {
                    Log("Successfully reported missing mod/weapons.");
                    return;
                }

                Warn($"Reporting missing mod/weapons failed with exception:\n{t.Exception}");
            });
        }
#pragma warning restore CS0162 // Unreachable code detected
        else
        {
            Log(Settings.IsFirstTimeRunning
                ? "Mod is running for the first time - log sending is disabled."
                : "Skipping reporting of missing mod/weapons because user opted out or the feature is disabled by the developer.");
        }

        if (!Settings.IsFirstTimeRunning)
            return;

        Settings.IsFirstTimeRunning = false;
        try
        {
            base.WriteSettings();
        }
        catch (Exception e)
        {
            Error("Failed to save settings to flag first run as over.", e);
        }
    }

    private void AddLateLoadEvents()
    {
        // Different thread loading...
        LongEventHandler.QueueLongEvent(() =>
        {
            while (lateLoadActions.TryDequeue(out var pair))
            {
                try
                {
                    LongEventHandler.SetCurrentEventText($"{ModTitle}: {pair.title}\n");
                    pair.action();
                }
                catch (Exception e)
                {
                    Error($"Exception in post-load event (async) '{pair.title}':", e);
                }
            }
        }, "AM.LoadingText", true, null);

        // Same thread loading...
        LongEventHandler.QueueLongEvent(() =>
        {
            while (lateLoadActionsSync.TryDequeue(out var pair))
            {
                try
                {
                    LongEventHandler.SetCurrentEventText($"{ModTitle}:\n{pair.title}");
                    pair.action();
                }
                catch (Exception e)
                {
                    Error($"Exception in post-load event '{pair.title}':", e);
                }
            }
        }, "AM.LoadingText", false, null);
    }

    private void AddLateLoadAction(bool synchronous, string title, Action a)
    {
        if (a == null)
            return;
        (synchronous ? lateLoadActionsSync : lateLoadActions).Enqueue((title, a));
    }

    public override string SettingsCategory() => ModTitle;

    public override void DoSettingsWindowContents(Rect inRect)
    {
        SimpleSettings.DrawWindow(Settings, inRect);
    }
}

public class HotSwapAllAttribute : Attribute { }
public class IgnoreHotSwapAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Assembly)]
internal class BuildDateAttribute : Attribute
{
    public BuildDateAttribute(string value)
    {
        DateTime = DateTime.ParseExact(value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None);
    }

    public DateTime DateTime { get; }
}