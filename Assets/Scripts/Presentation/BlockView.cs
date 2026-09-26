using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;
using UnityEngine;

namespace BlastPuzzle.Presentation
{
    [RequireComponent(typeof(SpriteRenderer))]
    // One block on screen. Recycled, so Bind must leave no trace of its last life.
    public sealed class BlockView : MonoBehaviour
    {
        [SerializeField]
        private SpriteRenderer spriteRenderer;

        [Tooltip("Shared artwork mapping, so the board and the HUD cannot disagree.")]
        [SerializeField]
        private BlockSpriteSet sprites;

        // Which logical Block this view currently stands for.
        public Block Block { get; private set; }
        public BoardPosition Position { get; private set; }
        private static readonly Color Untinted = Color.white;
        public void Bind(Block block, BoardPosition position)
        {
            Block = block;
            Position = position;

            transform.localScale = Vector3.one;
            transform.localRotation = Quaternion.identity;

            spriteRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            spriteRenderer.sprite = sprites.For(block);
            spriteRenderer.color = Untinted;
        }
        public void PrepareForPool()
        {
            Block = null;

            transform.localScale = Vector3.one;
            transform.localRotation = Quaternion.identity;

            spriteRenderer.sprite = null;
            spriteRenderer.color = Untinted;
        }
        public void SetLocalPosition(Vector3 localPosition)
        {
            transform.localPosition = localPosition;
        }
        public void SetBoardPosition(BoardPosition position)
        {
            Position = position;
        }
        public void SetScale(float scale)
        {
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        public void SetAlpha(float alpha)
        {
            spriteRenderer.color = new Color(Untinted.r, Untinted.g, Untinted.b, alpha);
        }
        public void ResetAppearance()
        {
            transform.localScale = Vector3.one;
            spriteRenderer.color = Untinted;
        }
    }
}
