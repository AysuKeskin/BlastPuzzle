using System;
using System.Collections;
using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;
using BlastPuzzle.Goals;
using BlastPuzzle.PowerUps;
using BlastPuzzle.Presentation;
using UnityEngine;

namespace BlastPuzzle.Gameplay
{
    // Orchestrates one selection: find the group, judge it, apply it, tell the view.
    public sealed class GameplayController : MonoBehaviour
    {
        [SerializeField]
        private int minimumGroupSize = 2;
        public int MinimumGroupSize => minimumGroupSize;

        [SerializeField]
        private BoardView boardView;

        [Tooltip("Optional. Sound and haptics for events this controller has already decided.")]
        [SerializeField]
        private GameFeedback feedback;

        [Tooltip("Optional. Cosmetic effects only.")]
        [SerializeField]
        private GameplayVFX vfx;

        private Board board;
        private IReadOnlyList<BlockColor> availableColors;
        private System.Random random;
        private GoalTracker goalTracker;
        public int MovesRemaining { get; private set; }

        public GoalTracker Goals => goalTracker;
        private GameplayState state = GameplayState.WaitingForInput;

        // Read-only to everyone else: this controller is the only thing that may change it.
        public GameplayState State => state;
        public event Action GameplayChanged;

        public event Action<GameplayState> StateChanged;
        public void Initialise(
            Board activeBoard,
            IReadOnlyList<BlockColor> colors,
            System.Random sessionRandom,
            int moveLimit,
            GoalTracker levelGoals)
        {
            if (moveLimit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(moveLimit), moveLimit, "A level needs at least one move.");
            }

            StopAllCoroutines();
            board = activeBoard;
            availableColors = colors;
            random = sessionRandom;
            goalTracker = levelGoals ?? throw new ArgumentNullException(nameof(levelGoals));

            MovesRemaining = moveLimit;
            state = MoveAvailabilityChecker.HasAnyValidMove(board, minimumGroupSize)
                ? GameplayState.WaitingForInput : GameplayState.Blocked;
            StateChanged?.Invoke(state);
            GameplayChanged?.Invoke();
        }
        private void SetState(GameplayState next)
        {
            if (state == next)
            {
                return;
            }

            state = next;
            StateChanged?.Invoke(next);
        }

        public void HandleBlockSelected(BoardPosition position)
        {
            // A tap arriving mid-move is dropped, not queued: the board it would be
            // judged against is still changing.
            if (state != GameplayState.WaitingForInput)
            {
                return;
            }

            if (board == null)
            {
                Debug.LogWarning("Selection arrived before a board was set.");
                return;
            }
            if (!board.IsInside(position))
            {
                return;
            }

            Cell cell = board.GetCell(position);
            if (!cell.HasBlock)
            {
                string what = cell.HasObstacle ? cell.Obstacle.ToString() : "empty cell";
                Debug.Log($"Selected r{position.Row} c{position.Column} ({what} - not a block selection)");
                return;
            }

            Debug.Log($"Selected r{position.Row} c{position.Column} {cell.Block}");
            if (cell.Block.IsPowerUp)
            {
                boardView?.CancelSelectionFeedback();
                SetState(GameplayState.ResolvingMove);
                StartCoroutine(ResolvePowerUpSequence(position));
                return;
            }

            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, position);
            Debug.Log($"Connected group size: {group.Count}");
            if (group.Count < minimumGroupSize)
            {
                boardView?.ShowInvalidSelection(cell.Block);
                return;
            }

