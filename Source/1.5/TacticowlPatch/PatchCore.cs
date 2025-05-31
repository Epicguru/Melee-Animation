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

	private static bool ShouldDraw(IdleControllerComp comp)
	{
		if (comp.parent is not Pawn pawn)
			return true;

		// If the pawn has an off-hand weapon, do not draw the idle animation.
		return !pawn.HasOffHand();
	}
}
  