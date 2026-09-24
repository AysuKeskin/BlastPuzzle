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

        [Tooltip("Overlay drawn on top of the tinted square for power-ups. Hidden for normal blocks.")]
        [SerializeField]
        private SpriteRenderer iconRenderer;

        [SerializeField]
        private Sprite rocketHorizontalIcon;

        [SerializeField]
        private Sprite rocketVerticalIcon;

        [SerializeField]
        private Sprite bombIcon;

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
        // The tint this view was bound with, kept so alpha can be changed during a removal
        // fade without losing the colour.
        private Color tint;

        public void Bind(Block block, BoardPosition position)
        {
            Block = block;
            Position = position;

            // Power-ups keep the colour of the group that made them, so the tint is the
            // same code path for every kind; only the overlay differs.
            tint = BlockColorPalette.ToDisplayColor(block.Color);
            spriteRenderer.color = tint;

            ShowIconFor(block);
        }

        // One prefab renders all three kinds. A second prefab per power-up would duplicate
        // the square, the collider-free setup and the naming for the sake of one sprite.
        private void ShowIconFor(Block block)
        {
            Sprite icon = block.Kind switch
            {
                BlockKind.Rocket => block.Direction == RocketDirection.Horizontal
                    ? rocketHorizontalIcon
                    : rocketVerticalIcon,
                BlockKind.Bomb => bombIcon,
                _ => null
            };

            iconRenderer.sprite = icon;
            iconRenderer.enabled = icon != null;
        }

        // Position is set separately from Bind because later milestones move a block
        // without re-binding it: gravity changes where a view is, not what it represents.
        public void SetLocalPosition(Vector3 localPosition)
        {
            transform.localPosition = localPosition;
        }

        // The cached coordinate is updated the moment the BOARD changes, separately from the
        // transform, which then animates toward it. During a fall the view already reports
        // the cell it logically occupies while its sprite is still travelling -- which is
        // what keeps "is every view at its block's cell?" answerable mid-animation.
        public void SetBoardPosition(BoardPosition position)
        {
            Position = position;
        }

        // --- animation primitives -------------------------------------------------------
        //
        // Deliberately plain setters rather than coroutines. BoardView drives every view
        // from one batch loop, so a view only needs to be told what to look like this frame;
        // it owns no timing, no easing and no sequencing, and therefore no gameplay rules.

        public void SetScale(float scale)
        {
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        public void SetAlpha(float alpha)
        {
            spriteRenderer.color = new Color(tint.r, tint.g, tint.b, alpha);

            if (iconRenderer.enabled)
            {
                Color icon = iconRenderer.color;
                iconRenderer.color = new Color(icon.r, icon.g, icon.b, alpha);
            }
        }

        // Puts the view back to its resting appearance, used as the explicit snap at the end
        // of every animation so nothing drifts on floating-point rounding.
        public void ResetAppearance()
        {
            transform.localScale = Vector3.one;
            spriteRenderer.color = tint;

            if (iconRenderer.enabled)
            {
                Color icon = iconRenderer.color;
                iconRenderer.color = new Color(icon.r, icon.g, icon.b, 1f);
            }
        }
    }
}
