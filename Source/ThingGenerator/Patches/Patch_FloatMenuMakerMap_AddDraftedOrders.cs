using AAM.Gizmos;
using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AAM.Patches;

[HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.AddDraftedOrders))]
public class Patch_FloatMenuMakerMap_AddDraftedOrders
{
    private static readonly TargetingParameters targetingArgs = new TargetingParameters()
    {
        canTargetAnimals = true,
        canTargetMechs = true,
        canTargetBloodfeeders = true,
        canTargetCorpses = false,
        canTargetItems = false,
        canTargetLocations = false,
        canTargetPawns = true,
        canTargetHumans = true,
        canTargetPlants = false,
        canTargetBuildings = false,
        canTargetSelf = false,
        canTargetFires = false,
    };

    static void Postfix(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts)
    {
        if (!pawn.IsColonistPlayerControlled)
            return;

        var weapon = pawn.GetFirstMeleeWeapon();
        var lasso = pawn.TryGetLasso();

        void Add(FloatMenuOption opt)
        {
            if (opt != null)
                opts.Add(opt);
        }

        foreach (var t in GenUI.TargetsAt(clickPos, targetingArgs, true))
        {
            var target = t.Pawn ?? t.Thing as Pawn;
            if (target.Dead)
                continue;

            Add(GenLassoOption(pawn, target, lasso));
            Add(GenExecuteOption(pawn, target, lasso, weapon));
        }
    }

    private static FloatMenuOption GenLassoOption(Pawn pawn, Pawn target, Thing lasso)
    {
        if (lasso == null)
            return null;

        bool HasLos(IntVec3 c)
        {
            return GenSight.LineOfSight(pawn.Position, c, pawn.Map);
        }

        void OnClicked()
        {
            AnimationGizmo.OnSelectedLassoTarget(pawn, new LocalTargetInfo(target), true);
        }

        void OnMouseOver(Rect area)
        {
            pawn.GetAnimManager().AddPostDraw(() =>
            {
                float radius = pawn.GetStatValue(AAM_DefOf.AAM_GrappleRadius);
                GenDraw.DrawRadiusRing(pawn.Position, radius, Color.yellow, HasLos);
            });
        }

        var op = new FloatMenuOption($"Lasso {target.Label}", OnClicked, mouseoverGuiAction: OnMouseOver, priority: MenuOptionPriority.VeryLow);

        string errorMsg = AnimationGizmo.OnSelectedLassoTarget(pawn, new LocalTargetInfo(target), false);
        if (errorMsg != null)
        {
            op.Disabled = true;
            op.Label = errorMsg;
        }

        return op;
    }

    private static FloatMenuOption GenExecuteOption(Pawn pawn, Pawn target, Thing lasso, Thing weapon)
    {
        if (weapon == null)
            return null;

        bool dontUseLasso = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || lasso == null;
        if (lasso != null && !dontUseLasso)
        {
            // Automatically force to walk if lasso will not work (such as out of range).
            string lassoErr = AnimationGizmo.OnSelectedLassoTarget(pawn, target, false);
            if (lassoErr != null)
            {
                Core.Log($"Allowing pawn to walk to execution because: {lassoErr}");
                dontUseLasso = true;
            }
        }

        bool HasLos(IntVec3 c)
        {
            return GenSight.LineOfSight(pawn.Position, c, pawn.Map);
        }

        void OnClicked()
        {
            Core.Log($"Clicked with dont use: {dontUseLasso}");
            AnimationGizmo.OnSelectedExecutionTarget(dontUseLasso, pawn, new LocalTargetInfo(target), true);
        }

        void OnMouseOver(Rect area)
        {
            if (lasso == null)
                return;

            pawn.GetAnimManager().AddPostDraw(() =>
            {
                float radius = pawn.GetStatValue(AAM_DefOf.AAM_GrappleRadius);
                GenDraw.DrawRadiusRing(pawn.Position, radius, Color.yellow, HasLos);
            });
        }

        var op = new FloatMenuOption($"Execute {target.Label} [Lethality: {pawn.GetStatValue(AAM_DefOf.AAM_Lethality)*100f:F0}%]", OnClicked, mouseoverGuiAction: OnMouseOver, priority: MenuOptionPriority.VeryLow);
        if (dontUseLasso)
            op.Label += " (Walking)";

        string errorMsg = AnimationGizmo.OnSelectedExecutionTarget(dontUseLasso, pawn, new LocalTargetInfo(target), false);
        if (errorMsg != null)
        {
            op.Disabled = true;
            op.Label = errorMsg;
        }

        return op;
    }
}