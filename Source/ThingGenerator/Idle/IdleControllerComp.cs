using JetBrains.Annotations;
using System;
using UnityEngine;
using Verse;

namespace AAM.Idle;

[UsedImplicitly]
public class IdleControllerComp : ThingComp
{
    public AnimRenderer CurrentAnimation;
    public int TicksSinceFlavour;

    public override void CompTick()
    {
        base.CompTick();

        try
        {
            // Should never happen but just in case:
            if (parent is not Pawn pawn || (pawn.CurJob != null && pawn.CurJob.def.neverShowWeapon))
            {
                ClearAnimation();
                return;
            }

            bool vanillaShouldDraw = pawn.drawer.renderer.CarryWeaponOpenly();
            if (!vanillaShouldDraw)
            {
                if (pawn.stances.curStance is Stance_Busy {neverAimWeapon: false, focusTarg.IsValid: true})
                    vanillaShouldDraw = true;
            }
            if (!vanillaShouldDraw)
            {
                ClearAnimation();
                return;
            }

            var weapon = pawn.GetFirstMeleeWeapon();
            bool shouldBeDrawing = Core.Settings.AnimateAtIdle && weapon != null && !pawn.Downed && !pawn.Dead &&
                                   pawn.Spawned;
            if (!shouldBeDrawing)
            {
                ClearAnimation();
                return;
            }

            UpdateAnimationIfRequired(pawn, weapon);
        }
        catch (Exception e)
        {
            Core.Error($"Exception processing idling animation for '{parent}':", e);
        }
    }

    private Rot4 GetCurrentAnimFacing() => CurrentAnimation.Def.idleType switch
    {
        IdleType.MoveVertical => CurrentAnimation.MirrorHorizontal ? Rot4.North : Rot4.South,
        IdleType.MoveHorizontal => CurrentAnimation.MirrorHorizontal ? Rot4.West : Rot4.East,
        _ => Rot4.South
    };

    public AnimDef GetDirectionalMovementAnimDef(Rot4 facing, Thing weapon, out bool flipX)
    {
        var cat = weapon.TryGetTweakData().GetCategory();
        switch (facing.AsInt)
        {
            case Rot4.NorthInt:
                flipX = true;
                return AnimDef.GetMoveIdleAnim(cat.size, cat.isSharp, false);

            case Rot4.SouthInt:
                flipX = false;
                return AnimDef.GetMoveIdleAnim(cat.size, cat.isSharp, false);

            case Rot4.EastInt:
                flipX = false;
                return AnimDef.GetMoveIdleAnim(cat.size, cat.isSharp, true);

            case Rot4.WestInt:
                flipX = true;
                return AnimDef.GetMoveIdleAnim(cat.size, cat.isSharp, true);
        }

        flipX = false;
        return null;
    }

    private void UpdateAnimationIfRequired(Pawn pawn, Thing weapon)
    {
        bool isMoving = pawn.pather.MovingNow;
        bool inMelee = pawn.IsInActiveMeleeCombat();
        Rot4 facing = pawn.Rotation;
        var cat = weapon.TryGetTweakData().GetCategory();

        AnimDef MakeNewAnim(out bool flipX)
        {
            // Not moving, can either be idle or melee.
            if (!isMoving)
            {
                // If facing south, use idle anim.
                if (facing == Rot4.South)
                {
                    flipX = false;
                    return AnimDef.GetMainIdleAnim(cat.size, cat.isSharp);
                }

                // Otherwise use the walk animations but freeze them.
                return GetDirectionalMovementAnimDef(facing, weapon, out flipX);
            }

            // Moving, use the directional movement anims.
            return GetDirectionalMovementAnimDef(facing, weapon, out flipX);
        }

        void StartNew()
        {
            // Clear old.
            ClearAnimation();

            // Create entirely new animation.
            var newAnim = MakeNewAnim(out bool fx);
            var startParams = new AnimationStartParameters(newAnim, pawn)
            {
                FlipX = fx,
                DoNotRegisterPawns = true,
                RootTransform = MakePawnMatrix(pawn, facing == Rot4.South)
            };

            if (!startParams.TryTrigger(out CurrentAnimation))
            {
                Core.Warn("Failed to create idle renderer animation.");
            }
            else
            {
                CurrentAnimation.Loop = true;
                //Core.Log($"[{pawn}] Started {CurrentAnimation?.ToString() ?? "null"}");
            }
        }

        if (CurrentAnimation == null || CurrentAnimation.IsDestroyed)
        {
            StartNew();
        }
        
        // Check if the current state is valid...

        // Facing in the right direction?
        var currentFacing = GetCurrentAnimFacing();
        if (currentFacing != facing)
        {
            StartNew();
        }

        // Is the animation a moving one when it should be an idle one?
        if (isMoving == CurrentAnimation.Def.idleType is IdleType.Idle or IdleType.Flavour)
        {
            if (!isMoving && facing != Rot4.South)
            {
                CurrentAnimation.Seek(0f, 0f);
                // TODO better way to pin time to 0?
            }
            else
            {
                StartNew();
            }
        }

        // TODO swing animations.

        // Random interrupt.
        if (CurrentAnimation.Def.idleType == IdleType.Idle && facing == Rot4.South && TicksSinceFlavour > GenTicks.TicksPerRealSecond * 5)
        {
            float mtbSeconds = 20;
            if (Rand.MTBEventOccurs(mtbSeconds, GenTicks.TicksPerRealSecond, 1))
            {
                ClearAnimation();
                var startParams = new AnimationStartParameters(weapon.TryGetTweakData().GetRandomFlavourAnim(), pawn)
                {
                    FlipX = false,
                    DoNotRegisterPawns = true,
                    RootTransform = MakePawnMatrix(pawn, false)
                };
                startParams.TryTrigger(out CurrentAnimation);
                TicksSinceFlavour = 0;
            }
        }
        TicksSinceFlavour++;

        CurrentAnimation.RootTransform = MakePawnMatrix(pawn, facing == Rot4.North);
    }

    private static Matrix4x4 MakePawnMatrix(Pawn pawn, bool north)
    {
        return Matrix4x4.TRS(pawn.DrawPos + new Vector3(0, north ? -0.8f : 0.1f), Quaternion.identity, Vector3.one);
    }

    public override void PostDeSpawn(Map map)
    {
        base.PostDeSpawn(map);
        ClearAnimation();
    }

    public override void PostDestroy(DestroyMode mode, Map previousMap)
    {
        base.PostDestroy(mode, previousMap);
        ClearAnimation();
    }

    public void ClearAnimation()
    {
        if (CurrentAnimation == null)
            return;

        //Core.Log($"[{parent}] Cleared {CurrentAnimation?.ToString() ?? "null"}");

        if (!CurrentAnimation.IsDestroyed)
            CurrentAnimation.Destroy();

        CurrentAnimation = null;
    }
}
