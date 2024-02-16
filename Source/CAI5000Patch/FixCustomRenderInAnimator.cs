using CombatAI;
using CombatAI.Patches;
using Verse;

namespace AM.CAI5000Patch;

/*
 * In certain specific animations, the pawn may be rendered using 
 * the regular Pawn.DrawAt method.
 * In CombatAI, when this method is called a prefix is run that expects
 * a static property called fogThings to be populated.
 * fogThings is assigned during a call to DrawDynamicThings which in turn is called
 * when a map is rendered. However, because the animators are rendered
 * during a map component update, DrawDynamicThings has not yet been invoked
 * and so the property is null.
 * This patch fixes that my ensuring that the property is set even during custom render calls.
 */
public static class FixCustomRenderInAnimator
{
    public static void PreCustomPawnRender(Pawn pawn, AnimRenderer anim)
    {
        var map = anim.Map;

        // This is CAI code:
        if (Pawn_Patch.fogThings == null || Pawn_Patch.fogThings.map != map)
        {
            Pawn_Patch.fogThings = map.GetComp_Fast<MapComponent_FogGrid>();
        }
    }

    public static void PostCustomPawnRender(Pawn pawn, AnimRenderer anim)
    {
        // CAI always sets fogThings to null after the map has rendered each frame.
        // I do not know why, probably to make sure it can be GC'd once the player exits to the main menu.
        // In any case, I will do the same even if it means having to call GetComp_Fast multiple times per frame above.
        Pawn_Patch.fogThings = null;
    }
}
