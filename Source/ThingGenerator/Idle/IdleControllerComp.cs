using JetBrains.Annotations;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements.Experimental;
using Verse;
using Patch = AAM.Patches.Patch_Verb_MeleeAttackDamage_ApplyMeleeDamageToTarget;

namespace AAM.Idle;

[UsedImplicitly]
public class IdleControllerComp : ThingComp
{
    public AnimRenderer CurrentAnimation;
    public int RareTicksSinceFlavour;

    private float pauseAngle;
    private int pauseTicks;
    private bool isPausing;
    private float lastSpeed;
    private float lastDelta;
    private AnimDef lastAttack;
    private AnimDef lastFlavour;

    public void PreDraw()
    {
        if (parent is not Pawn pawn || CurrentAnimation == null)
            return;

        if (CurrentAnimation.IsDestroyed)
        {
            TickAnimation(pawn, GetMeleeWeapon());
        }

        // Update animator position to match pawn.
        CurrentAnimation.RootTransform = MakePawnMatrix(pawn, pawn.Rotation == Rot4.North);
    }

    private bool ShouldBeActive()
    {
        // Should never happen but just in case:
        if (!Core.Settings.AnimateAtIdle || parent is not Pawn pawn || (pawn.CurJob != null && pawn.CurJob.def.neverShowWeapon))
        {
            return false;
        }

        bool vanillaShouldDraw = pawn.drawer.renderer.CarryWeaponOpenly();
        if (!vanillaShouldDraw)
        {
            if (pawn.stances.curStance is Stance_Busy { neverAimWeapon: false, focusTarg.IsValid: true })
                vanillaShouldDraw = true;
        }

        var weapon = GetMeleeWeapon();
        if (vanillaShouldDraw && weapon == null)
            vanillaShouldDraw = false;

        if (!vanillaShouldDraw)
        {
            return false;
        }

        bool shouldBeDrawing = Core.Settings.AnimateAtIdle && !pawn.Downed && !pawn.Dead &&
                               pawn.Spawned;
        if (!shouldBeDrawing)
        {
            return false;
        }

        return true;
    }

    public override void CompTick()
    {
        base.CompTick();

        try
        {
            if (!ShouldBeActive())
            {
                ClearAnimation();
                return;
            }

            TickAnimation(parent as Pawn, GetMeleeWeapon());
        }
        catch (Exception e)
        {
            Core.Error($"Exception processing idling animation for '{parent}':", e);
        }
    }

    private AnimDef Random(IReadOnlyList<AnimDef> anims, AnimDef preferNotThis)
    {
        if (anims.Count == 0)
            return null;

        if (anims.Count == 1)
            return anims[0];

        for (int i = 0; i < 20; i++)
        {
            var picked = anims.RandomElementByWeight(d => d.Probability);
            if (picked != preferNotThis)
                return picked;
        }

        return anims.RandomElementByWeight(d => d.Probability);
    }

    private Thing GetMeleeWeapon()
    {
        var weapon = (parent as Pawn)?.equipment?.Primary;
        if (weapon != null && weapon.def.IsMeleeWeapon) // TODO Check for tweak data?
            return weapon;
        return null;
    }

    private Rot4 GetCurrentAnimFacing() => CurrentAnimation.Def.idleType switch
    {
        IdleType.MoveVertical => CurrentAnimation.MirrorHorizontal ? Rot4.North : Rot4.South,
        IdleType.MoveHorizontal or IdleType.AttackHorizontal => CurrentAnimation.MirrorHorizontal ? Rot4.West : Rot4.East,
        IdleType.AttackSouth => Rot4.South,
        IdleType.AttackNorth => Rot4.North,
        _ => Rot4.South
    };

