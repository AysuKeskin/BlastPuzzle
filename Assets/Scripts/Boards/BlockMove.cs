using BlastPuzzle.Blocks;

namespace BlastPuzzle.Boards
{
    // A record of one Block changing cells: which block, where it was, where it now is.
    //
    // Carries the Block REFERENCE, not just coordinates, because the presentation layer
    // already maps Block -> BlockView by identity. Handing over the block means the view
    // layer never has to look up "what used to be at (r5,c2)" against a board that has
    // already changed underneath it.
    //
    // A readonly struct: this is a small immutable fact about something that happened, and
    // there is nothing to mutate afterwards.
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
