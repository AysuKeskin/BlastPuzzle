using System;
using BlastPuzzle.Blocks;
using UnityEngine;

namespace BlastPuzzle.Presentation
{
    // Maps a logical BlockColor onto a screen colour.
    //
    // This mapping lives in the presentation layer, not next to the enum, because
    // UnityEngine.Color is a rendering concept. BlockColor.Red means "the red block
    // kind" as a rule of the game; what red actually looks like is a display detail.
    // Keeping them apart is what allows BlastPuzzle.Blocks to stay Unity-free.
    public static class BlockColorPalette
    {
        // Placeholder art. Picked to stay clearly distinguishable from one another
        // rather than to look finished.
        private static readonly Color Red = new Color(0.90f, 0.22f, 0.27f);
        private static readonly Color Blue = new Color(0.20f, 0.51f, 0.89f);
        private static readonly Color Green = new Color(0.30f, 0.74f, 0.35f);
        private static readonly Color Yellow = new Color(0.98f, 0.80f, 0.18f);
        private static readonly Color Purple = new Color(0.61f, 0.35f, 0.80f);

        public static Color ToDisplayColor(BlockColor blockColor)
        {
            // Throwing on an unhandled value means adding a colour to the enum without
            // choosing its look fails loudly here, instead of rendering silently wrong.
            return blockColor switch
            {
                BlockColor.Red => Red,
                BlockColor.Blue => Blue,
                BlockColor.Green => Green,
                BlockColor.Yellow => Yellow,
                BlockColor.Purple => Purple,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(blockColor), blockColor, "No display colour defined for this BlockColor.")
            };
        }
    }
}