    public override void CompTickRare()
    {
        base.CompTickRare();

        if (parent is not Pawn pawn || CurrentAnimation == null)
            return;

        var weapon = GetMeleeWeapon();
        if (weapon == null)
            return;

        Rot4 facing = pawn.Rotation;

        // Random idle interrupt (flavour).
        // Done 
        if (CurrentAnimation.Def.idleType == IdleType.Idle && facing == Rot4.South && RareTicksSinceFlavour > 2)
        {
            float mtbSeconds = 20;
            if (Rand.MTBEventOccurs(mtbSeconds, GenTicks.TicksPerRealSecond, GenTicks.TickRareInterval))
            {
                ClearAnimation();
                var anim = Random(weapon.TryGetTweakData().GetFlavourAnimations(), lastFlavour);
                if (anim != null)
                {
                    lastFlavour = anim;
                    var startParams = new AnimationStartParameters(anim, pawn)
                    {
                        FlipX = false,
                        DoNotRegisterPawns = true,
                        RootTransform = MakePawnMatrix(pawn, false)
                    };
                    startParams.TryTrigger(out CurrentAnimation);
                    RareTicksSinceFlavour = 0;
                }
            }
        }
        RareTicksSinceFlavour++;
    }

    private void TickAnimation(Pawn pawn, Thing weapon)
    {
        bool isMoving = pawn.pather.MovingNow;
        bool inMelee = pawn.IsInActiveMeleeCombat();
        Rot4 facing = pawn.Rotation;

        AnimDef MakeNewAnim(out bool flipX)
        {
            var tweak = weapon.TryGetTweakData();

            // Not moving, can either be idle or melee.
            if (!isMoving)
            {
                // If facing south, use idle anim.
                if (facing == Rot4.South && !inMelee)
                {
                    flipX = false;
                    return tweak.GetIdleAnimation();
                }
            }

            // Moving, use the directional movement anims.
            if (facing.IsHorizontal)
            {
                flipX = facing == Rot4.West;
                return tweak.GetMoveHorizontalAnimation();
            }

            flipX = facing == Rot4.North;
            return tweak.GetMoveVerticalAnimation();
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
            }
        }

        if (CurrentAnimation == null || CurrentAnimation.IsDestroyed)
        {
            StartNew();
        }

        //return;
        
        // Facing in the right direction?
        var currentFacing = GetCurrentAnimFacing();
        if (currentFacing != facing)
        {
            StartNew();
        }

        // Idle -> move and mode -> idle
        if (isMoving)
        {
            // If moving but idle animation is playing, change it out.
            if (CurrentAnimation.Def.idleType is IdleType.Idle or IdleType.Flavour)
                StartNew();
        }
        else
        {
            // Not moving...
            // If moving animation is playing, change it out.
            bool moveIsPlaying = CurrentAnimation.Def.idleType is IdleType.MoveHorizontal or IdleType.MoveVertical;

            if (moveIsPlaying)
            {
                if (!inMelee)
                {
                    StartNew();
                }
                else
                {
                    // Freeze frame the move animation.
                    float idleTime = CurrentAnimation.Def.idleFrame / 60f;
                    CurrentAnimation.TimeScale = 0.0f;
                    //CurrentAnimation.Seek(idleTime, 0);
                }
            }
        }

        // Update walk animation speed based on pawn speed.
        if (CurrentAnimation.Def.idleType is IdleType.MoveHorizontal or IdleType.MoveVertical && isMoving)
        {
            float refSpeed = 4f;
            float minCoef = 0.4f;
            float maxCoef = 2.5f;
            float exp = 0.6f;

            bool isDiagonal = pawn.pather.nextCell.x != pawn.pather.lastCell.x && pawn.pather.nextCell.z != pawn.pather.lastCell.z;
            float dst = isDiagonal ? 1.41421f : 1f;
            float pctPerTick = pawn.pather.CostToPayThisTick() / pawn.pather.nextCellCostTotal;
            float dstPerSecond = 60f * dst * pctPerTick;
            float coef = Mathf.Clamp(Mathf.Pow(dstPerSecond / refSpeed, exp), minCoef, maxCoef);

            if (!isPausing && dstPerSecond > 0.05f)
                CurrentAnimation.TimeScale = coef;
        }

