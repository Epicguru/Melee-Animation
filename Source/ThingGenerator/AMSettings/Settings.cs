using Meta.Numerics.Statistics.Distributions;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
#if !V14
using LudeonTK;
#endif

namespace AM.AMSettings;

public class Settings : SimpleSettingsBase
{
    [TweakValue("Melee Animation")]
    [NonSerialized]
    private static bool canModIdleAnims = false;

    #region General
    [Header("General")]
    [Label("Always Animate Weapons")]
    [Description("If enabled, melee weapons are animated whenever they are held, such as when standing drafted or while moving in combat.\nIf disabled, animations are limited to duels, special skills and executions.\n\n" +
                 "<b>Leaving this enabled can have a large performance impact on densely populated maps.\nPlease reload your save after changing this setting.</b>")]
    [WebContent("AlwaysAnimate", true)]
    public bool AnimateAtIdle = true;

    [Label("Enable Unique Skills")]
    [Description("Enables or disables the Unique Skill system.\n" +
                 "Unique Skills are powerful attacks or abilities that are unlocked under certain conditions.\n" +
                 "Only your colonists can use these skills and they must be activated manually. See the Steam workshop page for more info.")]
    [WebContent("Skills", false)]
    public bool EnableUniqueSkills = true;

    [Label("Animated Pawns Considered Invisible")]
    [Description("When in an animation, such as an execution, pawns are considered invisible by all other pawns and turrets: " +
                 "they will not be actively targeted or shot at. This makes executions less risky.\n" +
                 "Note that pawns in animations can still take damage, such as from stray gunfire or explosions.")]
    [WebContent("Invisible", true)]
    public bool AllowInvisiblePawns = true;

    [Range(0.01f, 5f)]
    [Percentage]
    [Description("A modifier on the speed of all animations.\nHigher is faster.")]
    public float GlobalAnimationSpeed = 1f;

    [Label("Individual Animation Settings")]
    [DrawMethod(nameof(DrawAnimationList))]
    [SettingOptions(drawValue: false, allowReset: false, drawHoverHighlight: false, ignoreEqualityForPresets: true)]
    private Dictionary<string, AnimDef.SettingsData> animSettings = new Dictionary<string, AnimDef.SettingsData>();
    #endregion

    #region Lasso
    [Header("Lasso")]
    [Label("Auto Lasso")]
    [Description("If true, your colonists will automatically use their lassos against enemies.\n\n" +
                 "This only changes the <b>default</b> setting. It can also be configured on a per-pawn basis.")]
    public bool AutoGrapple = true;

    [Label("Automatic Lasso Average Interval (Friendly)")]
    [Description("This is the average time, in seconds, at which friendly pawns will attempt to use their lasso to pull an enemy into melee range.\n" +
                 "If their execution is off cooldown and Auto Execute is enabled, they will immediately execute the lassoed target too.")]
    [Range(1, 240)]
    [Step(1f)]
    public float GrappleAttemptMTBSeconds = 10;

    [Label("Lasso Commonality")]
    [Description("This is the % chance for any <b>melee fighter</b> pawn to spawn with a lasso equipped.\nSet this to 0% to disable natural lasso generation on pawns.")]
    [Percentage]
    public float LassoSpawnChance = 0.2f;

    [Label("Enemies Can Lasso")]
    [Description("Can enemies use lassos (if they have any) to pull your colonists into melee range?")]
    public bool EnemiesCanGrapple = true;

    [Label("Automatic Lasso Average Interval (Enemy)")]
    [Description("This is the average time, in seconds, at which enemy pawns will attempt to use their lasso to pull a target into melee range.\n" +
                 "If their execution is off cooldown and 'Enemies Can Perform Executions' is enabled, they will immediately execute the lassoed target too.")]
    [VisibleIf(nameof(EnemiesCanGrapple))]
    [Range(1, 240)]
    [Step(1f)]
    public float GrappleAttemptMTBSecondsEnemy = 40;

    [Label("Minimum Melee Skill")]
    [Description("The minumum melee skill required to use a lasso.\nAffects all pawns.")]
    [Range(0, 20)]
    public int MinMeleeSkillToLasso = 4;

    [Label("Minimum Manipulation")]
    [Description("The minimum Manipulation stat required to use a lasso.\nAffects all pawns.")]
    [Percentage]
    public float MinManipulationToLasso = 0.5f;

