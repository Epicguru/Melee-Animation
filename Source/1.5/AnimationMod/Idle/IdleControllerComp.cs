using System;
using System.Collections.Generic;
using System.Linq;
using AM.AMSettings;
using AM.Patches;
using AM.Processing;
using AM.Tweaks;
using AM.UniqueSkills;
using JetBrains.Annotations;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace AM.Idle;

[UsedImplicitly]
public class IdleControllerComp : ThingComp
{
    [PublicAPI]
    public static readonly List<IdleControllerDrawDelegate> ShouldDrawAdditional = [];
    public static double TotalTickTimeMS;
    public static int TotalActive;
    [TweakValue("Melee Animation")]
    protected static bool IsLeftHanded; // TODO make instance, or come from pawn.
    
    [UsedImplicitly]
    [DebugAction("Melee Animation", "Log Skills", allowedGameStates = AllowedGameStates.PlayingOnMap, actionType = DebugActionType.ToolMapForPawns)]
    private static void LogSkills(Pawn pawn)
    {
        var comp = pawn.GetComp<IdleControllerComp>();
        if (comp == null)
        {
            Log.Error("Missing IdleControllerComp, probably not humanlike...");
            return;
        }

        var skills = comp.GetSkills();
        if (skills.Count == 0)
        {
            Log.Warning($"{pawn} has no skills available. Probably not colonist.");
            return;
        }

        foreach (var skill in skills)
        {
            Log.Message($" - {skill.GetType().FullName}:");
            Log.Message(skill.ToString());
        }
    }

    protected static AnimDef SelectRandomAnim(IReadOnlyList<AnimDef> anims, AnimDef preferNotThis)
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

