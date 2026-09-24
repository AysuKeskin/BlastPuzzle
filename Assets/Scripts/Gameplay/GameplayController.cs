using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;
using BlastPuzzle.Presentation;
using UnityEngine;

namespace BlastPuzzle.Gameplay
{
    // Orchestrates one selection: find the group, judge it, apply it, tell the view.
    //
    // It contains no BFS, no removal loop and no view-destruction details -- each of those
    // lives in the class that owns it. What it does own is the SEQUENCE, and the policy
    // value that decides what counts as a legal blast.
    public sealed class GameplayController : MonoBehaviour
    {
        // The smallest group that may be blasted. Lives here, in the orchestration layer,
        // rather than inside ConnectedGroupFinder: the finder reports what IS connected,
        // which is a fact about the board, while this is a rule about the game and is the
        // kind of thing a level or balance config may later want to override.
        [SerializeField]
        private int minimumGroupSize = 2;

        [SerializeField]
        private BoardView boardView;

        private Board board;
        private IReadOnlyList<BlockColor> availableColors;
        private System.Random random;

        // Everything this controller needs to run a move, handed over by the composition
        // root. The Random in particular is created ONCE for the session and reused: a fresh
        // System.Random per click would be seeded from the system clock, and two clicks in
        // the same clock tick would produce identical sequences. Reusing one instance also
        // makes ownership obvious -- there is exactly one source of randomness.
        public void Initialise(Board activeBoard, IReadOnlyList<BlockColor> colors, System.Random sessionRandom)
        {
            board = activeBoard;
            availableColors = colors;
            random = sessionRandom;
        }

        public void HandleBlockSelected(BoardPosition position)
        {
            if (board == null)
            {
                Debug.LogWarning("Selection arrived before a board was set.");
                return;
            }

            // The input layer already range-checks, but this class must not assume its
            // caller did its job -- it is the boundary where a coordinate becomes trusted.
            if (!board.IsInside(position))
            {
                return;
            }

            Cell cell = board.GetCell(position);

            // An empty cell resolves to a coordinate, which is useful for debugging, but it
            // is NOT a block selection: no group lookup runs and no move begins. Nothing
            // downstream should come to rely on selecting empty space.
            if (cell.IsEmpty)
            {
                Debug.Log($"Selected r{position.Row} c{position.Column} (empty cell - not a block selection)");
                return;
            }

            Debug.Log($"Selected r{position.Row} c{position.Column} {cell.Block.Color}");

            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, position);
            Debug.Log($"Connected group size: {group.Count}");

            // LOGICAL FIRST: the board is updated before anything visual happens, so the
            // board is already correct and authoritative by the time the view is told.
            IReadOnlyList<Block> removed = GroupRemover.TryRemoveGroup(board, group, minimumGroupSize);

            if (removed.Count == 0)
            {
                Debug.Log($"Invalid group - no blocks removed (needs {minimumGroupSize})");
                return;
            }

            // THEN VISUAL: the removed Blocks are handed over as identities, so the view can
            // look up exactly which of its objects to destroy.
            boardView.RemoveViews(removed);

            // Settle the board. Again logical first: gravity finishes updating the Board,
            // and only then is the view told which of its existing objects to reposition.
            IReadOnlyList<BlockMove> moves = GravityResolver.ApplyGravity(board);
            boardView.ApplyMoves(moves);

            // Refill last, once the board has settled, so new blocks land in cells that are
            // genuinely still empty. Again logical first, then the view catches up.
            IReadOnlyList<BlockSpawn> spawns = RefillResolver.ApplyRefill(board, availableColors, random);
            boardView.AddViews(spawns);

            // Refill may well have created new same-colour groups. They are NOT blasted
            // automatically: this is a tap-to-blast game, so a new group simply becomes
            // something the player may choose to tap next. No cascade, no chain reaction.
            Debug.Log($"Removed {removed.Count} blocks, {moves.Count} blocks fell, {spawns.Count} spawned");
        }
    }
}
