using Meta.Numerics.Statistics.Distributions;
using System;
using System.Collections.Generic;
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

        [Label("Animated Pawns Considered Invisible")]
        [Description("When in an animation, such as an execution, pawns are considered invisible by all other pawns and turrets: " +
                     "they will not be actively targeted or shot at. This makes executions less risky.\n" +
                     "Note that pawns in animations can still take damage, such as from stray gunfire or explosions.")]
        public bool AllowInvisiblePawns = true;

        [Range(0.01f, 5f)]
        [Percentage]
        [Description("A modifier on the speed of all animations.")]
        public float GlobalAnimationSpeed = 1f;

        [Description("The minimum number of attacks in a duel. Just affects the duration of the animation, has no impact on the outcome of the duel.")]
        [Min(1)]
        public int MinDuelDuration = 4;

        [Description("The maximum number of attacks in a duel. Just affects the duration of the animation, has no impact on the outcome of the duel.")]
        [Min(1)]
        public int MaxDuelDuration = 8;

        [Description("If true, the name of pawns is drawn below them, just like in the base game.\nIf false, the name is not drawn, for a more cinematic animation.")]
        public bool DrawNamesInAnimation = true;

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

        #region Executions
        [Header("Executions")]
        [Description("If true, your pawns will automatically execute enemy pawns in combat, without your input.\n" +
                     "This may include opportunistically using their grappling hooks if the Auto Grapple setting is enabled.\n\n" +
                     "This only changes the <b>default</b> setting. It can also be configured on a per-pawn basis.")]
        public bool AutoExecute = true;

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

        [Description("Allows you to finely control who can execute who. A summary of the default settings is:\n" +
                     "Colonists will only execute hostile enemies.\n" +
                     "This means colonists will not execute prisoners or friendly pawns from trade caravans.")]
        public ExecutionMatrix ExecutionMatrix = new();
        #endregion

        #region Gore
        [Header("Gore")]
        [Label("Damage Effect")]
        [Description("Enable or disable the damage affect in animations.\n" +
                     "The damage effect is normally a small, temporary puff of blood.")]
        public bool Gore_DamageEffect = true;

        [Label("Blood (filth)")]
        [Description("Enable or disable the spawning of blood in animations.\n" +
                     "The blood is filth that must be cleaned up. Includes modded blood and mechanoid blood (oil).")]
        public bool Gore_FloorBlood = true;

        [Header("Performance")]
        [Description("The interval, in ticks, between a complex pawn calculation that runs on each map.\nDon't touch this unless you know what you are doing.")]
        [Range(1, 240)]
        public int PawnProcessorTickInterval = 20;

        [Label("Max CPU Time Per Tick")]
        [Description("The maximum amount of time, in milliseconds, that the mod can spend processing pawns <b>per tick, per map</b>.\n" +
                     "Higher values can increase the responsiveness of automatic grappling and executions, but can also greatly lower performance on very populated maps.")]
        [Range(0.25f, 10f)]
        public double MaxCPUTimePerTick = 1;
        #endregion

        #region Other
        [Header("Other")]
        [Description("In order for the animation to transition seamlessly to regular gameplay, execution animations leave the corpse of the victim in non-vanilla positions and rotations.\n" +
                     "This offset can be confusing however, because the corpse no longer occupies the center of the tile.\n" +
                     "<b>Note:</b> The offset corpses are reset after a save-reload.")]
        public CorpseOffsetMode CorpseOffsetMode = CorpseOffsetMode.KeepOffset;

        [Label("Friendly Pawn Lethality Bonus")]
        [Description("Positive values act as a lethality bonus for friendly pawns (including slaves) in execution & duel outcomes, meaning that they will be lethal more often.")]
        [Percentage]
        public float FriendlyPawnMeanNudge = 0f;

        [Label("Friendly Pawn Duel Ability Bonus")]
        [Description("Positive values act as a duel ability bonus for friendly pawns (including slaves), meaning that they will win duels more often.")]
        [Percentage]
        public float FriendlyPawnDuelMeanNudge = 0.1f;

        [Label("Lethality Normal Distribution")]
        [Description("Tl;Dr: Lower values make execution & duel outcomes less random (more dependent on Duel Ability), higher values make the outcome more random.\n\n" +
                     "Detail: Changes the normal distribution used when comparing 2 pawn's Duel Ability stats. Lower values (<0.5) remove almost all randomness, higher values (>1) make the outcome much more random.")]
        [Range(0.1f, 2f)]
        [Percentage]
        public float NormalDist = 0.5f;

        [Label("Show Warning Before Executing Friendly")]
        [Description("Prevents you from accidentally executing a friendly pawn by requiring you to hold the [Alt] key when targeting a friendly pawn for execution.")]
        public bool WarnOfFriendlyExecution = true;

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

                Widgets.Label(rect, def.LabelCap);

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

            var animations = AnimDef.AllDefs;
            foreach (var anim in animations)
            {
                if (!anim.canEditProbability)
                    continue;

                float h = DrawAnim(anim);
                area.y += h;
                height += h;
            }

            return height;
        }

        public bool IsExecutionAllowed(Pawn executioner, Pawn victim, out string explanation)
        {
            if (executioner == null || victim == null || victim == executioner)
            {
                explanation = "Invalid inputs";
                return false;
            }

            PawnType exec = executioner.GetPawnType();
            PawnType vic = victim.GetPawnType();

            bool allow = ExecutionMatrix.GetLineOf(exec).Allows(vic);
            if (allow)
                explanation = $"[{exec}] is allowed to execute [{vic}]";
            else
                explanation = $"[{exec}] {executioner.NameShortColored} is not allowed to execute [{vic}] {victim.NameShortColored}.";

            return allow;
        }
    }

    public enum CorpseOffsetMode
    {
        None,
        InterpolateToCorrect,
        KeepOffset
    }

    public class ExecutionMatrix : IExposable
    {
        /// <summary>
        /// Colonists of the player colony.
        /// Includes imprisoned colonists.
        /// </summary>
        public ExecutionLine Colonists = new()
        {
            Colonists = false,
            Prisoners = false,
            Friendlies = false,
            Enemies = true
        };

        /// <summary>
        /// Prisoners of the player colony. Does not include imprisoned colonists.
        /// </summary>
        public ExecutionLine Prisoners = new()
        {
            Colonists = true,
            Prisoners = false,
            Friendlies = false,
            Enemies = true
        };

        /// <summary>
        /// Pawns that are friendly with the player faction, such as traders or allies.
        /// </summary>
        public ExecutionLine Friendlies = new()
        {
            Colonists = true,
            Prisoners = true,
            Friendlies = true,
            Enemies = true
        };

        /// <summary>
        /// Enemies of the player faction.
        /// </summary>
        public ExecutionLine Enemies = new()
        {
            Colonists = true,
            Prisoners = true,
            Friendlies = true,
            Enemies = false
        };

        public ref bool AllowsRef(PawnType attacker, PawnType victim)
        {
            var line = GetLineOf(attacker);
            return ref line.AllowsRef(victim);
        }

        public bool Allows(PawnType attacker, PawnType victim) => GetLineOf(attacker).Allows(victim);
        
        public ExecutionLine GetLineOf(PawnType type)
            => type switch
            {
                PawnType.Colonist => Colonists,
                PawnType.Prisoner => Prisoners,
                PawnType.Friendly => Friendlies,
                PawnType.Enemy => Enemies,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"Unhandled pawn type: {type}")
            };

        public void ExposeData()
        {
            Scribe_Deep.Look(ref Colonists, "colonists");
            Scribe_Deep.Look(ref Prisoners, "prisoners");
            Scribe_Deep.Look(ref Friendlies, "friendlies");
            Scribe_Deep.Look(ref Enemies, "enemies");
        }

    }

    public class ExecutionLine : IExposable
    {
        public bool Colonists;
        public bool Prisoners;
        public bool Friendlies;
        public bool Enemies;

        public bool Allows(PawnType type)
            => type switch
            {
                PawnType.Colonist => Colonists,
                PawnType.Prisoner => Prisoners,
                PawnType.Friendly => Friendlies,
                PawnType.Enemy => Enemies,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"Unhandled pawn type: {type}")
            };

        public ref bool AllowsRef(PawnType type)
        {
            switch (type)
            {
                case PawnType.Colonist:
                    return ref Colonists;
                case PawnType.Prisoner:
                    return ref Prisoners;
                case PawnType.Friendly:
                    return ref Friendlies;
                case PawnType.Enemy:
                    return ref Enemies;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }
        }
            

        public void ExposeData()
        {
            Scribe_Values.Look(ref Colonists, "colonists");
            Scribe_Values.Look(ref Prisoners, "prisoners");
            Scribe_Values.Look(ref Friendlies, "friendlies");
            Scribe_Values.Look(ref Enemies, "enemies");
        }
    }

    public enum PawnType
    {
        Colonist,
        Prisoner,
        Friendly,
        Enemy
    }
}
