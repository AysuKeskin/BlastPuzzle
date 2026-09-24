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

        [Tooltip("Optional. Sound and haptics for events this controller has already decided.")]
        [SerializeField]
        private GameFeedback feedback;

        private Board board;
        private IReadOnlyList<BlockColor> availableColors;
        private System.Random random;
        private GoalTracker goalTracker;

        // Runtime progress, not configuration. The starting value arrives through Initialise;
        // what is left is only ever known here, for this attempt.
        public int MovesRemaining { get; private set; }

        public GoalTracker Goals => goalTracker;

        // A plain private field rather than an auto-property, so the name is stable for the
        // tests that reach in to drive it. Production exposes no setter at all.
        private GameplayState state = GameplayState.WaitingForInput;

        // Read-only to everyone else: this controller is the only thing that may change it.
        public GameplayState State => state;

        // Everything this controller needs to run a move, handed over by the composition
        // root. The Random in particular is created ONCE for the session and reused: a fresh
        // System.Random per click would be seeded from the system clock, and two clicks in
        // the same clock tick would produce identical sequences. Reusing one instance also
        // makes ownership obvious -- there is exactly one source of randomness.
        // Takes plain values rather than the LevelDefinition asset itself. That keeps this
        // controller free of any ScriptableObject type, so it cannot write back into shared
        // configuration even by accident, and so every test can set up a level with ordinary
        // arguments instead of authoring an asset. The composition root does the unpacking.
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

            board = activeBoard;
            availableColors = colors;
            random = sessionRandom;
            goalTracker = levelGoals ?? throw new ArgumentNullException(nameof(levelGoals));

            MovesRemaining = moveLimit;
            state = GameplayState.WaitingForInput;
        }

        public void HandleBlockSelected(BoardPosition position)
        {
            // Re-entrancy guard. A selection arriving while a move is being resolved is
            // dropped outright rather than queued: the board it would be judged against is
            // mid-transition, so the group it found could already be stale.
            //
            // The guard lives here rather than in BoardInputHandler because whether input is
            // currently meaningful is a gameplay question. The input layer's job is to report
            // that a tap happened, not to decide whether the game cares.
            if (state != GameplayState.WaitingForInput)
            {
                return;
            }

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

            // Anything that is not a coloured block is not a selection: no group lookup
            // runs and no move begins.
            //
            // HasBlock rather than !IsEmpty. A crate cell is NOT empty -- it is occupied by
            // the crate -- so testing emptiness here would let a crate tap fall through to
            // cell.Block.Color and throw. Crates are destroyed by blasting NEXT to them,
            // never by tapping them.
            if (!cell.HasBlock)
            {
                string what = cell.HasObstacle ? cell.Obstacle.ToString() : "empty cell";
                Debug.Log($"Selected r{position.Row} c{position.Column} ({what} - not a block selection)");
                return;
            }

            Debug.Log($"Selected r{position.Row} c{position.Column} {cell.Block}");

            // Tapping a power-up IS the move. No minimum-group rule applies: the piece was
            // already earned by the group that created it.
            if (cell.Block.IsPowerUp)
            {
                state = GameplayState.ResolvingMove;
                StartCoroutine(ResolvePowerUpSequence(position));
                return;
            }

            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, position);
            Debug.Log($"Connected group size: {group.Count}");

            // The state changes only once the move is known to be real. An undersized group
            // touches nothing, so making it pass through ResolvingMove would advertise work
            // that never happens -- and at Milestone 14 it would block input for the length
            // of an animation that was never played.
            if (group.Count < minimumGroupSize)
            {
                Debug.Log($"Invalid group - no blocks removed (needs {minimumGroupSize})");
                return;
            }

            // The state is set here, synchronously, BEFORE the coroutine starts. A coroutine
            // does not begin executing until the next frame boundary, so leaving the guard
            // to the coroutine would open a window in which a second tap could start a
            // second move.
            state = GameplayState.ResolvingMove;
            StartCoroutine(ResolveMoveSequence(group, position));
        }

        // Only now, with the board settled and refilled, is the level's fate decided.
        // Shared by both kinds of move so the outcome rule exists in exactly one place.
        private void ConcludeMove()
        {
            state = LevelOutcome.Evaluate(goalTracker.AreAllGoalsComplete, MovesRemaining);

            if (state == GameplayState.Won)
            {
                Debug.Log("LEVEL WON");
                feedback?.PlayWin();
            }
            else if (state == GameplayState.Lost)
            {
                Debug.Log("LEVEL LOST");
                feedback?.PlayLose();
            }
        }

        // Tapping a Rocket or Bomb. The resolver owns the geometry; this owns the sequence.
        //
        // try/finally, not try/catch: C# forbids yield inside a try that has a catch, but
        // allows it with a finally. The finally only restores input if the sequence did NOT
        // reach its conclusion -- so an exception cannot leave the controller wedged in
        // ResolvingMove, while a Won or Lost that was legitimately decided is never
        // overwritten.
        private IEnumerator ResolvePowerUpSequence(BoardPosition position)
        {
            try
            {
                // Captured before activation destroys it, so the view can be pulsed.
                Block powerUp = board.GetCell(position).Block;

                // LOGIC FIRST, all of it, synchronously. The board is final from here on;
                // everything below is the view catching up.
                PowerUpActivationResult result = PowerUpResolver.Activate(board, position);

                MovesRemaining = Math.Max(0, MovesRemaining - 1);
                goalTracker.ProcessRemovedBlocks(result.RemovedBlocks);
                goalTracker.ProcessRemovedObstacles(result.RemovedObstacles);

                Debug.Log($"Activated power-up: removed {result.RemovedBlocks.Count} blocks, "
                    + $"{result.RemovedObstacles.Count} crates"
                    + $" | Moves remaining: {MovesRemaining} | {goalTracker}");

                // FEEDBACK for the piece the PLAYER activated. Other power-ups caught in the
                // footprint were removed, not fired, so they get no activation sound -- the
                // same distinction the no-chain rule makes in gameplay.
                if (feedback != null)
                {
                    // Explicit cases rather than "Rocket, else Bomb": a third power-up kind
                    // would otherwise silently play the bomb sound instead of being noticed.
                    switch (powerUp.Kind)
                    {
                        case BlockKind.Rocket:
                            feedback.PlayRocketActivation();
                            break;

                        case BlockKind.Bomb:
                            feedback.PlayBombActivation();
                            break;
                    }

                    feedback.PlayCrateBreak(result.RemovedObstacles.Count);
                }

                // VISUALS, in order. Each phase runs to completion before the next begins.
                yield return boardView.AnimateActivation(powerUp);
                yield return boardView.AnimateDestruction(result.RemovedBlocks, result.RemovedObstacles);

                yield return SettleBoard();

                // Only now, with nothing still moving on screen, is the level's fate shown.
                ConcludeMove();
            }
            finally
            {
                if (state == GameplayState.ResolvingMove)
                {
                    state = GameplayState.WaitingForInput;
                }
            }
        }

        // The whole of a valid normal move, in order. Each step is a call into a system that
        // owns that transition; this method owns only the sequence.
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

                // Crates are resolved BEFORE gravity, while the blasted cells are still where
                // they were.
                IReadOnlyList<ObstacleRemoval> crates = ObstacleResolver.ResolveAdjacentHits(board, group);
                goalTracker.ProcessRemovedObstacles(crates);

                // A large enough group earns a power-up on the cell the player TAPPED, which
                // the removal above has just emptied. A NEW Block instance, not one of the
                // destroyed ones promoted. Placed before gravity, so it falls like anything else.
                Block powerUp = PowerUpRules.TryCreatePowerUp(group, blastedColor);

                if (powerUp != null)
                {
                    board.SetBlock(selected, powerUp);
                }

                Debug.Log($"Removed {removed.Count} blocks, {crates.Count} crates"
                    + (powerUp != null ? $", created {powerUp} at r{selected.Row} c{selected.Column}" : string.Empty)
                    + $" | Moves remaining: {MovesRemaining} | {goalTracker}");

                // FEEDBACK. Fire-and-forget, alongside the animation rather than before it:
                // audio is non-blocking, so nothing here waits for a clip to finish.
                // One blast sound per player action regardless of group size, and one crate
                // sound for the whole batch however many crates broke.
                if (feedback != null)
                {
                    feedback.PlayBlast(removed.Count);
                    feedback.PlayCrateBreak(crates.Count);
                }

                // VISUALS. Blocks and crates die together, so a crate is gone from screen
                // before anything falls through the cell it occupied.
                yield return boardView.AnimateDestruction(removed, crates);

                if (powerUp != null)
                {
                    yield return boardView.AnimatePowerUpCreation(new BlockSpawn(powerUp, selected));
                }

                yield return SettleBoard();

                ConcludeMove();
            }
            finally
            {
                if (state == GameplayState.ResolvingMove)
                {
                    state = GameplayState.WaitingForInput;
                }
            }
        }

        // Gravity then refill, logic first and animation after in each case. Shared by both
        // kinds of move so the settling half of a turn exists in exactly one place.
        private IEnumerator SettleBoard()
        {
            IReadOnlyList<BlockMove> moves = GravityResolver.ApplyGravity(board);
            yield return boardView.AnimateMoves(moves);

            // A power-up created this turn falls as the SAME view: AnimateMoves repositions
            // existing views by Block identity and never recreates one.
            IReadOnlyList<BlockSpawn> spawns = RefillResolver.ApplyRefill(board, availableColors, random);
            yield return boardView.AnimateSpawns(spawns);
        }
    }
}