    [Label("Max Pawn Mass")]
    [Description("The maximum mass that a pawn can have in order for it to be lassoed. The mass is measured in kilograms. You can check the mass of pawns in their Stat sheet.\nSet to zero to disable the mass limit.")]
    [Min(0)]
    public float MaxLassoMass = 0f;

    [Label("Max Pawn Size")]
    [Description("The maximum 'body size' that a pawn can have in order for it to be lassoed.\nFor reference, here are some example body sizes:\n" +
                 "• Chicken: 0.3\n" +
                 "• Human: 1\n" +
                 "• Boomalope: 2\n" +
                 "• Warg: 3\n" +
                 "• Thrumbo: 4\n" +
                 "\n\nSet to zero to disable the body size limit.")]
    [Min(0)]
    public float MaxLassoBodySize = 3;

    [Label("Lasso Travel Speed")]
    [Description("Adjusts the speed <b>(not cooldown)</b> of lassos, making it faster or slower to ensnare and pull in an enemy.\nHigher values make the lasso faster.\nAffects all pawns.")]
    [Range(0.1f, 5f)]
    [Percentage]
    public float GrappleSpeed = 1f;

    [Label("Max Building Fill For Lasso Drag")]
    [Description("The maximum 'fill' percentage of a building that a pawn can be dragged through by a lasso.\n" +
                 "Lower values mean that pawns can <b>not</b> be dragged through/over partial cover such as sand bags or embrasures. " +
                 "A value of 100% means that the lasso can pull pawns though/over anything except completely solid walls and buildings.")]
    [Percentage]
    public float MaxFillPctForLasso = 0.2f;
    #endregion

    #region Executions & Duels
    [Header("Executions & Duels", order = 0)]

    [Description("Entirely enables or disables the execution system. Disabling this means that no pawns will every be able to do executions, " +
                 "and the option is removed from the UI.")]
    public bool EnableExecutions = true;

    [Description("If true, your pawns will automatically execute enemy pawns in combat, without your input.\n" +
                 "This may include opportunistically using their grappling hooks if the Auto Grapple setting is enabled.\n\n" +
                 "This only changes the <b>default</b> setting. It can also be configured on a per-pawn basis.")]
    [VisibleIf(nameof(EnableExecutions))]
    public bool AutoExecute = true;

    [Label("Enemies Can Perform Executions")]
    [Description("Can enemies perform execution animations?")]
    [VisibleIf(nameof(EnableExecutions))]
    public bool EnemiesCanExecute = true;

    [Label("Execution Failure Chance (Low Skill)")]
    [Description("The chance that an execution attempt will result in failure, stunning the attacking pawn and leaving them vulnerable for a short time.\n" +
        "This is the chance when a pawn has 0 melee skill.\n" +
        "Affects all pawns.")]
    [Percentage]
    [VisibleIf(nameof(EnableExecutions))]
    public float ChanceToFailMinSkill = 0.15f;

    [Label("Execution Failure Chance (High Skill)")]
    [Description("The chance that an execution attempt will result in failure, stunning the attacking pawn and leaving them vulnerable for a short time.\n" +
                 "This is the chance when a pawn has 20 melee skill.\n" +
                 "Affects all pawns.")]
    [Percentage]
    [VisibleIf(nameof(EnableExecutions))]
    public float ChanceToFailMaxSkill = 0.03f;

    [Label("Automatic Execution Average Interval (Friendly)")]
    [Description("This is the average time, in seconds, at which friendly pawns will attempt to start an execution animation on the enemy they are currently fighting.\n" +
                 "For example, if this is set to 5 and your pawn is fighting in melee, an execution animation will be triggered on average after 5 seconds.\n" +
                 "This does not affect execution cooldown, which is a pawn-specific stat.\n\nLower values can greatly impact performance on populated maps.")]
    [Range(0.5f, 240)]
    [Step(1f)]
    [VisibleIf(nameof(EnableExecutions))]
    public float ExecuteAttemptMTBSeconds = 10;

    [Label("Automatic Execution Average Interval (Enemy)")]
    [Description("This is the average time, in seconds, at which enemy pawns will attempt to start an execution animation on the target they are currently fighting.\n" +
                 "For example, if this is set to 5 and an enemy is fighting in melee, an execution animation will be triggered on average after 5 seconds.\n" +
                 "This does not affect execution cooldown, which is a pawn-specific stat.\n\nLower values can greatly impact performance on populated maps.")]
    [Range(0.5f, 240)]
    [Step(1f)]
    [VisibleIf(nameof(EnableExecutions))]
    public float ExecuteAttemptMTBSecondsEnemy = 30;

