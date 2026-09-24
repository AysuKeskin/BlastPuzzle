using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;
using UnityEngine;

namespace BlastPuzzle.Presentation
{
    // Displays exactly one logical Block.
    //
    // Strictly one-directional: this reads the Block and never writes to it. The Block
    // does not know a BlockView exists. If every BlockView in the scene were deleted,
    // the logical board would be completely unaffected.
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BlockView : MonoBehaviour
    {
        [SerializeField]
        private SpriteRenderer spriteRenderer;

        // Which logical Block this view currently stands for.
        public Block Block { get; private set; }

        // Which cell this view was last told it is showing.
        //
        // This is a CACHED COPY of a fact the Board owns, not a second source of truth.
        // Nothing reads it to decide game rules, and the Board is never consulted about
        // where a view thinks it is. If the two ever disagree, the Board is right by
        // definition and this value is simply stale.
        public BoardPosition Position { get; private set; }

        // Point this view at a logical block and take on its appearance.
        public void Bind(Block block, BoardPosition position)
        {
            Block = block;
            Position = position;
            spriteRenderer.color = BlockColorPalette.ToDisplayColor(block.Color);
        }

        // Position is set separately from Bind because later milestones move a block
        // without re-binding it: gravity changes where a view is, not what it represents.
        public void SetLocalPosition(Vector3 localPosition)
        {
            transform.localPosition = localPosition;
        }
    }
}
