using AM.Idle;
using JetBrains.Annotations;
using System;
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
			//IdleControllerComp.ShouldDrawAdditional.Add(ShouldDraw);

			Core.Log("Initialized Tacticowl patch (currently not active)");
		}
		catch (Exception e)
		{
			Core.Error("Failed to initialize Tacticowl patch:", e);
		}
	}

	private static bool ShouldDraw(IdleControllerComp comp)
	{
		var pawn = comp.parent as Pawn;
		if (pawn == null)
			return true;

		// All this stuff is internal!
		// Have requested the author to change this.

		//Tacticowl.ex
		//return !pawn.HasOffHand();
		return true;
	}
}
  