    [Description("Allows animals to be executed.\nYou are a bad person if you enable this.")]
    [VisibleIf(nameof(EnableExecutions))]
    public bool AnimalsCanBeExecuted = false;

    [Range(0, 10)]
    [Percentage]
    [Description("A general modifier on the lethality of execution animations. Higher values make executions more lethal. Affects all pawns.")]
    [VisibleIf(nameof(EnableExecutions))]
    public float ExecutionLethalityModifier = 1f;

    [Label("Executions Are Non Lethal On Friendlies")]
    [Description("If enabled, execution animations on friendly pawns are always non-lethal regardless of other settings.\nPrisoners and slaves are considered friendly.\n\nUseful when trying to stop a mental break or prisoner uprising without causing a bloodbath.")]
    [VisibleIf(nameof(EnableExecutions))]
    public bool ExecutionsOnFriendliesAreNotLethal = true;

    [Label("Execution Armor Strength")]
    [Description("A multiplier on the effectiveness of armor when calculating execution animation outcome.\nLower values decrease the effect of armor on the outcome, higher values increase the strength of armor.\nSet to 0% to make armor be ignored.")]
    [Percentage]
    [Range(0, 5)]
    [VisibleIf(nameof(EnableExecutions))]
    public float ExecutionArmorCoefficient = 1f;

    [Description("If true, executions can destroy specific vital body parts, such as the heart or head.\n" +
                 "If false, the pawn is simply killed by 'magic' (no specific part takes damage)\n" +
                 "Note: if disabled, combat log generation does not work properly for the execution, and will give a default message: \"<i>name was killed.</i>\"")]
    [VisibleIf(nameof(EnableExecutions))]
    public bool ExecutionsCanDestroyBodyParts = true;

    [Label("Amount Skill Affects Execution Cooldown")]
    [Description("The amount that melee skill affects execution cooldown time.\n" +
                 "Higher melee skill means lower cooldown, changing this value increases or decreases the effect of melee skill.\n" +
                 "Set to 0% to disable melee skill as a factor.\n\n" +
                 "Note: only affects friendly pawns.")]
    [Range(0, 2)]
    [Percentage]
    [VisibleIf(nameof(EnableExecutions))]
    public float MeleeSkillExecCooldownFactor = 1f;

    [Label("Execution Cooldown Factor (Friendly)")]
    [Description("This adjust the execution cooldown time for friendly pawns. Lower values decrease the cooldown. You can see the final cooldown time in the pawn's stats.")]
    [Percentage]
    [Range(0.01f, 5f)]
    [Step(0.01f)]
    [VisibleIf(nameof(EnableExecutions))]
    public float FriendlyExecCooldownFactor = 1f;

    [Label("Execution Cooldown Factor (Enemy)")]
    [Description("This adjust the execution cooldown time for hostile pawns. Lower values decrease the cooldown. You can see the final cooldown time in the pawn's stats.")]
    [Percentage]
    [Range(0.01f, 5f)]
    [Step(0.01f)]
    [VisibleIf(nameof(EnableExecutions))]
    public float EnemyExecCooldownFactor = 1f;

    // Duel visuals:

    [Description("The minimum number of attacks in a duel. Just affects the duration of the animation, has no impact on the outcome of the duel.")]
    [Min(1)]
    public int MinDuelDuration = 4;

    [Description("The maximum number of attacks in a duel. Just affects the duration of the animation, has no impact on the outcome of the duel.")]
    [Min(1)]
    public int MaxDuelDuration = 8;

    [Description("The cooldown time, in seconds, after a friendly duel where a friendly duel cannot be started again.")]
    [Min(0)]
    public float FriendlyDuelCooldown = 60 * 5;
    #endregion

    #region Visuals
    [Header("Visuals", order = 1)]
    [Description("Should pawn hands be displayed holding melee weapons?")]
    [WebContent("HandsEnabled", false)]
    public bool ShowHands = true;

    [Label("Damage Effect")]
    [Description("Enable or disable the damage affect in animations.\n" +
                 "The damage effect is normally a small, temporary puff of blood.")]
    public bool Gore_DamageEffect = true;

    [Label("Blood (filth)")]
    [Description("Enable or disable the spawning of blood in animations.\n" +
                 "The blood is filth that must be cleaned up. Includes modded blood and mechanoid blood (oil).")]
    public bool Gore_FloorBlood = true;

