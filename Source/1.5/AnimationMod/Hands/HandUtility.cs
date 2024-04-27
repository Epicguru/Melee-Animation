using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace AM.Hands;

public static class HandUtility
{
    private static readonly List<Apparel> clothesCoveringHands = [];
    private static readonly Dictionary<Texture2D, Color> textureToAverageColor = [];
    private static readonly HashSet<ThingDef> apparelThatCoversHands = [];

    [UsedImplicitly]
    [DebugAction("Melee Animation", actionType = DebugActionType.ToolMapForPawns)]
    public static void LogHandsInfo(Pawn pawn)
    {
        Span<HandInfo> hands = stackalloc HandInfo[8];

        var timer = Stopwatch.StartNew();
        int count = GetHandData(pawn, hands);
        timer.Stop();
        
        Core.Log($"[{timer.Elapsed.TotalMilliseconds:F2} ms] {pawn} has {count} hands:");
        
        for (int i = 0; i < count; i++)
        {
            ref var hand = ref hands[i];
            Core.Log($"[{i}] {hand.Flags}: {hand.Color} <color=#{ColorUtility.ToHtmlStringRGB(hand.Color)}>\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588</color>");
        }
    }

    public static void DoInitialLoading()
    {
        var timer = Stopwatch.StartNew();
        
        foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            var defApparel = def.apparel;
            if (defApparel == null)
                continue;
            
            bool doesCoverHands = defApparel.CoversBodyPartGroup(AM_DefOf.Hands) ||
                                  defApparel.CoversBodyPartGroup(BodyPartGroupDefOf.LeftHand) || 
                                  defApparel.CoversBodyPartGroup(BodyPartGroupDefOf.RightHand);

            if (doesCoverHands)
            {
                apparelThatCoversHands.Add(def);
            }
        }
        