        // Fallback in case there are not alternatives.
        return anims.RandomElementByWeight(d => d.Probability);
    }
    
    public AnimRenderer CurrentAnimation => currentAnimation;
    [PublicAPI]
    public bool IsFistsOfFuryComp { get; protected set; } = false;
    
    private UniqueSkillInstance[] skills;
    private AnimDef lastAttack;
    private AnimDef lastFlavour;
    private AnimRenderer currentAnimation;
    private float pauseAngle;
    private int pauseTicks;
    private bool isPausing;
    private float lastSpeed;
    private float lastDelta;
    private uint ticksMoving;
    private int drawTick;
    private float dodgeRotationOffset;
    private float dodgeRotationVelocity;
    private Vector2 dodgePositionOffset;
    private Vector2 dodgePositionVelocity;
    private bool wantsVanillaDrawThisFrame;

    /// <summary>
    /// Returns true to indicate that the default vanilla weapon draw should be performed.
    /// </summary>
    [MustUseReturnValue]
    public virtual bool PreDraw()
    {
        if (parent is not Pawn pawn || CurrentAnimation == null)
            return wantsVanillaDrawThisFrame;

        bool shouldBeActive = false;
        try
        {
            // If the animation is about to draw, but it was just destroyed (by the animation ending)
            // then immediately tick to get a new animation running before the frame is drawn.
            // This prevents an annoying flicker.
            if (CurrentAnimation.IsDestroyed)
            {
                shouldBeActive = ShouldBeActive(out var weapon, out wantsVanillaDrawThisFrame);
                if (shouldBeActive)
                {
                    TickActive(weapon);
                }
            }

            // Update animator position to match pawn.
            CurrentAnimation.RootTransform = MakePawnMatrix(pawn, pawn.Rotation == Rot4.North);
            drawTick = Find.TickManager.TicksAbs;
        }
        catch (Exception e)
        {
            Core.Error("PreDraw exception:", e);
        }

        if (shouldBeActive && wantsVanillaDrawThisFrame)
        {
            Core.Warn("IdleComp is active and rendering but vanilla draw was also requested. This is likely a bug that will result in double-drawing the weapon. " +
                      "Please report this to the mod author if this is logged more than once.");
        }
        return wantsVanillaDrawThisFrame;
    }

    protected bool SimpleShouldBeActiveChecks(out Pawn pawn)
    {
        pawn = parent as Pawn;
        return Core.Settings.AnimateAtIdle
               && pawn != null
               && (pawn.CurJob == null || !pawn.CurJob.def.neverShowWeapon)
               && !pawn.Dead
               && !pawn.Downed
               && pawn.Spawned;
    }

    protected bool AdditionalShouldBeActiveChecks(out bool doDefaultDraw)
    {
        bool wantsToBeActive = true;
        doDefaultDraw = false;
        
        foreach (var method in ShouldDrawAdditional)
        {
            method(this, ref wantsToBeActive, ref doDefaultDraw);
        }
        
        return wantsToBeActive;
    }

    protected virtual bool ShouldBeActive(out Thing weapon, out bool wantsVanillaDraw)
    {
        weapon = null;
        wantsVanillaDraw = false;

        // Basic checks:
        if (!SimpleShouldBeActiveChecks(out Pawn pawn))
        {
            return false;
        }

        // Vanilla checks.
        bool vanillaShouldDraw = PawnRenderUtility.CarryWeaponOpenly(pawn);

        if (!vanillaShouldDraw)
        {
            // Sometimes the pawn will not be 'openly carrying' but will still be aiming their weapon, such as when casting psycasts.
            if (pawn.stances.curStance is Stance_Busy { neverAimWeapon: false, focusTarg.IsValid: true })
                vanillaShouldDraw = true;
        }

        // Additional draw check:
        // Used for mod compatibility such as Fog of War etc.
        if (!AdditionalShouldBeActiveChecks(out wantsVanillaDraw))
        {
            return false;
        }

        // Has a valid melee weapon:
        weapon = GetMeleeWeapon();
        if (vanillaShouldDraw && weapon == null)
            vanillaShouldDraw = false;

        // Not in animation:
        if (pawn.IsInAnimation())
            return false;

        return vanillaShouldDraw;
    }

    public override void CompTick()
    {
        base.CompTick();
        
        dodgePositionOffset += dodgePositionVelocity;
        dodgePositionVelocity *= 0.9f;
        dodgePositionOffset *= 0.9f;
        dodgeRotationOffset += dodgeRotationVelocity;
        dodgeRotationVelocity *= 0.9f;
        dodgeRotationOffset *= 0.9f;

        var timer = new RefTimer();
        try
        {
            TickSkills();

            if (!ShouldBeActive(out var weapon, out wantsVanillaDrawThisFrame))
            {
                ClearAnimation();
                return;
            }

            TotalActive++;
            TickActive(weapon);
        }
        catch (Exception e)
        {
            Core.Error($"Exception processing idling animation or skills for '{parent}':", e);
        }
        TotalTickTimeMS += timer.GetElapsedMilliseconds();
    }

    private void TickSkills()
    {
        if (skills == null)
            return;

        foreach (var s in skills)
        {
            s?.Tick();
        }
    }

    protected virtual void TickActive(Thing weapon)
    {
        bool IsPlayingAnim() => CurrentAnimation is { IsDestroyed: false };

        var pawn = (Pawn)parent;

        // Avoids single-frame buggy movement animations:
        bool patherMoving = pawn.pather.MovingNow;
        bool isBusyStance = pawn.stances?.curStance is Stance_Busy { neverAimWeapon: false, focusTarg.IsValid: true };
        if (patherMoving && !isBusyStance)
            ticksMoving++;
        else
            ticksMoving = 0;
        bool isMoving = ticksMoving >= 2;

        var tweak = weapon?.TryGetTweakData();
        bool isAttacking = IsPlayingAnim() && CurrentAnimation.Def.idleType.IsAttack();

        // If attacking, it takes priority over everything else.
        if (isAttacking)
        {
            UpdateAttackAnimation();
            return;
        }

        // Reset pause ticks if not attacking.
        isPausing = false;
        pauseTicks = 0;

        if (isMoving)
        {
            // When moving and not attacking, play the movement animation.
            EnsuringMoving(pawn, tweak);
        }
        else
        {
            // Play facing or idle animation.
            EnsureFacingOrIdle(pawn, tweak);
        }

        // Randomly interrupt idle with Flavour.
        HandleStartingFlavourAnim(tweak);

        // Mirror and loop:
        if (CurrentAnimation == null)
            return;

        bool shouldLoop = CurrentAnimation.Def.idleType.IsIdle(false) || CurrentAnimation.Def.idleType.IsMove();
        bool shouldBeMirrored = pawn.Rotation == Rot4.West || (pawn.Rotation == (IsLeftHanded ? Rot4.South : Rot4.North));
        CurrentAnimation.Loop = shouldLoop;
        CurrentAnimation.MirrorHorizontal = shouldBeMirrored;

        // Normally animation root transform is set from the draw method.
        // However, when pawns are culled, the draw method is not called so the animation
        // position can get out of sync. These lines ensure that the matrix is updated if the draw hasn't been called due to culling.
        if (Find.TickManager.TicksAbs - drawTick >= 2)
            CurrentAnimation.RootTransform = MakePawnMatrix(pawn, pawn.Rotation == Rot4.North);
    }

    protected virtual void UpdateAttackAnimation()
    {
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
        else if (isPausing)
        {
            isPausing = false;
            CurrentAnimation.TimeScale = lastSpeed;
        }
    }

    protected virtual AnimDef GetMovementAnimation(ItemTweakData tweak, bool horizontal)
    {
        return horizontal ? tweak.GetMoveHorizontalAnimation() : tweak.GetMoveVerticalAnimation();
    }

    private void EnsuringMoving(Pawn pawn, ItemTweakData tweak)
    {
        bool horizontal = pawn.Rotation.IsHorizontal;

        var anim = GetMovementAnimation(tweak, horizontal);
        if (anim == null)
        {
            Core.Warn($"Missing movement animation for {tweak?.ItemDefName ?? "<null>"}, horizontal: {horizontal}");
            return;
        }

        // Start the movement animation if required.
        bool needsNew = CurrentAnimation == null || CurrentAnimation.IsDestroyed || CurrentAnimation.Def != anim;
        if (needsNew)
            StartAnim(anim);

        if (CurrentAnimation == null)
            return;

        var rot = pawn.Rotation;
        CurrentAnimation.MirrorHorizontal = rot == Rot4.North || rot == Rot4.West;
        CurrentAnimation.TimeScale = GetMoveAnimationSpeed(pawn);
    }

    /// <summary>
    /// Handles animations when the pawn is not moving.
    /// </summary>
    protected virtual void EnsureFacingOrIdle(Pawn pawn, ItemTweakData tweak)
    {
        var rot = pawn.Rotation;
        bool facingSouth = rot == Rot4.South;
        bool isBusyStance = pawn.stances.curStance is Stance_Busy { neverAimWeapon: false, focusTarg.IsValid: true };
        var anim = (isBusyStance || !facingSouth) ? rot.IsHorizontal ? tweak.GetMoveHorizontalAnimation() : tweak.GetMoveVerticalAnimation() : tweak.GetIdleAnimation();

        // Start anim if required.
        bool neededIsMovement = anim.idleType.IsMove();
        bool currentIsIdle = CurrentAnimation is {IsDestroyed: false} && CurrentAnimation.Def.idleType.IsIdle();
        bool needsNew = CurrentAnimation == null || CurrentAnimation.IsDestroyed || (CurrentAnimation.Def != anim && (!currentIsIdle || neededIsMovement));
        if (needsNew)
            StartAnim(anim);

        // Sanity check.
        if (CurrentAnimation == null)
            return;

        // Set anim speed if not idle:
        if (CurrentAnimation.Def.idleType.IsIdle())
            return;

        // Do freeze frame:
        float idleTime = CurrentAnimation.Def.idleFrame / 60f;
        CurrentAnimation.TimeScale = 0f;
        CurrentAnimation.Seek(idleTime, 0f, null);
    }

    protected virtual IReadOnlyList<AnimDef> GetFlavourAnimations(ItemTweakData tweakData)
    {
        return tweakData.GetFlavourAnimations();
    }

    private void HandleStartingFlavourAnim(ItemTweakData tweak)
    {
        // Check if disabled from settings:
        if (Core.Settings.FlavourMTB <= 0f)
            return;

        // Only perform when idling:
        if (CurrentAnimation == null || CurrentAnimation.Def.idleType != IdleType.Idle)
            return;

        // Random chance to occur each frame, using MTB from settings.
        if (!Rand.MTBEventOccurs(Core.Settings.FlavourMTB, 60f, 1f))
            return;

        // Pick random flavour.
        var anim = SelectRandomAnim(GetFlavourAnimations(tweak), lastFlavour);
        lastFlavour = anim;

        // Play said flavour.
        if (anim != null)
            StartAnim(anim);
    }

    protected void StartAnim(AnimDef def)
    {
        ClearAnimation();

        var args = new AnimationStartParameters(def, (Pawn) parent)
        {
            DoNotRegisterPawns = true,
        };

        if (!args.TryTrigger(out currentAnimation))
            Core.Error($"Failed to start idle anim '{def}'!");
    }

    private static float GetMoveAnimationSpeed(Pawn pawn)
    {
        const float REF_SPEED = 4f;
        const float MIN_COEF = 0.4f;
        const float MAX_COEF = 2.5f;
        const float EXP = 0.6f;

        bool isDiagonal = pawn.pather.nextCell.x != pawn.pather.lastCell.x && pawn.pather.nextCell.z != pawn.pather.lastCell.z;
        float dst = isDiagonal ? 1.41421f : 1f;
        float pctPerTick = pawn.pather.CostToPayThisTick() / pawn.pather.nextCellCostTotal;
        float dstPerSecond = 60f * dst * pctPerTick;
        return Mathf.Clamp(Mathf.Pow(dstPerSecond / REF_SPEED, EXP), MIN_COEF, MAX_COEF) * Core.Settings.MoveAnimSpeedCoef;
    }

    private Thing GetMeleeWeapon()
    {
        var weapon = (parent as Pawn)?.equipment?.Primary;
        if (weapon != null && weapon.def.IsMeleeWeapon() && weapon.TryGetTweakData() != null)
            return weapon;
        return null;
    }

    protected virtual IReadOnlyList<AnimDef> GetAttackAnimationsFor(Pawn pawn, Thing weapon, out bool allowPauseEver)
    {
        allowPauseEver = true;
        var tweak = weapon?.TryGetTweakData();
        if (tweak == null)
            return null;
        
        return tweak.GetAttackAnimations(pawn.Rotation);
    }
    
    public virtual void NotifyPawnDidMeleeAttack(Thing target, Verb_MeleeAttack verbUsed)
    {
        // Check valid state.
        var pawn = parent as Pawn;
        var weapon = GetMeleeWeapon();

        // If there is no weapon, and Fists of Fury is not active, then don't play an attack animation.
        if (weapon == null && !Core.IsFistsOfFuryActive)
        {
            return;
        }

        var rot = pawn.Rotation;
        var anims = GetAttackAnimationsFor(pawn, weapon, out bool allowPauseEver);
        if (anims == null || anims.Count == 0)
        {
            Core.Warn($"Failed to find any attack animation to play for {weapon}, rot: {rot.AsVector2}!");
            return;
        }

        // Attempt to get an attack animation for current weapon and stance.
        bool didHit = target != null && Patch_Verb_MeleeAttack_ApplyMeleeDamageToTarget.lastTarget == target;

        var anim = SelectRandomAnim(anims, lastAttack);

        lastAttack = anim;
        if (anim == null)
        {
            Core.Warn($"Failed to find any attack animation to play for {weapon}, rot: {rot.AsVector2}!");
            return;
        }

        // Adjust animation speed if the attack cooldown is very low.
        float speedFactor = 1f;
        if (Core.Settings.SpeedUpAttackAnims)
        {
            float cooldown = verbUsed?.verbProps.AdjustedCooldownTicks(verbUsed, pawn).TicksToSeconds() ?? 100f;
            if (cooldown < anim.Data.Duration && cooldown > 0)
            {
                speedFactor = anim.Data.Duration / cooldown;
            }
        }

        bool flipX = rot == Rot4.West;

        isPausing = false;
        pauseTicks = -1;
        if (target != null)
        {
            // Set target angle regardless of hit (it's used by some animations).
            float angle = (target.DrawPos - pawn.DrawPos).ToAngleFlatNew();
            pauseAngle = angle;

            // Only if we actually hit:
            if (didHit && allowPauseEver && Core.Settings.AttackPauseDuration != AttackPauseIntensity.Disabled)
                pauseTicks = (int)Core.Settings.AttackPauseDuration;
        }

        Patch_Verb_MeleeAttack_ApplyMeleeDamageToTarget.lastTarget = null;

        // Play animation.
        var args = new AnimationStartParameters(anim, pawn)
        {
            FlipX = flipX,
            DoNotRegisterPawns = true,
            RootTransform = MakePawnMatrix(pawn, rot == Rot4.North)
        };

        ClearAnimation();
        if (!args.TryTrigger(out currentAnimation))
        {
            Core.Error($"Failed to trigger attack animation for {pawn} ({args}). Dead: {pawn.Dead}, Downed: {pawn.Downed}, InAnim: {pawn.TryGetAnimator() != null}");
        }
        else
        {
            CurrentAnimation.TimeScale = speedFactor;
        }
    }

    private Matrix4x4 MakePawnMatrix(Pawn pawn, bool north)
    {
        var offset = new Vector3(0, north ? -0.8f : 0.1f, 0);

        Matrix4x4 animationMatrix;
        if (Core.Settings.InheritBodyPosture)
        {
            var currentDrawResults = pawn.drawer.renderer.results;
            animationMatrix = currentDrawResults.parms.matrix * Matrix4x4.Translate(offset);
        }
        else
        {
            animationMatrix = Matrix4x4.TRS(pawn.DrawPos + offset, Quaternion.identity, Vector3.one);
        }

        if (CurrentAnimation == null || !CurrentAnimation.Def.pointAtTarget)
            return animationMatrix;

        float lerp = GetPointAtTargetLerp();

        const float IDLE_ANGLE = 0;
        float point = -pauseAngle;
        if (CurrentAnimation.MirrorHorizontal)
        {
            point -= 180;
        }

        if (CurrentAnimation.Def.idleType == IdleType.AttackNorth)
        {
            point += 90;
        }
        else if (CurrentAnimation.Def.idleType == IdleType.AttackSouth)
        {
            point -= 90;
        }

        point += CurrentAnimation.Def.pointAtTargetAngleOffset;

        float a = Mathf.LerpAngle(point, IDLE_ANGLE, lerp);
        return animationMatrix * Matrix4x4.Rotate(Quaternion.Euler(0f, a, 0f));
    }

    /// <summary>
    /// 0 is point at target, 1 is idle.
    /// </summary>
    protected virtual float GetPointAtTargetLerp()
    {
        float frame = CurrentAnimation.CurrentTime * 60f;
        float lerp = Mathf.InverseLerp(CurrentAnimation.Def.returnToIdleStart, CurrentAnimation.Def.returnToIdleEnd, frame);
        return lerp;
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
        if (currentAnimation == null)
            return;
        
        if (!currentAnimation.IsDestroyed)
            currentAnimation.Destroy();

        currentAnimation = null;
    }

    public override void PostExposeData()
    {
        base.PostExposeData();

        try
        {
            if (!ShouldHaveSkills())
                return;

            if (skills == null || skills.Any(s => s == null))
            {
                PopulateSkills();
            }

            for (int i = 0; i < skills.Length; i++)
            {
                if (skills[i] == null)
                {
                    Core.Warn($"Missing (null) skill at index {i}");
                    continue;
                }

                try
                {
                    Scribe_Deep.Look(ref skills[i], skills[i].GetType().FullName);
                }
                catch (Exception e)
                {
                    Core.Error($"Exception exposing skill {skills[i]}:", e);
                }
            }
        }
        catch (Exception e2)
        {
            Core.Error("Big ouch:", e2);
        }
    }

    private bool ShouldHaveSkills() => Core.Settings.EnableUniqueSkills && parent is Pawn p && (p.IsColonist || p.IsSlaveOfColony);

    private void PopulateSkills()
    {
        try
        {
            var pawn = parent as Pawn;
            var list = DefDatabase<UniqueSkillDef>.AllDefsListForReading;
            skills = new UniqueSkillInstance[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                var instance = Activator.CreateInstance(list[i].instanceClass) as UniqueSkillInstance;
                if (instance == null)
                {
                    Core.Error($"Failed to create instance of class '{list[i].instanceClass}'. This will surely cause issues down the line.");
                    continue;
                }

                instance.Pawn = pawn;
                instance.Def = list[i];

                skills[i] = instance;
            }
        }
        catch (Exception e)
        {
            Core.Error("Exception populating skills:", e);
        }
    }

    public IReadOnlyList<UniqueSkillInstance> GetSkills()
    {
        if (skills == null)
        {
            if (ShouldHaveSkills())
            {
                // Populate skills list.
                PopulateSkills();
            }
            else
            {
                return Array.Empty<UniqueSkillInstance>();
            }
        }
        return skills;
    }
    
    /// <summary>
    /// Called when this pawn dodges a melee attack.
    /// </summary>
    public virtual void OnMeleeDodge(Pawn attackedBy)
    {
        if (!Core.Settings.EnableDodgeMotion)
            return;
        
        var directionFromAttacker = parent.DrawPos - attackedBy.DrawPos;
        var directionFromAttackerFlat = directionFromAttacker.ToFlat().normalized;
        
        // Add dodge offset.
        dodgePositionVelocity += directionFromAttackerFlat * 0.13f;
        
        // Add dodge rotation.
        bool isToRight = parent.DrawPos.x > attackedBy.DrawPos.x;
        dodgeRotationVelocity += isToRight ? 7f : -7f;
    }
    
    public virtual void AddBodyDrawOffset(ref PawnRenderer.PreRenderResults pawnDrawArgs)
    {
        if (Mathf.Abs(dodgeRotationOffset) > 0.5f || dodgePositionOffset.sqrMagnitude > 0.02f)
        {
            pawnDrawArgs.useCached = false;
            pawnDrawArgs.bodyPos += dodgePositionOffset.ToVector3();
            pawnDrawArgs.bodyAngle += dodgeRotationOffset;
        }
    }
}
