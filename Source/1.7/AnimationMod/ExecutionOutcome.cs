using System;

namespace AM;

[Flags]
public enum ExecutionOutcome
{
    Nothing = 0,
    Failure = 1 << 0,
    Damage = 1 << 1,
    Down = 1 << 2,
    Kill = 1 << 3,
    All = ~Nothing
}