    [Description("In order for the animation to transition seamlessly to regular gameplay, execution animations leave the corpse of the victim in non-vanilla positions and rotations.\n" +
                 "This offset can be confusing however, because the corpse no longer occupies the center of the tile.\n" +
                 "<b>Note:</b> The offset corpses are reset after a save-reload.")]
    [WebContent("OffsetMode", false)]
    public CorpseOffsetMode CorpseOffsetMode = CorpseOffsetMode.KeepOffset;

    // TODO finish body size scaling support:
    //public bool BodyScaleUp = true;
    //public bool BodyScaleDown = true;

    [Label("Move Animation Speed")]
    [Description("Changes the speed of the movement animations.\nHigher values increase the speed. This is just a visual change, it obviously doesn't change the pawn's movement speed.")]
    [Percentage]
    [Range(0.1f, 3f)]
    [Step(0.01f)]
    public float MoveAnimSpeedCoef = 1f;

    [Label("Increase Animation Speed When Attacks Are Fast")]
    [Description("If enabled, the speed of melee attack animations will be automatically increased to match the pawn's melee speed.\n" +
                 "If false, the attack animation speed will never change however the animation may be interrupted by the next attack animation starting before the last one ended.\n" +
                 "This is a purely visual change and does not affect combat.")]
    public bool SpeedUpAttackAnims = true;

    [Label("Idle Animation Average Interval")]
    [Description("Pawns standing with their weapon out (such as when drafted) will sometimes play an animation where they swing their weapon about, flourish it etc.\n" +
                 "This option controls the average time, in seconds, between the occurrence of this animation.\nSet to 0 to disable the animations entirely.")]
    [Range(0, 60)]
    [Step(0.5f)]
    public float FlavourMTB = 10f;

    [Description("When doing a regular melee attack, the animation will very briefly pause at the point when the weapon intersects the target.\n" +
                 "This lets you know whether an attack connected, as well as giving the hit a bit more visual <i>oomph</i>.\n" +
                 "This setting changes the duration of that pause, or disables it entirely. This is a purely visual change and does not affect combat.")]
    public AttackPauseIntensity AttackPauseDuration = AttackPauseIntensity.Medium;

    [Label("Weapon Trail Color")]
    [Description("The base color of weapon trails. If you set the alpha to 0, trails will be disabled.")]
    [WebContent("SweepColor", false)]
    public Color TrailColor = Color.white;

    [Label("Weapon Trail Length")]
    [Description("A multiplier on the length of weapon trails. If 0%, trails are disabled.")]
    [Percentage]
    [Range(0f, 2f)]
    [WebContent("SweepLength", false)]
    public float TrailLengthScale = 1f;

    [Description("If true, the name of pawns is drawn below them, just like in the base game.\nIf false, the name is not drawn, for a more cinematic animation.")]
    [WebContent("ShowNames", false)]
    public bool DrawNamesInAnimation = true;

    [Label("Show Execution Outcome Text")]
    [Description("Enables or disables the text popup that shows the outcome of an execution (i.e. injure, down or kill) when an execution animation plays.")]
    public bool ShowExecutionMotes = true;
    #endregion

    #region Performance
    [Header("Performance", order = 2)]
    [Description("The maximum number of CPU threads to use when processing pawns for automatic executions & lasso usage.\n" +
                 "If set to 0, the thread count is automatically determined based on your CPU, and if set to 1 then multi-threaded processing is disabled.\n" +
                 "Set to 1 if you experience error spam caused by a mod conflict, although it will decrease performance considerably.")]
    [Range(0, 64)]
    public int MaxProcessingThreads = 0;

    [Description("When enabled, multiple CPU threads are used to calculate complex matrix transformation needed for animations.\n" +
                 "The number of threads is given by the Max Processing Threads setting.")]
    public bool MultithreadedMatrixCalculations = true;

    [Description("When enabled, offscreen animations are not drawn to save time and increase FPS.\n" +
                 "This option is only here in case there are unexpected bugs related to this culling.")]
    public bool OffscreenCulling = true;

    [Description("The number of ticks between scanning all pawns on the map for automatic execution/duel/lasso opportunities.\n" +
                 "Higher values can increase FPS at the cost of less responsiveness.")]
    [Range(1, 60)]
    public int ScanTickInterval = 5;
    #endregion

    #region Other

