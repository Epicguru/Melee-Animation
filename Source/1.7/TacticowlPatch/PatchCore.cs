using System;
using AM.Idle;
using JetBrains.Annotations;
using Tacticowl;
using Verse;

namespace AM.TacticowlPatch;

[HotSwapAll]
[UsedImplicitly]
public class PatchCore : Mod
{
	public PatchCore(ModContentPack content) : base(content)
	{
		try
		{
			IdleControllerComp.ShouldDrawAdditional.Add(ShouldDraw);
			Core.Log("Initialized Tacticowl patch");
		}
		catch (Exception e)
		{
			Core.Error("Failed to initialize Tacticowl patch:", e);
		}
	}

	private static void ShouldDraw(IdleControllerComp comp, ref bool shouldBeActive, ref bool doDefaultDraw)
	{
		// Mod settings to disable this patch.
		if (Core.Settings.DualWieldDrawSingle)
		{
			return;
		}
		
		if (comp.parent is not Pawn pawn)
		{
			return;
		}

		// If the pawn has an off-hand weapon, do not draw the idle animation.
		if (pawn.HasOffHand())
		{
			shouldBeActive = false; // Do not draw the modded idle animation(s).
			doDefaultDraw = true; // Do the vanilla draw instead, which in this case will draw the off-hand weapon as well from Tacticowl.
		}
	}
}
  