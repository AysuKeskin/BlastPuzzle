using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;
using BlastPuzzle.Gameplay;
using BlastPuzzle.Levels;
using BlastPuzzle.Presentation;
using UnityEngine;

namespace BlastPuzzle.Core
{
    // Composition root: builds the pieces and wires them together, then gets out of the way.
    //
    // It holds no game rules and no board state of its own. Its whole job is to decide
    // who gets which object at startup.
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField]
        private bool logStartup = true;

        [SerializeField]
        private BoardView boardView;

        [SerializeField]
        private GameplayController gameplayController;

        // Start rather than Awake: every component's Awake has already run by then, so the
        // BoardView and input handler are guaranteed initialised before they are used.
        private void Start()
        {
            // TEMPORARY: replaced by real level loading in Milestone 10.
            Board board = DemoBoardFactory.CreateDemoBoard();
            IReadOnlyList<BlockColor> availableColors = DemoBoardFactory.AvailableColors;

            // One Random for the whole session, created here in the composition root and
            // shared with the controller, so there is a single source of randomness rather
            // than one per system or one per click.
            var random = new System.Random();

            // The demo layout leaves a couple of cells empty. Rather than teaching the
            // factory to fill them, run the real refill system once, so gameplay starts on a
            // full board and there is only one piece of code that knows how to fill a cell.
            RefillResolver.ApplyRefill(board, availableColors, random);

            // One Board instance, handed to everyone who needs to read it. The view renders
            // it, the controller queries it, and it stays the single source of truth.
            boardView.Build(board);
            gameplayController.Initialise(board, availableColors, random);

            if (logStartup)
            {
                Debug.Log($"Blast Puzzle started: {board.Rows}x{board.Columns} board, {boardView.ViewCount} block views.");
            }
        }
    }
}
