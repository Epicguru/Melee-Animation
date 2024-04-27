using UnityEngine;

namespace AM.Hands;

public readonly struct HandInfo
{
    public required Color Color { get; init; }
    public required HandFlags Flags { get; init; }
}