        // Update animation pausing.
        if (CurrentAnimation == null || !CurrentAnimation.Def.idleType.IsAttack())
        {
            pauseTicks = 0;
            isPausing = false;
        }
        if (pauseTicks > 0)
        {
            if (isPausing)
            {
                pauseTicks--;
            }
            else
            {
                // Check weapon angle.
                // Once the weapon swings by the target, do a brief pause to indicate a hit.
                var ss = CurrentAnimation.GetPart("ItemA").GetSnapshot(CurrentAnimation);
                var dir = (CurrentAnimation.RootTransform * ss.WorldMatrixPreserveFlip).MultiplyVector(Vector3.right).normalized;
                float angle = dir.ToAngleFlatNew();
                float delta = Mathf.DeltaAngle(angle, pauseAngle);
                bool shouldStartPausing = Mathf.Abs(delta) < 5f || (lastDelta != 0 && lastDelta.Polarity() != delta.Polarity() && Mathf.Abs(lastDelta) < 120);
                //Core.Log($"{angle:F0} vs {pauseAngle:F0} is Delta: {delta} ({dir.ToString("F2")})"); 
                lastDelta = delta;
                if (shouldStartPausing)
                {
                    isPausing = true;
                    lastSpeed = CurrentAnimation.TimeScale;
                    CurrentAnimation.TimeScale = 0;
                    lastDelta = 0;
                }
            }
        }
        else
        {
            if (isPausing)
            {
                isPausing = false;
                CurrentAnimation.TimeScale = lastSpeed;
            }
        }
    }

    public void NotifyPawnDidMeleeAttack(Thing target, Verb_MeleeAttack verbUsed)
    {
        // Check valid state.
        var pawn = parent as Pawn;
        var weapon = GetMeleeWeapon();
        var tweak = weapon?.TryGetTweakData();
        if (tweak == null)
            return;

        // Attempt to get an attack animation for current weapon and stance.
        var rot = pawn.Rotation;
        var anim = Random(tweak.GetAttackAnimations(rot), lastAttack);
        lastAttack = anim;
        if (anim == null)
        {
            Core.Warn($"Failed to find any attack animation to play for {weapon}!");
            return;
        }

        // Adjust animation speed if the attack cooldown is very low.
        float cooldown = verbUsed?.verbProps.AdjustedCooldownTicks(verbUsed, pawn).TicksToSeconds() ?? 100f;
        bool flipX = rot == Rot4.West;

        isPausing = false;
        pauseTicks = -1;
        if (target != null)
        {
            // Set target angle regardless of hit (it's used by some animations).
            float angle = (target.DrawPos - pawn.DrawPos).ToAngleFlatNew();
            pauseAngle = angle;

            // Only if we actually hit:
            if (Patch.lastTarget == target)
                pauseTicks = 5;
        }

        Patch.lastTarget = null;

        // Play animation.
        var args = new AnimationStartParameters(anim, pawn)
        {
            FlipX = flipX,
            DoNotRegisterPawns = true,
            RootTransform = MakePawnMatrix(pawn, rot == Rot4.North)
        };

        ClearAnimation();
        if (!args.TryTrigger(out CurrentAnimation))
        {
            Core.Error($"Failed to trigger attack animation for {pawn} ({args})");
        }
    }

    private Matrix4x4 MakePawnMatrix(Pawn pawn, bool north)
    {
        var mat = Matrix4x4.TRS(pawn.DrawPos + new Vector3(0, north ? -0.8f : 0.1f), Quaternion.identity, Vector3.one);
        if (CurrentAnimation == null || !CurrentAnimation.Def.pointAtTarget)
            return mat;

        float frame = CurrentAnimation.CurrentTime * 60f;
        float lerp = Mathf.InverseLerp(CurrentAnimation.Def.returnToIdleStart, CurrentAnimation.Def.returnToIdleEnd, frame);

        float idle = 0;
        float point = -pauseAngle;
        if (CurrentAnimation.MirrorHorizontal)
        {
            point = point - 180;
        }

        float a = Mathf.LerpAngle(point, idle, lerp);
        return mat * Matrix4x4.Rotate(Quaternion.Euler(0f, a, 0f));
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
