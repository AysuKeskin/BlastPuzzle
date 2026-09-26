using System.Collections.Generic;
using BlastPuzzle.Boards;

namespace BlastPuzzle.Gameplay
{
    // What a shuffle attempt did: not needed, succeeded, or failed.
    public readonly struct ShuffleResult
    {
        private static readonly BlockMove[] NoMoves = new BlockMove[0];

        private ShuffleResult(bool wasNeeded, bool succeeded, int attempts, IReadOnlyList<BlockMove> moves)
        {
            WasNeeded = wasNeeded;
            Succeeded = succeeded;
            Attempts = attempts;
            Moves = moves;
        }

        // False when the board already had a legal move, so nothing was touched.
        public bool WasNeeded { get; }

        // True when the board now has a legal move. Trivially true when none was needed.
        public bool Succeeded { get; }

        // How many permutations were tried. Zero when no shuffle was needed.
        public int Attempts { get; }

        // Empty unless blocks actually moved.
        public IReadOnlyList<BlockMove> Moves { get; }

        public static ShuffleResult NotNeeded() => new ShuffleResult(false, true, 0, NoMoves);

        public static ShuffleResult Shuffled(IReadOnlyList<BlockMove> moves, int attempts) =>
            new ShuffleResult(true, true, attempts, moves);
        public static ShuffleResult Failed(int attempts) => new ShuffleResult(true, false, attempts, NoMoves);

        public override string ToString() =>
            !WasNeeded ? "shuffle not needed"
                : Succeeded ? $"shuffled {Moves.Count} blocks in {Attempts} attempt(s)"
                : $"shuffle FAILED after {Attempts} attempt(s)";
    }
}
