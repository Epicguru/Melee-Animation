using System;
using UnityEngine;
using Verse;

namespace AAM
{
    public class Settings : ModSettings
    {
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

        [Header("Lasso")]
        [Label("Auto Lasso")]
        [Description("If true, your colonists will automatically use their lassos against enemies.\n\n" +
                     "This only changes the <b>default</b> setting. It can also be configured on a per-pawn basis.")]
        public bool AutoGrapple = true;

        [Header("Executions")]
        [Description("If true, your pawns will automatically execute enemy pawns in combat, without your input.\n" +
                     "This may include opportunistically using their grappling hooks if the Auto Grapple setting is enabled.\n\n" +
                     "This only changes the <b>default</b> setting. It can also be configured on a per-pawn basis.")]
        public bool AutoExecute = true;

        [Description("Allows animals to be executed.\nYou are a bad person if you enable this.")]
        public bool AnimalsCanBeExecuted = false;

        [Range(0, 20)]
        [Description("The absolute minimum melee skill required to perform any execution.\n" +
                     "Certain execution animations may require higher skill levels though.")]
        public int MinMeleeSkillToExecute = 4;

        [Range(0, 10)]
        [Percentage]
        [Description("A general modifier on the chance for execution animations to trigger. Affects all pawns.")]
        public float ExecutionChanceModifier = 1f;

        [Description("If true, executions can destroy specific vital body parts, such as the heart or head.\n" +
                     "If false, the pawn is simply killed by 'magic' (no specific part takes damage)\n" +
                     "Note: if disabled, combat log generation does not work properly for the execution, and will give a default message: \"<i>name was killed.</i>\"")]
        public bool ExecutionsCanDestroyBodyParts = true;

        [Description("Allows you to finely control who can execute who. A summary of the default settings is:\n" +
                     "Colonists will only execute hostile enemies.\n" +
                     "This means colonists will not execute prisoners or friendly pawns from trade caravans.")]
        public ExecutionMatrix ExecutionMatrix = new ExecutionMatrix();

        [Header("Gore")]
        [Label("Damage Effect")]
        [Description("Enable or disable the damage affect in animations.\n" +
                     "The damage effect is normally a small, temporary puff of blood.")]
        public bool Gore_DamageEffect = true;

        [Label("Blood (filth)")]
        [Description("Enable or disable the spawning of blood in animations.\n" +
                     "The blood is filth that must be cleaned up. Includes modded blood and mechanoid blood (oil).")]
        public bool Gore_FloorBlood = true;

        //[Label("Blood Spray")]
        //[Description("Enable or disable the blood spray effect in certain animations.\n" +
        //             "The blood spray is a visual effect and does not leave any permanent filth.\n" +
        //             "May be too over-the-top for some player's liking.")]
        //public bool Gore_Spray = true;

        [Header("Performance")]
        [Description("The interval, in ticks, between a complex pawn calculation that runs on each map.\nDon't touch this unless you know what you are doing.")]
        [Range(1, 240)]
        public int PawnProcessorTickInterval = 20;

        [Label("Max CPU Time Per Tick")]
        [Description("The maximum amount of time, in milliseconds, that the mod can spend processing pawns <b>per tick, per map</b>.\n" +
                     "Higher values can increase the responsiveness of automatic grappling and executions, but can also greatly lower performance on very populated maps.")]
        [Range(0.25f, 10f)]
        public double MaxCPUTimePerTick = 1;

        [Header("Other")]
        [Description("In order for the animation to transition seamlessly to regular gameplay, execution animations leave the corpse of the victim in non-vanilla positions and rotations.\n" +
                     "This offset can be confusing however, because the corpse no longer occupies the center of the tile.\n" +
                     "<b>Note:</b> The offset corpses are reset after a save-reload.")]
        public CorpseOffsetMode CorpseOffsetMode = CorpseOffsetMode.KeepOffset;

        public override void ExposeData()
        {
            base.ExposeData();
            SimpleSettings.AutoExpose(this);
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
        public ExecutionLine Colonists = new ExecutionLine()
        {
            Colonists = false,
            Prisoners = false,
            Friendlies = false,
            Enemies = true
        };

        /// <summary>
        /// Prisoners of the player colony. Does not include imprisoned colonists.
        /// </summary>
        public ExecutionLine Prisoners = new ExecutionLine()
        {
            Colonists = true,
            Prisoners = false,
            Friendlies = false,
            Enemies = true
        };

        /// <summary>
        /// Pawns that are friendly with the player faction, such as traders or allies.
        /// </summary>
        public ExecutionLine Friendlies = new ExecutionLine()
        {
            Colonists = true,
            Prisoners = true,
            Friendlies = true,
            Enemies = true
        };

        /// <summary>
        /// Enemies of the player faction.
        /// </summary>
        public ExecutionLine Enemies = new ExecutionLine()
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