    [Header("Other", order = 3)]
    [Label("Friendly Pawn Lethality Bonus")]
    [Description("Positive values act as a lethality bonus for friendly pawns (including slaves) in execution & duel outcomes, meaning that they will be lethal more often.")]
    [Percentage]
    [Range(-1, 1)]
    public float FriendlyPawnLethalityBonus = 0f;

    [Label("Friendly Pawn Duel Ability Bonus")]
    [Description("Positive values act as a duel ability bonus for friendly pawns (including slaves), meaning that they will win duels more often.")]
    [Percentage]
    public float FriendlyPawnDuelBonus = 0.1f;

    [Label("Duel Normal Distribution")]
    [Description("Higher values make duel outcomes less dependent on duel ability and more on randomness, lower values make the outcome more dependent on duel ability and less on randomness.\n\nTechnical: This is the standard deviation used in the duel outcome normal distribution curve.")]
    [Range(0.1f, 2f)]
    [Percentage]
    public float NormalDist = 0.5f;

    [Label("Show Warning Before Executing Friendly")]
    [Description("Prevents you from accidentally executing a friendly pawn by requiring you to hold the [Shift] key when selecting a friendly pawn for execution.")]
    public bool WarnOfFriendlyExecution = true;

    [Label("Send Anonymous Patch Statistics")]
    [Description("When a mod is missing a patch (that allows the melee weapons to do animations), the ID of said mod is anonymously logged to " +
                 "let this mod's author know that a patch is needed. The <b>only</b> information logged is: mod ID, mod name, weapon count.\n" +
                 "You can opt out of this functionality by disabling this option.\nNote: logging does not occur the first time you run the game with this mod.")]
    public bool SendStatistics = true;

    [NonSerialized]
    public bool IsFirstTimeRunning = true;
    #endregion

    private static NormalDistribution normal;

    public NormalDistribution GetNormalDistribution()
    {
        if (normal == null || Math.Abs(normal.StandardDeviation - NormalDist) > 0.001f)
            normal = new NormalDistribution(0.0, NormalDist);

        return normal;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        SimpleSettings.AutoExpose(this);
        Scribe_Values.Look(ref IsFirstTimeRunning, nameof(IsFirstTimeRunning), true);
    }

    public void PostLoadDefs()
    {
        animSettings ??= new Dictionary<string, AnimDef.SettingsData>();

        foreach (var def in AnimDef.AllDefs)
        {
            if (def.SData != null)
                Core.Error("EXPECTED NULL DATA!");

            if (def.canEditProbability && animSettings.TryGetValue(def.defName, out var found))
            {
                def.SData = found;
            }
            else
            {
                def.SetDefaultSData();
                animSettings[def.defName] = def.SData;
            }
        }
    }

    public float DrawAnimationList(SimpleSettingsBase self, SimpleSettings.MemberWrapper member, Rect area)
    {
        float height = SimpleSettings.DrawFieldHeader(self, member, area);
        area.y += height;

        float DrawAnim(AnimDef def)
        {
            var rect = area;
            rect.height = 32;

            Widgets.Label(rect, def.LabelOrFallback);

            var checkbox = rect;
            checkbox.x += 230;
            checkbox.width = 100;
            Widgets.CheckboxLabeled(checkbox, "Enabled: ", ref def.SData.Enabled, placeCheckboxNearText: true);

            if (def.SData.Enabled)
            {
                checkbox.x += 110;
                checkbox.width = 200;

#if V14
                def.SData.Probability = Widgets.HorizontalSlider_NewTemp(checkbox, def.SData.Probability, 0f, 10f, label: $"Relative Probability: {def.SData.Probability * 100f:F0}%", roundTo: 0.05f);
#else
                def.SData.Probability = Widgets.HorizontalSlider(checkbox, def.SData.Probability, 0f, 10f, label: $"Relative Probability: {def.SData.Probability * 100f:F0}%", roundTo: 0.05f);
#endif
            }

            return rect.height;
        }

        var animations = AnimDef.AllDefs.OrderBy(d => d.type).ThenBy(d => d.idleType).ThenBy(d => d.LabelOrFallback);
        foreach (var anim in animations)
        {
            if (!anim.canEditProbability)
                continue;
            if (anim.type == AnimType.Idle && !canModIdleAnims)
                continue;

            float h = DrawAnim(anim);
            area.y += h;
            height += h;
        }

        return height;
    }
}

public enum CorpseOffsetMode
{
    None,
    InterpolateToCorrect,
    KeepOffset
}

public enum AttackPauseIntensity
{
    Disabled = 0,
    Short = 3,
    Medium = 5,
    Long = 12
}