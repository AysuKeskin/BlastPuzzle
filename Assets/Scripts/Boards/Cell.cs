using BlastPuzzle.Blocks;

namespace BlastPuzzle.Boards
{
    // One slot in the grid. A Cell always exists for every coordinate on the
    // board; what changes is whether it currently holds a Block. An empty slot
    // is a Cell whose Block is null.
    //
    // A reference type on purpose. Board stores these in a Cell[,] and handing
    // out references means a caller reading a cell sees the live slot. Were Cell
    // a struct, GetCell would hand back a *copy* and writing to it would silently
    // fail to change the board -- a classic and very hard-to-spot bug.
    //
    // Cell stores state and answers questions about itself. It runs no gameplay
    // rules: no matching, no gravity, no scoring.
    public sealed class Cell
    {
        public Cell(BoardPosition position)
        {
            Position = position;
        }

        public BoardPosition Position { get; }

        public Block Block { get; private set; }

        public bool IsEmpty => Block == null;

        // internal rather than public: the Board owns the grid, so mutation is
        // meant to flow through Board.SetBlock / Board.RemoveBlock, which can
        // validate the coordinate first. Being honest about the strength of this:
        // inside a single assembly `internal` is a signpost, not a wall. It
        // becomes a real boundary only if the project is ever split into
        // separate assemblies.
        internal void SetBlock(Block block) => Block = block;

        internal void RemoveBlock() => Block = null;

        public override string ToString() => IsEmpty ? $"{Position} empty" : $"{Position} {Block}";
    }
}
