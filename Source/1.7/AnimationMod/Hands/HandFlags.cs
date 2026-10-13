using System;

namespace AM.Hands;

[Flags]
public enum HandFlags
{
    Natural = 1 << 0,
    Artificial = 1 << 2,
    Clothed = 1 << 1
}