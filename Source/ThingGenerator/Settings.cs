using Meta.Numerics.Statistics.Distributions;
using System;
using System.Collections.Generic;
using System.Linq;
using AAM.Idle;
using UnityEngine;
using Verse;

namespace AAM
{
    public class Settings : ModSettings
    {
        #region General
        [Header("General")]
        [Description("<i>(Gizmos are the buttons that appear when selecting a pawn)</i>\n\n" +
                     "If enabled, the Advanced Melee gizmo will be shown even if the pawn does not have a valid (compatible) melee weapon.")]
        public bool ShowGizmosWithoutMeleeWeapon = false;

        [Label("Always Animate Weapons")]
        [Description("If enabled, melee weapons are animated whenever they are held, such as when standing drafted or while moving in combat.\nIf disabled, animations are limited to duels, special skills and executions.\n\n" +
                     "<b>Leaving this enabled can have a large performance impact on densely populated maps.\nPlease reload your save after changing this setting.</b>")]
        [WebContent("AlwaysAnimate", true)]
        public bool AnimateAtIdle = true;

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

        [DrawMethod(nameof(DrawAnimationList))]
        [SettingOptions(drawValue: false, allowReset: false, drawHoverHighlight: false)]
        private Dictionary<string, AnimDef.SettingsData> animSettings = new Dictionary<string, AnimDef.SettingsData>();
        #endregion

        #region Lasso
        [Header("Lasso")]
        [Label("Auto Lasso")]
        [Description("If true, your colonists will automatically use their lassos against enemies.\n\n" +
                     "This only changes the <b>default</b> setting. It can also be configured on a per-pawn basis.")]
        public bool AutoGrapple = true;

        [Description("Can enemies use lassos (if they have any) to pull your colonists into melee range?")]
        public bool EnemiesCanGrapple = true;

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
        #endregion

        #region Executions & Duels
        [Header("Executions & Duels")]
        [Description("If true, your pawns will automatically execute enemy pawns in combat, without your input.\n" +
                     "This may include opportunistically using their grappling hooks if the Auto Grapple setting is enabled.\n\n" +
                     "This only changes the <b>default</b> setting. It can also be configured on a per-pawn basis.")]
        public bool AutoExecute = true;

        [Label("Automatic Execution Average Interval (Friendly)")]
        [Description("This is the average time, in seconds, at which friendly pawns will attempt to start an execution animation on the enemy they are currently fighting.\n" +
                     "For example, if this is set to 5 and your pawn is fighting in melee, an execution animation will be triggered on average after 5 seconds.\n" +
                     "This does not affect execution cooldown, which is a pawn-specific stat.\n\nLower values can greatly impact performance on populated maps.")]
        [Range(0.5f, 120)]
        [Step(1f)]
        public float ExecuteAttemptMTBSeconds = 8;

        [Label("Enemies Can Perform Executions")]
        [Description("Can enemies perform execution animations?")]
        public bool EnemiesCanExecute = true;

        
        [Label("Automatic Execution Average Interval (Enemy)")]
        [Description("This is the average time, in seconds, at which enemy pawns will attempt to start an execution animation on the target they are currently fighting.\n" +
                     "For example, if this is set to 5 and an enemy is fighting in melee, an execution animation will be triggered on average after 5 seconds.\n" +
                     "This does not affect execution cooldown, which is a pawn-specific stat.\n\nLower values can greatly impact performance on populated maps.")]
        [Range(0.5f, 120)]
        [Step(1f)]
        public float ExecuteAttemptMTBSecondsEnemy = 14;

        [Description("Allows animals to be executed.\nYou are a bad person if you enable this.")]
        public bool AnimalsCanBeExecuted = false;

        [Range(0, 10)]
        [Percentage]
        [Description("A general modifier on the lethality of execution animations. Higher values make executions more lethal. Affects all pawns.")]
        public float ExecutionLethalityModifier = 1f;

        [Label("Execution Are Non Lethal On Friendlies")]
        [Description("If enabled, execution animations on friendly pawns are always non-lethal regardless of other settings.\nPrisoners are considered friendly.\n\nUseful when trying to stop a mental break or prisoner uprising without causing a bloodbath.")]
        public bool ExecutionsOnFriendliesAreNotLethal = true;

        [Description("If true, executions can destroy specific vital body parts, such as the heart or head.\n" +
                     "If false, the pawn is simply killed by 'magic' (no specific part takes damage)\n" +
                     "Note: if disabled, combat log generation does not work properly for the execution, and will give a default message: \"<i>name was killed.</i>\"")]
        public bool ExecutionsCanDestroyBodyParts = true;

        [Description("The minimum number of attacks in a duel. Just affects the duration of the animation, has no impact on the outcome of the duel.")]
        [Min(1)]
        public int MinDuelDuration = 4;

        [Description("The maximum number of attacks in a duel. Just affects the duration of the animation, has no impact on the outcome of the duel.")]
        [Min(1)]
        public int MaxDuelDuration = 8;
        #endregion

        #region Visuals
        [Header("Visuals")]
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

        [Label("Move Animation Speed")]
        [Description("Changes the speed of the movement animations.\nHigher values increase the speed. This is just a visual change, it obviously doesn't change the pawn's movement speed.")]
        [Percentage]
        [Range(0.1f, 3f)]
        [Step(0.01f)]
        public float MoveAnimSpeedCoef = 1f;

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
        #endregion

        #region Performance
        [Header("Performance")]
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

        [Header("Other")]
        [Label("Friendly Pawn Lethality Bonus")]
        [Description("Positive values act as a lethality bonus for friendly pawns (including slaves) in execution & duel outcomes, meaning that they will be lethal more often.")]
        [Percentage]
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
        [Description("Prevents you from accidentally executing a friendly pawn by requiring you to hold the [Alt] key when targeting a friendly pawn for execution.")]
        public bool WarnOfFriendlyExecution = true;

        public bool TrailsAreDisabled => TrailColor.a <= 0 || TrailLengthScale <= 0;

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

        public float DrawAnimationList(ModSettings self, SimpleSettings.MemberWrapper member, Rect area)
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
#if V13
                    def.SData.Probability = Widgets.HorizontalSlider(checkbox, def.SData.Probability, 0f, 10f, label: $"Relative Probability: {def.SData.Probability * 100f:F0}%");
#else
                    def.SData.Probability = Widgets.HorizontalSlider_NewTemp(checkbox, def.SData.Probability, 0f, 10f, label: $"Relative Probability: {def.SData.Probability * 100f:F0}%", roundTo: 0.05f);
#endif
                }

                return rect.height;
            }

            var animations = AnimDef.AllDefs.OrderBy(d => d.type).ThenBy(d => d.idleType).ThenBy(d => d.LabelOrFallback);
            foreach (var anim in animations)
            {
                if (!anim.canEditProbability)
                    continue;
                if (anim.type == AnimType.Idle && anim.idleType is IdleType.Idle or IdleType.MoveHorizontal or IdleType.MoveVertical)
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
        Short = 4,
        Medium = 8,
        Long = 12
    }
}
