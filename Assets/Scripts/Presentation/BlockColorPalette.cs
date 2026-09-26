using System;
using BlastPuzzle.Blocks;
using UnityEngine;

namespace BlastPuzzle.Presentation
{
    // A display colour per block colour, for UI that cannot show the block art itself.
    public static class BlockColorPalette
    {
        private static readonly Color Red = new Color(0.90f, 0.22f, 0.27f);
        private static readonly Color Blue = new Color(0.20f, 0.51f, 0.89f);
        private static readonly Color Green = new Color(0.30f, 0.74f, 0.35f);
        private static readonly Color Yellow = new Color(0.98f, 0.80f, 0.18f);
        private static readonly Color Purple = new Color(0.61f, 0.35f, 0.80f);

        public static Color ToDisplayColor(BlockColor blockColor)
        {
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
