using BlastPuzzle.Boards;
using BlastPuzzle.Obstacles;
using UnityEngine;

namespace BlastPuzzle.Presentation
{
    // Displays one crate.
    //
    // Simpler than BlockView because a crate never moves: it is placed once and later
    // destroyed, so there is no MoveTo and no need to keep a cached coordinate in step with
    // a transform. Reads the logical obstacle, never writes to it.
    [RequireComponent(typeof(SpriteRenderer))]
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
