using BlastPuzzle.Blocks;

namespace BlastPuzzle.Boards
{
    // A block that changed cells: which block, from where, to where.
    public readonly struct BlockMove
    {
        public BlockMove(Block block, BoardPosition from, BoardPosition to)
        {
            Block = block;
            From = from;
            To = to;
        }

        public Block Block { get; }

        public BoardPosition From { get; }

        public BoardPosition To { get; }

        public override string ToString() => $"{Block} {From} -> {To}";
    }
}