            // Close input before beginning the move; keep it closed across animation yields.
            boardView?.CancelSelectionFeedback();
            SetState(GameplayState.ResolvingMove);
            StartCoroutine(ResolveMoveSequence(group, position));
        }
        private IEnumerator ConcludeMove()
        {
            GameplayState outcome = LevelOutcome.Evaluate(goalTracker.AreAllGoalsComplete, MovesRemaining);

            if (outcome == GameplayState.Won)
            {
                SetState(GameplayState.Won);
                Debug.Log("LEVEL WON");
                feedback?.PlayWin();
                vfx?.PlayWin(boardView != null ? boardView.transform.position : Vector3.zero);
                yield break;
            }

            if (outcome == GameplayState.Lost)
            {
                SetState(GameplayState.Lost);
                Debug.Log("LEVEL LOST");
                feedback?.PlayLose();
                yield break;
            }
            yield return RecoverIfDeadlocked();
        }
        private IEnumerator RecoverIfDeadlocked()
        {
            ShuffleResult result = BoardShuffleResolver.Shuffle(board, minimumGroupSize, random);

            // The overwhelmingly common case: the board is playable, nothing happened.
            if (!result.WasNeeded)
            {
                yield break;
            }

            if (!result.Succeeded)
            {
                Debug.LogWarning($"Could not find a playable arrangement ({result}); retry is available.");
                SetState(GameplayState.Blocked);
                yield break;
            }

            Debug.Log($"No moves available -- {result}");
            yield return boardView.AnimateShuffle(result.Moves);
        }

        // Tapping a Rocket or Bomb. The resolver owns the geometry; this owns the sequence.
        private IEnumerator ResolvePowerUpSequence(BoardPosition position)
        {
            try
            {
                PowerUpActivationResult result = PowerUpResolver.Activate(board, position);

                MovesRemaining = Math.Max(0, MovesRemaining - 1);
                goalTracker.ProcessRemovedBlocks(result.RemovedBlocks);
                goalTracker.ProcessRemovedObstacles(result.RemovedObstacles);
                GameplayChanged?.Invoke();

                Debug.Log($"Activated power-up: removed {result.RemovedBlocks.Count} blocks, "
                    + $"{result.RemovedObstacles.Count} crates"
                    + $" | Moves remaining: {MovesRemaining} | {goalTracker}");
                foreach (Block activated in result.ActivatedPowerUps)
                {
                    if (activated.Kind == BlockKind.Rocket)
                        feedback?.PlayRocketActivation();
                    else if (activated.Kind == BlockKind.Bomb)
                        feedback?.PlayBombActivation();

                    yield return boardView.AnimateActivation(activated);
                }
                feedback?.PlayCrateBreak(result.RemovedObstacles.Count);

                yield return boardView.AnimateDestruction(result.RemovedBlocks, result.RemovedObstacles);

                yield return SettleBoard();

                // Only now, with nothing still moving on screen, is the level's fate shown.
                yield return ConcludeMove();
            }
            finally
            {
                if (state == GameplayState.ResolvingMove)
                {
                    SetState(GameplayState.WaitingForInput);
                }
            }
        }
        private IEnumerator ResolveMoveSequence(IReadOnlyList<BoardPosition> group, BoardPosition selected)
        {
            try
            {
                // Read before removal: afterwards the cell is empty.
                BlockColor blastedColor = board.GetCell(selected).Block.Color;

                IReadOnlyList<Block> removed = GroupRemover.TryRemoveGroup(board, group, minimumGroupSize);

                // Exactly one move per valid blast.
                MovesRemaining = Math.Max(0, MovesRemaining - 1);

                // Goals advance from the blocks that were actually DESTROYED.
                goalTracker.ProcessRemovedBlocks(removed);
                IReadOnlyList<ObstacleRemoval> crates = ObstacleResolver.ResolveAdjacentHits(board, group);
                goalTracker.ProcessRemovedObstacles(crates);
                Block powerUp = PowerUpRules.TryCreatePowerUp(group, blastedColor);

                if (powerUp != null)
                {
                    board.SetBlock(selected, powerUp);
                }

                GameplayChanged?.Invoke();

                Debug.Log($"Removed {removed.Count} blocks, {crates.Count} crates"
                    + (powerUp != null ? $", created {powerUp} at r{selected.Row} c{selected.Column}" : string.Empty)
                    + $" | Moves remaining: {MovesRemaining} | {goalTracker}");
                if (feedback != null)
                {
                    feedback.PlayBlast(removed.Count);
                    feedback.PlayCrateBreak(crates.Count);
                }
                yield return boardView.AnimateDestruction(removed, crates);

                if (powerUp != null)
                {
                    yield return boardView.AnimatePowerUpCreation(new BlockSpawn(powerUp, selected));
                }

                yield return SettleBoard();

                yield return ConcludeMove();
            }
            finally
            {
                if (state == GameplayState.ResolvingMove)
                {
                    SetState(GameplayState.WaitingForInput);
                }
            }
        }
        private IEnumerator SettleBoard()
        {
            while (true)
            {
                var moves = GravityResolver.ApplyGravity(board);
                yield return boardView.AnimateMoves(moves);
                var diagonal = GravityResolver.ApplyDiagonalGravity(board);
                if (diagonal.Count > 0)
                {
                    yield return boardView.AnimateMoves(diagonal);
                    continue;
                }
                // Replenish open columns, then let those real blocks flow around crates.
                var incoming = RefillResolver.ApplyRefill(board, availableColors, random, topAccessibleOnly: true);
                if (incoming.Count > 0)
                {
                    yield return boardView.AnimateSpawns(incoming);
                    continue;
                }
                // Fully enclosed pockets have no physical entry. Retain a bounded fallback
                // for authored layouts instead of leaving the board permanently incomplete.
                var enclosed = RefillResolver.ApplyRefill(board, availableColors, random);
                yield return boardView.AnimateSpawns(enclosed);
                break;
            }
        }
    }
}
