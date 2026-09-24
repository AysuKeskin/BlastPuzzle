using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;
using UnityEngine;

namespace BlastPuzzle.Presentation
{
    // Renders a logical Board as a grid of BlockViews parented under this object.
    //
    // The dependency runs one way only: BoardView reads Board. Board has never heard of
    // BoardView, so the logical state is never derived from where a Transform happens to be.
    // This class contains no gameplay rules -- no matching, no gravity, no scoring. It
    // answers one question: "given this board, what should be on screen?"
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField]
        private BlockView blockPrefab;

        [SerializeField]
        private float cellSize = 1f;

        [SerializeField]
        private float spacing = 0.05f;

        // Reference identity, not value equality: Block does not override Equals, so two
        // blocks are the same key only if they are literally the same object. That is
        // exactly what is wanted -- "the view showing THIS block" -- and it is what lets
        // Milestone 5/6 find the right view for a block that was removed or moved.
        private readonly Dictionary<Block, BlockView> viewsByBlock = new Dictionary<Block, BlockView>();

        private Board board;

        // Distance from one cell's centre to the next, including the gap between them.
        private float CellStride => cellSize + spacing;

        // Half the board's visible extent: every cell's width plus the gaps between them.
        // n cells and (n - 1) gaps, i.e. n * stride - one trailing gap.
        private float HalfBoardWidth => (board.Columns * CellStride - spacing) * 0.5f;

        private float HalfBoardHeight => (board.Rows * CellStride - spacing) * 0.5f;

        // One-shot render. No events and no per-frame Update: nothing changes the board
        // yet, so re-rendering on a schedule would be work with no cause.
        public void Build(Board boardToRender)
        {
            Clear();
            board = boardToRender;

            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    Cell cell = board.GetCell(row, column);

                    // An empty cell gets no view at all, rather than a hidden or
                    // transparent one. "No block here" and "a block you cannot see"
                    // must never be the same thing on screen.
                    if (cell.IsEmpty)
                    {
                        continue;
                    }

                    CreateViewFor(cell);
                }
            }
        }

        // Destroys every view this component created and forgets them.
        public void Clear()
        {
            foreach (BlockView view in viewsByBlock.Values)
            {
                if (view != null)
                {
                    // Destroy is deferred to the end of the frame, so these objects are
                    // still children for the rest of this frame. Harmless while Build is
                    // called once; worth remembering if a rebuild ever counts children.
                    Destroy(view.gameObject);
                }
            }

            viewsByBlock.Clear();
            board = null;
        }

        // Turns a logical grid coordinate into a local offset from this object's origin.
        //
        // Row 0 is the bottom row and column 0 the left, matching the domain convention,
        // so +row moves up (+y) and +column moves right (+x) with no flip anywhere.
        //
        // Centering: cell centres run from index 0 to index (count - 1), so the midpoint
        // of that range is (count - 1) / 2. Subtracting it puts the middle of the board at
        // this object's origin. Note the 0.5f rather than an integer 2: with 8 columns the
        // centre sits at 3.5, and integer division would truncate it to 3 and shift the
        // whole board half a cell off-centre.
        public Vector3 BoardPositionToLocalPosition(BoardPosition position)
        {
            float x = (position.Column - (board.Columns - 1) * 0.5f) * CellStride;
            float y = (position.Row - (board.Rows - 1) * 0.5f) * CellStride;
            return new Vector3(x, y, 0f);
        }

        // The inverse of BoardPositionToLocalPosition: a point in the world becomes the
        // grid coordinate it falls on. This is arithmetic, not a physics query -- see the
        // milestone notes for why a uniform grid does not need colliders.
        //
        // Returns false when the point lies outside the board, which is how "tapped the
        // background" is distinguished from "tapped a cell".
        public bool TryGetBoardPosition(Vector3 worldPosition, out BoardPosition position)
        {
            position = default;

            if (board == null)
            {
                return false;
            }

            // Into this object's local space first, so moving or scaling BoardRoot keeps working.
            Vector3 local = transform.InverseTransformPoint(worldPosition);

            // Reject anything beyond the board's visible rectangle BEFORE rounding.
            // Without this, rounding to nearest extends each edge cell's catchment half a
            // gap (spacing / 2) past the artwork, so a tap just outside the board would
            // snap back onto an edge block. Interior gaps are unaffected: they lie inside
            // this rectangle and still resolve to the nearest cell.
            if (Mathf.Abs(local.x) > HalfBoardWidth || Mathf.Abs(local.y) > HalfBoardHeight)
            {
                return false;
            }

            // Rounding to the nearest index means a tap landing in the gap between two
            // blocks resolves to the closer one, rather than being swallowed. On a phone
            // that forgiveness matters more than pixel-exactness.
            int column = Mathf.RoundToInt(local.x / CellStride + (board.Columns - 1) * 0.5f);
            int row = Mathf.RoundToInt(local.y / CellStride + (board.Rows - 1) * 0.5f);

            if (!board.IsInside(row, column))
            {
                return false;
            }

            position = new BoardPosition(row, column);
            return true;
        }

        // Exposed for verification and for later systems that need a specific view.
        public BlockView GetViewFor(Block block) =>
            viewsByBlock.TryGetValue(block, out BlockView view) ? view : null;

        public int ViewCount => viewsByBlock.Count;

        private void CreateViewFor(Cell cell)
        {
            // Instantiate copies the prefab into the scene as a child of this transform.
            BlockView view = Instantiate(blockPrefab, transform);
            view.Bind(cell.Block, cell.Position);
            view.SetLocalPosition(BoardPositionToLocalPosition(cell.Position));

            // Naming generated objects after their coordinate makes the live hierarchy
            // readable while debugging.
            view.name = $"Block_r{cell.Position.Row}_c{cell.Position.Column}";

            viewsByBlock.Add(cell.Block, view);
        }
    }
}
