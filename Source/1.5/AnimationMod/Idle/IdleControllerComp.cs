using System;
using System.Collections.Generic;
using AM.AMSettings;
using AM.Patches;
using AM.Processing;
using AM.Tweaks;
using AM.UniqueSkills;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
using Verse;
using LudeonTK;
using System.Linq;

namespace AM.Idle;

[UsedImplicitly]
public class IdleControllerComp : ThingComp
{
    public static readonly List<Predicate<IdleControllerComp>> ShouldDrawAdditional = new List<Predicate<IdleControllerComp>>();
    public static double TotalTickTimeMS;
    public static int TotalActive;

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

    public AnimRenderer CurrentAnimation;

    private float pauseAngle;
    private int pauseTicks;
    private bool isPausing;
    private float lastSpeed;
    private float lastDelta;
    private AnimDef lastAttack;
    private AnimDef lastFlavour;
    private uint ticksMoving;
    private int drawTick;
    private UniqueSkillInstance[] skills;

    public void PreDraw()
    {
        if (parent is not Pawn pawn || CurrentAnimation == null)
            return;

        try
        {
            // If the animation is about to draw, but it was just destroyed (by the animation ending)
            // then immediately tick to get a new animation running before the frame is drawn.
            // This prevents an annoying flicker.
            if (CurrentAnimation.IsDestroyed)
            {
                if (ShouldBeActive(out var weapon))
                    TickActive(weapon);
            }

            // Update animator position to match pawn.
            CurrentAnimation.RootTransform = MakePawnMatrix(pawn, pawn.Rotation == Rot4.North);
            drawTick = Find.TickManager.TicksAbs;
        }
        catch (Exception e)
        {
            Core.Error("PreDraw exception:", e);
        }
    }

    private bool ShouldBeActive(out Thing weapon)
    {
        weapon = null;

        // Basic checks:
        if (!Core.Settings.AnimateAtIdle
            || parent is not Pawn pawn
            || (pawn.CurJob != null && pawn.CurJob.def.neverShowWeapon) 
            || pawn.Dead
            || pawn.Downed
            || !pawn.Spawned)
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

        // Dual wield check.

        // Additional draw check:
        // Used for mod compatibility such as Fog of War etc.
        foreach (var item in ShouldDrawAdditional)
        {
            if (!item(this))
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

        var timer = new RefTimer();
        try
        {
            TickSkills();

            if (!ShouldBeActive(out var weapon))
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

    private void TickActive(Thing weapon)
    {
        bool IsPlayingAnim() => CurrentAnimation is {IsDestroyed: false};

        var pawn = (Pawn)parent;

        // Avoids single-frame buggy movement animations:
        bool patherMoving = pawn.pather.MovingNow;
        bool isBusyStance = pawn.stances?.curStance is Stance_Busy { neverAimWeapon: false, focusTarg.IsValid: true };
        if (patherMoving && !isBusyStance)
            ticksMoving++;
        else
            ticksMoving = 0;
        bool isMoving = ticksMoving >= 2;

        var tweak = weapon.TryGetTweakData();
        bool isAttacking = IsPlayingAnim() && CurrentAnimation.Def.idleType.IsAttack();

        // If attacking, it takes priority over everything else.
        if (isAttacking)
        {
            DoAttackPausing();
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
        TickFlavour(tweak);

        // Mirror and loop:
        if (CurrentAnimation == null)
            return;

        bool shouldLoop = CurrentAnimation.Def.idleType.IsIdle(false) || CurrentAnimation.Def.idleType.IsMove();
        bool shouldBeMirrored = pawn.Rotation == Rot4.West || pawn.Rotation == Rot4.North;
        CurrentAnimation.Loop = shouldLoop;
        CurrentAnimation.MirrorHorizontal = shouldBeMirrored;

        // Normally animation root transform is set from the draw method.
        // However when pawns are culled, the draw method is not called so the animation
        // position can get out of sync. These lines ensure that the matrix is updated if the draw hasn't be called due to culling.
        if (Find.TickManager.TicksAbs - drawTick >= 2)
            CurrentAnimation.RootTransform = MakePawnMatrix(pawn, pawn.Rotation == Rot4.North);
    }

    private void DoAttackPausing()
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

    private void EnsuringMoving(Pawn pawn, ItemTweakData tweak)
    {
        bool horizontal = pawn.Rotation.IsHorizontal;

        var anim = horizontal ? tweak.GetMoveHorizontalAnimation() : tweak.GetMoveVerticalAnimation();
        if (anim == null)
        {
            Core.Warn($"Missing movement animation for {tweak.ItemDefName}, horizontal: {horizontal}");
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

    private void EnsureFacingOrIdle(Pawn pawn, ItemTweakData tweak)
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

    private void TickFlavour(ItemTweakData tweak)
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
        var anim = Random(tweak.GetFlavourAnimations(), lastFlavour);
        lastFlavour = anim;

        // Play said flavour.
        if (anim != null)
            StartAnim(anim);
    }

    private void StartAnim(AnimDef def)
    {
        ClearAnimation();

        var args = new AnimationStartParameters(def, (Pawn) parent)
        {
            DoNotRegisterPawns = true,
        };

        if (!args.TryTrigger(out CurrentAnimation))
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

    private static AnimDef Random(IReadOnlyList<AnimDef> anims, AnimDef preferNotThis)
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

    private Thing GetMeleeWeapon()
    {
        var weapon = (parent as Pawn)?.equipment?.Primary;
        if (weapon != null && weapon.def.IsMeleeWeapon() && weapon.TryGetTweakData() != null)
            return weapon;
        return null;
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
        bool didHit = target != null && Patch_Verb_MeleeAttack_ApplyMeleeDamageToTarget.lastTarget == target;

        // Get list of attack animations.
        var anims = tweak.GetAttackAnimations(rot);
        var anim = Random(anims, lastAttack);

        lastAttack = anim;
        if (anim == null)
        {
            Core.Warn($"Failed to find any attack animation to play for {weapon} {tweak.GetCategory()}, rot: {rot.AsVector2} !");
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
            if (didHit && Core.Settings.AttackPauseDuration != AttackPauseIntensity.Disabled)
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
        if (!args.TryTrigger(out CurrentAnimation))
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
        var mat = Matrix4x4.TRS(pawn.DrawPos + new Vector3(0, north ? -0.8f : 0.1f), Quaternion.identity, Vector3.one);
        if (CurrentAnimation == null || !CurrentAnimation.Def.pointAtTarget)
            return mat;

        float frame = CurrentAnimation.CurrentTime * 60f;
        float lerp = Mathf.InverseLerp(CurrentAnimation.Def.returnToIdleStart, CurrentAnimation.Def.returnToIdleEnd, frame);

        float idle = 0;
        float point = -pauseAngle;
        if (CurrentAnimation.MirrorHorizontal)
        {
            point -= 180;
        }

        if (CurrentAnimation.Def.idleType == IdleType.AttackNorth)
            point += 90;
        if (CurrentAnimation.Def.idleType == IdleType.AttackSouth)
            point -= 90;

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

    public override void PostExposeData()
    {
        base.PostExposeData();

        try
        {
            if (!ShouldHaveSkills())
                return;

            if (skills == null || skills.Any(s => s == null))
                PopulateSkills();

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
                    Log.Error($"Failed to create instance of class '{list[i].instanceClass}'. This will surely cause issues down the line.");
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
}
