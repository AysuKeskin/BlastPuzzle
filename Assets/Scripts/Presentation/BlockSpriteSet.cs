using System;
using BlastPuzzle.Blocks;
using UnityEngine;

namespace BlastPuzzle.Presentation
{
    // The one mapping from a block to its artwork, shared by the board and the HUD.
    [CreateAssetMenu(fileName = "BlockSprites", menuName = "BlastPuzzle/Block Sprite Set", order = 1)]
    public sealed class BlockSpriteSet : ScriptableObject
    {
        // Grouped by colour rather than fifteen loose fields, so adding a colour means
        // adding one entry instead of four scattered references.
        [Serializable]
        private struct ColorArt
        {
            public BlockColor color;
            public Sprite block;
            public Sprite rocketHorizontal;
            public Sprite rocketVertical;
            public Sprite bomb;
        }

        [SerializeField]
        private ColorArt[] palette;

        [Header("Obstacles")]
        [SerializeField]
        private Sprite crate;

        public Sprite Crate => crate;

        public Sprite For(BlockColor color) => Art(color).block;

        // Power-ups keep the colour of the group that created them.
        public Sprite For(Block block)
        {
            ColorArt art = Art(block.Color);

            return block.Kind switch
            {
                BlockKind.Rocket => block.Direction == RocketDirection.Horizontal
                    ? art.rocketHorizontal
                    : art.rocketVertical,
                BlockKind.Bomb => art.bomb,
                _ => art.block
            };
        }

        // Throws rather than drawing nothing, so a missing entry fails loudly.
        private ColorArt Art(BlockColor color)
        {
            if (palette != null)
            {
                foreach (ColorArt entry in palette)
                {
                    if (entry.color == color)
                    {
                        return entry;
                    }
                }
            }

            throw new ArgumentOutOfRangeException(nameof(color), color, "No artwork assigned for this BlockColor.");
        }
    }
}
