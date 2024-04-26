using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AM.Retexture;

public struct ActiveTextureReport
{
    /// <summary>
    /// Does this report have an error? See <see cref="ErrorMessage"/>.
    /// </summary>
    public bool HasError => ErrorMessage != null || Weapon == null;
    /// <summary>
    /// An error message, if an error has ocurred.
    /// </summary>
    public string ErrorMessage { set; get; }
    /// <summary>
    /// The mod that originally added the weapon.
    /// </summary>
    public ModContentPack SourceMod => Weapon.modContentPack;
    /// <summary>
    /// The weapon.
    /// </summary>
    public ThingDef Weapon { set; get; }
    /// <summary>
    /// The active main texture of this weapon.
    /// </summary>
    public Texture2D ActiveTexture { get; set; }
    /// <summary>
    /// The mod that is currently providing the texture for this weapon.
    /// </summary>
    public ModContentPack ActiveRetextureMod { set; get; }
    /// <summary>
    /// A list, in load order, of all active mods that are providing retextures for this weapon.
    /// One of them will be the <see cref="ActiveRetextureMod"/>.
    /// </summary>
    public List<(ModContentPack mod, Texture2D texture)> AllRetextures { set; get; }
    /// <summary>
    /// The path, relative to the Texture/ folder, of this weapon.
    /// </summary>
    public string TexturePath { set; get; }

    public ActiveTextureReport (string errorMsg)
    {
        ErrorMessage = errorMsg;
    }
}
