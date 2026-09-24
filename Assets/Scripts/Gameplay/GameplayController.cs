using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;
using UnityEngine;

namespace BlastPuzzle.Gameplay
{
    // Receives a logical coordinate and asks the Board about it.
    //
    // Deliberately knows nothing about screen pixels, cameras, Transforms, colliders or
    // GameObjects. By the time a selection reaches this class it is already just a row
    // and a column, so every later rule (matching, moves, win/lose) can be written
    // against the board alone.
    //
    // Milestone 3 scope: report what was selected. It does not mutate the board.
    public sealed class GameplayController : MonoBehaviour
    {
        private Board board;

        public void SetBoard(Board activeBoard)
        {
            board = activeBoard;
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

            Block block = cell.Block;
            Debug.Log($"Selected r{position.Row} c{position.Column} {block.Color}");

            // Coordinate the call; the algorithm itself lives in ConnectedGroupFinder.
            // Milestone 4 only reports the group. Nothing is removed and no move is spent,
            // and the minimum-size rule that decides move legality is not applied here yet.
            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, position);
            Debug.Log($"Connected group size: {group.Count}");
        }
    }
}
