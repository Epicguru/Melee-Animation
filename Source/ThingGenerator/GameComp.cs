using AM.Grappling;
using AM.Idle;
using AM.Patches;
using AM.PawnData;
using AM.UI;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Verse;
using Object = UnityEngine.Object;

namespace AM;

[UsedImplicitly]
public class GameComp : GameComponent
{
    public static GameComp Current;
    public static event Action LazyTick;
    public static ulong FrameCounter;
    [TweakValue("Melee Animation")]
    [UsedImplicitly]
#pragma warning disable CS0649 // Field 'GameComp.drawTextureExtractor' is never assigned to, and will always have its default value false
    private static bool drawTextureExtractor;
#pragma warning restore CS0649 // Field 'GameComp.drawTextureExtractor' is never assigned to, and will always have its default value false

    public readonly Game Game;

    private string texPath;
    private readonly Dictionary<Pawn, PawnMeleeData> pawnMeleeData = new Dictionary<Pawn, PawnMeleeData>();
    private List<PawnMeleeData> allMeleeData = new List<PawnMeleeData>();

    public GameComp(Game game)
    {
        this.Game = game;
        Current = this;
    }

    public override void ExposeData()
    {
        base.ExposeData();

        if (Scribe.mode == LoadSaveMode.Saving)
        {
            allMeleeData.RemoveAll(d => !d.ShouldSave());
        }

        Scribe_Collections.Look(ref allMeleeData, "pawnMeleeData", LookMode.Deep);

        allMeleeData ??= new List<PawnMeleeData>();

        if (Scribe.mode != LoadSaveMode.PostLoadInit)
            return;

        pawnMeleeData.Clear();
        foreach (var data in allMeleeData)
        {
            if (!data.ShouldSave())
                continue;

            if (pawnMeleeData.ContainsKey(data.Pawn))
            {
                // Adding this check because a user reported this exact error.
                // No idea how they managed that. Save editing?
                Core.Error("Duplicate pawn data (or data with same pawn!) found when loading!");
                continue;
            }
                    
            pawnMeleeData.Add(data.Pawn, data);
        }
    }

    public PawnMeleeData GetOrCreateData(Pawn pawn)
    {
        if (pawn == null || pawn.Destroyed)
            return null;

        if (pawnMeleeData.TryGetValue(pawn, out var found))
            return found;

        var created = new PawnMeleeData
        {
            Pawn = pawn
        };
        allMeleeData.Add(created);
        pawnMeleeData.Add(pawn, created);
        return created;
    }

    public override void GameComponentTick()
    {
        if (Find.TickManager.TicksGame % 600 == 0 && LazyTick != null)
        {
            try
            {
                LazyTick();
            }
            catch (Exception e)
            {
                Core.Error("Exception during lazy tick:", e);
            }
        }

        IdleControllerComp.TotalTickTimeMS = 0;
        IdleControllerComp.TotalActive = 0;

        base.GameComponentTick();

        AnimRenderer.TickAll();
        AnimRenderer.RemoveDestroyed();
        GrabUtility.Tick();

        Patch_Corpse_DrawAt.Tick();
        Patch_PawnRenderer_LayingFacing.Tick();

        const float DT = 1 / 60f;

        foreach (var data in allMeleeData)
        {
            data.TimeSinceExecuted += DT;
            data.TimeSinceGrappled += DT;
            data.TimeSinceFriendlyDueled += DT;
        }
    }

    public override void GameComponentUpdate()
    {
        base.GameComponentUpdate();
        FrameCounter++;
    }

    public override void GameComponentOnGUI()
    {
        //GUILayout.Label($"Mem: {System.GC.GetTotalMemory(false)/(1024f*1024f):F1} MB");

        if (Prefs.DevMode && Dialog_AnimationDebugger.IsInRehearsalMode)
        {
            GUILayout.Space(100);
            GUILayout.Label("<b><color=green>IN REHEARSAL MODE!</color></b>");
        }

        if (!drawTextureExtractor)
            return;

        GUILayout.Space(100);

        texPath ??= "";
        texPath = GUILayout.TextField(texPath);
        var tex = ContentFinder<Texture2D>.Get(texPath, false);

        if (tex == null)
            return;

        GUILayout.Box(tex);

        if (!GUILayout.Button("Save"))
            return;

        RenderTexture renderTex = RenderTexture.GetTemporary(
            tex.width,
            tex.height,
            0,
            RenderTextureFormat.Default,
            RenderTextureReadWrite.Linear);

        Graphics.Blit(tex, renderTex);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTex;
        Texture2D readableText = new(tex.width, tex.height);
        readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
        readableText.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTex);

        var pngBytes = readableText.EncodeToPNG();
                
        Log.Message($"Writing {pngBytes.Length} bytes of {texPath} to Desktop ...");
        File.WriteAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), $"{tex.name ?? "grab"}.png"), pngBytes);

        Object.Destroy(readableText);
        Object.Destroy(renderTex);
    }
}