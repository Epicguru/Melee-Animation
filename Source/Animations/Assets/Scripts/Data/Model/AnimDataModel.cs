using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents the intermediate (json) data of an animation,
/// which is then turned into <see cref="AnimData"/>.
/// </summary>
public class AnimDataModel
{
    /// <summary>
    /// The datetime when this animation was exported.
    /// </summary>
    public DateTime ExportTimeUTC { get; set; }

    /// <summary>
    /// The name of this animation clip.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The duration, in seconds, of this animation clip.
    /// </summary>
    public float Length { get; set; }

    /// <summary>
    /// The overall bounds of this animation.
    /// </summary>
    public Rect Bounds { get; set; }

    /// <summary>
    /// A list of all animation events.
    /// </summary>
    public List<EventModel> Events { get; set; } = new List<EventModel>();

    /// <summary>
    /// A list of all animation parts. Each part contains curve data.
    /// </summary>
    public List<AnimPartModel> Parts { get; set; } = new List<AnimPartModel>();
}
