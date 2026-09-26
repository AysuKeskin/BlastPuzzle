using BlastPuzzle.Boards;
using BlastPuzzle.Obstacles;
using UnityEngine;

namespace BlastPuzzle.Presentation
{
    [RequireComponent(typeof(SpriteRenderer))]
    // One crate on screen.
    public sealed class CrateView : MonoBehaviour
    {
        [SerializeField]
        private SpriteRenderer spriteRenderer;

        public Obstacle Obstacle { get; private set; }

        public BoardPosition Position { get; private set; }

        public void Bind(Obstacle obstacle, BoardPosition position)
        {
            Obstacle = obstacle;
            Position = position;
        }

        public void SetLocalPosition(Vector3 localPosition)
        {
            transform.localPosition = localPosition;
        }

        // Same plain setters as BlockView: BoardView drives the timing from one batch loop.
        public void SetScale(float scale)
        {
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        public void SetAlpha(float alpha)
        {
            Color color = spriteRenderer.color;
            spriteRenderer.color = new Color(color.r, color.g, color.b, alpha);
        }
    }
}
