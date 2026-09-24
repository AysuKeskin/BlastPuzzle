using BlastPuzzle.Blocks;
using BlastPuzzle.Obstacles;

namespace BlastPuzzle.Boards
{
    // One slot in the grid. A Cell always exists for every coordinate on the board; what
    // changes is what occupies it.
    //
    // THE INVARIANT: a cell holds a Block, OR an Obstacle, OR nothing. Never both. A crate
    // occupies the cell itself rather than sitting under a block, so the two can never
    // coexist. Board enforces this -- see Board.SetBlock and Board.PlaceObstacle, which
    // refuse rather than overwrite.
    //
    // Three questions, three properties, so no caller has to infer one state from another:
    //   IsEmpty      nothing here; refill may fill it
    //   HasBlock     a coloured block; may be matched and may fall
    //   HasObstacle  a crate; blocks neither match through it nor fall through it
    //
    // A reference type on purpose. Board stores these in a Cell[,] and handing out
    // references means a caller reading a cell sees the live slot. Were Cell a struct,
    // GetCell would hand back a *copy* and writing to it would silently fail to change the
    // board -- a classic and very hard-to-spot bug.
    //
    // Cell stores state and answers questions about itself. It runs no gameplay rules.
    public sealed class Cell
    {
        public Cell(BoardPosition position)
        {
            Position = position;
        }

        public BoardPosition Position { get; }

        public Block Block { get; private set; }

        public Obstacle Obstacle { get; private set; }

        public bool HasBlock => Block != null;

        public bool HasObstacle => Obstacle != null;

        // "Nothing here at all." A crate cell is NOT empty -- that distinction is what stops
        // refill from spawning a block on top of a crate and stops gravity from treating a
        // crate as a gap to fall into.
        public bool IsEmpty => Block == null && Obstacle == null;

        // internal rather than public: the Board owns the grid, so mutation is meant to flow
        // through its methods, which validate the coordinate and enforce the invariant above.
        // Being honest about the strength of this: inside a single assembly `internal` is a
        // signpost, not a wall.
        internal void SetBlock(Block block) => Block = block;

        internal void RemoveBlock() => Block = null;

        internal void SetObstacle(Obstacle obstacle) => Obstacle = obstacle;

        internal void RemoveObstacle() => Obstacle = null;

        public override string ToString()
        {
            if (HasObstacle)
            {
                return $"{Position} {Obstacle}";
            }

            return IsEmpty ? $"{Position} empty" : $"{Position} {Block}";
        }
    }
}
