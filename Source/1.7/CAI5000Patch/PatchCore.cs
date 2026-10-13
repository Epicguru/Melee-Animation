using HarmonyLib;
using JetBrains.Annotations;
using System;
using Verse;

namespace AM.CAI5000Patch;

[HotSwapAll]
[UsedImplicitly]
public class PatchCore : Mod
{
    public PatchCore(ModContentPack content) : base(content)
    {
        try
        {
            CAI5000AnimationPatch.Init();            
            new Harmony(content.Name).PatchAll();

            Core.Log("Initialized CAI-5000 patch");
        }
        catch (Exception e)
        {
            Core.Error("Failed to initialize CAI-5000 patch:", e);
        }
    }
}