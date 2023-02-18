using System.Collections.Generic;

namespace AAM.Data.Model;

public class AnimPartModel
{
    /// <summary>
    /// The internal ID of this part.
    /// It will change every time the animation is exported, but will be the same for every animation
    /// in the animation set.
    /// </summary>
    public int ID { get; set; }

    /// <summary>
    /// The hierarchical path of this part within the animation.
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// The optional custom name of this part.
    /// </summary>
    public string CustomName { get; set; }

    /// <summary>
    /// The ID of the parent of this part.
    /// If 0, it has no parent.
    /// </summary>
    public int ParentID { get; set; }

    /// <summary>
    /// The texture path of this part, Rimworld style.
    /// May be null if it does not have a texture.
    /// </summary>
    public string TexturePath { get; set; }

    /// <summary>
    /// Is this part transparent by default?
    /// </summary>
    public bool TransparentByDefault { get; set; }

    /// <summary>
    /// A map of property names to curves for this object.
    /// </summary>
    public Dictionary<string, CurveModel> Curves { get; set; } = new Dictionary<string, CurveModel>();

    /// <summary>
    /// A map of property names to default values of curves.
    /// </summary>
    public Dictionary<string, float> DefaultValues { get; set; } = new Dictionary<string, float>();

    /// <summary>
    /// The optional ID of the split draw pivot part.
    /// If 0, the pivot does not exist.
    /// </summary>
    public int SplitDrawPivotPartID { get; set; }

    /// <summary>
    /// A list of sweep path points.
    /// </summary>
    public List<SweepPoint[]> SweepPaths { get; set; } = new List<SweepPoint[]>();
}