        timer.Stop();
        Core.Log($"Found {apparelThatCoversHands.Count} items of clothing that cover hands, took {timer.Elapsed.TotalMilliseconds:F2} ms:\n{string.Join(",\n", apparelThatCoversHands.Select(ap => $"{ap.defName} ({ap.LabelCap})"))}");
    }
    
    public static int GetHandData(Pawn pawn, Span<HandInfo> output)
    {
        if (pawn == null)
            return 0;

        int count = 0;
        
        clothesCoveringHands.AddRange(GetAllApparelCoveringHands(pawn));
        clothesCoveringHands.SortByDescending(ap => ap.def.apparel.layers.MaxBy(l => l.drawOrder).drawOrder);

        count += GetNaturalHandData(pawn, output);

        if (count < output.Length)
        {
            count += GetArtificialHandData(pawn, output[count..]);
        }
        
        clothesCoveringHands.Clear();
        
        return count;
    }

    private static int GetNaturalHandData(Pawn pawn, Span<HandInfo> output)
    {
        if (pawn.health?.hediffSet == null)
            return 0;

        int count = 0;
        foreach (var part in pawn.health.hediffSet.GetNotMissingParts())
        {
            if (!IsHand(part))
                continue;
            
            // Check for clothes or armor covering the hand.
            Color? apparelColor = TryGetColorOfApparelCoveringHand(part);
            
            if (apparelColor != null)
            {
                // Write hand with apparel color.
                if (!TryWriteToOutput(ref count, output, new HandInfo
                {
                    Color = apparelColor.Value,
                    Flags = HandFlags.Natural | HandFlags.Clothed
                })){ return count; }
            }
            else
            {
                // Write hand with natural skin color.
                if (!TryWriteToOutput(ref count, output, new HandInfo
                {
                    Color = GetSkinColor(pawn),
                    Flags = HandFlags.Natural
                })){ return count; }
            }
        }

        return count;
    }

    private static int GetArtificialHandData(Pawn pawn, Span<HandInfo> output)
    {
        if (pawn.health?.hediffSet == null)
            return 0;

        int count = 0;
        foreach (var hediff in pawn.health.hediffSet.hediffs)
        {
            if (hediff is not Hediff_AddedPart addedPart)
                continue;

            if (!IsArtificialHand(addedPart.Part, out var actualHandPart))
                continue;
            
            Color? apparelColor = TryGetColorOfApparelCoveringHand(actualHandPart);
            Color color = apparelColor ?? TryGetArtificialHandColor(addedPart.def) ?? GetSkinColor(pawn);
                
            if (!TryWriteToOutput(ref count, output, new HandInfo
            {
                Color = color,
                Flags = HandFlags.Artificial | (apparelColor != null ? HandFlags.Clothed : 0)
            })){ return count; }
        }

        return count;
    }

    private static bool IsArtificialHand(BodyPartRecord part, out BodyPartRecord actualHand)
    {
        if (part.def == BodyPartDefOf.Hand)
        {
            actualHand = part;
            return true;
        }

        if (part.parts == null || part.parts.Count == 0)
        {
            actualHand = null;
            return false;
        }

        foreach (var child in part.parts)
        {
            if (IsArtificialHand(child, out actualHand))
                return true;
        }

        actualHand = null;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Color? TryGetArtificialHandColor(HediffDef part)
    {
        if (part.spawnThingOnRemoved == null)
            return null;

        TechLevel techLevel = part.spawnThingOnRemoved.techLevel;
        return techLevel switch
        {
            TechLevel.Animal => new Color32(153, 105, 34, 255),
            TechLevel.Neolithic => new Color32(153, 105, 34, 255),
            TechLevel.Medieval => new Color32(132, 141, 145, 255),
            TechLevel.Industrial => new Color32(132, 141, 145, 255),
            TechLevel.Spacer => new Color32(104, 189, 227, 255),
            TechLevel.Ultra => new Color32(104, 189, 227, 255),
            TechLevel.Archotech => new Color32(119, 135, 93, 255),
            _ => null
        };
    }
    
    private static bool TryWriteToOutput(ref int count, Span<HandInfo> dest, in HandInfo hand)
    {
        if (count >= dest.Length)
            return false;
        
        dest[count++] = hand;
        return true;
    }

    private static Color? TryGetColorOfApparelCoveringHand(BodyPartRecord hand)
    {
        foreach (var apparel in clothesCoveringHands)
        {
            if (!apparel.def.apparel.CoversBodyPart(hand))
                continue;
                
            return GetApparelDrawColor(apparel);
        }

        return null;
    }

    private static Color GetApparelDrawColor(Thing apparel)
    {
        // Comp Colorable comes first, for dyed clothes and the like.
        var colorComp = apparel.TryGetComp<CompColorable>();
        if (colorComp is {Active: true})
        {
            return colorComp.Color;
        }
        
        // Attempt to use stuff color, if it has stuff.
        if (apparel.Stuff != null)
        {
            return apparel.def.GetColorForStuff(apparel.Stuff);
        }
        
        // Fall back to getting color from the texture pixels.
        var texture = apparel.Graphic?.MatSingle?.mainTexture as Texture2D;
        if (texture != null)
        {
            return GetTextureAverageColor(texture);
        }
        
        // No clue, give up and just use grey.
        return Color.grey;
    }

    private static Color GetTextureAverageColor(Texture2D texture)
    {
        if (textureToAverageColor.TryGetValue(texture, out var cached))
            return cached;

        var timer = Stopwatch.StartNew();
        var tempRenderTexture = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
        Texture2D tempTexture = null;
        RenderTexture oldActiveRT = RenderTexture.active;
        try
        {
            Graphics.Blit(texture, tempRenderTexture);

            RenderTexture.active = tempRenderTexture;

            tempTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, true);
            tempTexture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0, false);

            var pixels = tempTexture.GetPixels32(0);
            var color = MakeAverageColor(pixels);
            textureToAverageColor.Add(texture, color);
            
            timer.Stop();
            Core.Log($"Took {timer.Elapsed.TotalMilliseconds:F3} ms to calculate average color of {texture.name}, {texture.width}x{texture.height}, result is: {color} <color=#{ColorUtility.ToHtmlStringRGB(color)}>\u2588\u2588\u2588\u2588\u2588\u2588</color>");
            
            return color;
        }
        finally
        {
            RenderTexture.active = oldActiveRT;
            RenderTexture.ReleaseTemporary(tempRenderTexture);
            if (tempTexture != null)
                UnityEngine.Object.Destroy(tempTexture);
        }
    }

    private static Color MakeAverageColor(IEnumerable<Color32> pixels)
    {
        double r = 0;
        double g = 0;
        double b = 0;
        double sum = 0;

        const double BYTE_MAX_RECIPROCAL = 1.0 / 255.0;

        foreach (var pixel in pixels)
        {
            double alpha = pixel.a * BYTE_MAX_RECIPROCAL;
            double average = (pixel.r + pixel.g + pixel.b) * (1.0 / 3.0);
            double weight = alpha * SmoothStep(0, 0.25, average);            

            r += pixel.r * weight;
            g += pixel.g * weight;
            b += pixel.b * weight;

            sum += weight;
        }

        double normalizer = 1.0 / sum * BYTE_MAX_RECIPROCAL;
        return new Color((float) (r * normalizer), (float) (g * normalizer), (float) (b * normalizer), 1f);
    }

    private static double SmoothStep(double a, double b, double x)
    {
        double t = Math.Min(Math.Max((x - a) / (b - a), 0), 1);
        return t * t * (3 - 2 * t);
    }
    
    private static Color GetSkinColor(Pawn pawn)
    {
        return pawn.story?.SkinColor ?? Color.white;
    }

    private static IEnumerable<Apparel> GetAllApparelCoveringHands(Pawn pawn)
    {
        if (pawn.apparel == null)
            return [];

        return pawn.apparel.WornApparel.Where(ap => apparelThatCoversHands.Contains(ap.def));
    }
    
    private static bool IsHand(BodyPartRecord bodyPart) => bodyPart.def == BodyPartDefOf.Hand;
}
