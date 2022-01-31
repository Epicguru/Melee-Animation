using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AAM
{
    public class Settings : ModSettings
    {
        public bool AllowInvisiblePawns = true;
        [Range(-1, 15)]
        public int ThreadCount = -1;
        public bool AutoGrapple = true;
        public bool AutoGrab = true;
        public bool AutoExecute = true;
        public bool AnimalsCanBeExecuted = false;
        public CorpseOffsetMode CorpseOffsetMode = CorpseOffsetMode.KeepOffset;
        [Range(0, 20)]
        public int MinMeleeSkillToExecute = 4;
        [Range(0, 10)]
        public float ExecutionChanceModifier = 1f;
        public ExecutionMatrix ExecutionMatrix = new ExecutionMatrix();

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

    public class TestSettings : ModSettings
    {
        public string Message = "Hello";
        public IntVec2 IntVec2;
        public Vector3 Vec3 = new Vector3(1, 2, 3);
        public ThingDef MyThingDef;

        // Integer types.
        public byte Byte;
        public sbyte SByte;
        public short Short;
        public ushort UShort;
        public int Int;
        public uint UInt;
        public long Long;
        public ulong ULong;

        // Floating point types.
        public float Float;
        public double Double;
        public decimal Decimal;

        public ExecutionLine Line = new ExecutionLine()
        {
            Colonists = true,
            Prisoners = true
        };
        public List<long> Longs = new List<long>()
        {
            -500, 0, 1024
        };
        public List<string> Strings = new List<string>()
        {
            "First",
            "Second",
            "Third"
        };
        public List<ExecutionLine> Lines = new List<ExecutionLine>()
        {
            new ExecutionLine(){Friendlies = true},
            null
        };

        public TestSettings()
        {
            SimpleSettings.Init(this);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            SimpleSettings.AutoExpose(this);
        }
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
