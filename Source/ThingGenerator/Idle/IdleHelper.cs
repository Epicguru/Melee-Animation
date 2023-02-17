using AAM.Tweaks;
using Verse;

namespace AAM.Idle;

public static class IdleHelper
{
    public static AnimDef GetMainIdleAnim(this ItemTweakData tweak)
    {
        var cat = tweak.GetCategory();
        return AnimDef.GetMainIdleAnim(cat.size, cat.isSharp);
    }

    public static AnimDef GetRandomFlavourAnim(this ItemTweakData tweak)
    {
        var cat = tweak.GetCategory();
        return AnimDef.GetIdleFlavours(cat.size, cat.isSharp).RandomElementByWeight(d => d.Probability);
    }

    public static AnimRenderer StartAnimOn(Pawn pawn, AnimDef anim)
    {
        if (pawn == null)
            return null;

        if (pawn.Dead || pawn.Downed || !pawn.Spawned)
            return null;

        var trs = pawn.MakeAnimationMatrix(0.5f);
        var startParams = new AnimationStartParameters(anim, pawn)
        {
            DoNotRegisterPawns = true,
            RootTransform = trs,
        };

        if (!startParams.TryTrigger(out var renderer))
            Core.Error($"Failed to start '{anim}' as an idle anim on {pawn}");
        renderer.Loop = true;

        return renderer;
    }

    [DebugAction("Advanced Melee Animation", actionType = DebugActionType.ToolMapForPawns)]
    private static void PlayIdleAnim(Pawn pawn)
    {
        DebugAnimStart(pawn, GetMainIdleAnim(pawn.GetFirstMeleeWeapon().TryGetTweakData()));
    }

    [DebugAction("Advanced Melee Animation", actionType = DebugActionType.ToolMapForPawns)]
    private static void PlayFlavourAnim(Pawn pawn)
    {
        DebugAnimStart(pawn, GetRandomFlavourAnim(pawn.GetFirstMeleeWeapon().TryGetTweakData()));
    }

    private static void DebugAnimStart(Pawn pawn, AnimDef anim)
    {
        var weapon = pawn.GetFirstMeleeWeapon();
        if (weapon == null)
            return;

        StartAnimOn(pawn, anim);
    }
}
