using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;
using BlastPuzzle.Gameplay;
using BlastPuzzle.Goals;
using BlastPuzzle.Obstacles;
using BlastPuzzle.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BlastPuzzle.Tests.PlayMode
{
    // These rules need a wired BoardView and block prefab, because a valid move destroys and
    // creates GameObjects. BoardView.RemoveViews calls Destroy, which is invalid in edit
    // mode, so they cannot be EditMode tests without distorting production code. Loading the
    // real scene gives a genuinely wired controller rather than a hand-assembled stand-in.
    //
    // The pure rules -- goal arithmetic and the win-before-loss ordering -- are covered by
    // fast EditMode tests instead; only what truly needs a scene lives here.
    public sealed class LevelRulesPlayModeTests
    {
        private const string GameplaySceneName = "Gameplay";

        private GameplayController controller;
        private BoardView boardView;
        private Board board;

        private IEnumerator LoadScene()
        {
            SceneManager.LoadScene(GameplaySceneName);

            // One frame to load, one more so GameBootstrap.Start has run.
            yield return null;
            yield return null;

            controller = Object.FindFirstObjectByType<GameplayController>();
            boardView = Object.FindFirstObjectByType<BoardView>();

            Assert.That(controller, Is.Not.Null, "GameplayController missing from the scene.");
            Assert.That(boardView, Is.Not.Null, "BoardView missing from the scene.");

            // Test-only reach for the board, so group selection can use the production
            // ConnectedGroupFinder instead of re-implementing BFS in test code.
            FieldInfo field = typeof(GameplayController)
                .GetField("board", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "GameplayController.board was renamed; update this seam.");
            board = (Board)field.GetValue(controller);
            Assert.That(board, Is.Not.Null);
        }

        // The largest connected group currently on the board, using production code.
        private BoardPosition LargestGroup(out int size, out BlockColor color)
        {
            var best = new BoardPosition(0, 0);
            size = 0;
            color = BlockColor.Red;

            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    var position = new BoardPosition(row, column);
                    Cell cell = board.GetCell(position);

                    if (cell.IsEmpty)
                    {
                        continue;
                    }

                    int count = ConnectedGroupFinder.FindConnectedGroup(board, position).Count;

                    if (count > size)
                    {
                        size = count;
                        best = position;
                        color = cell.Block.Color;
                    }
                }
            }

            return best;
        }

        private BoardPosition IsolatedBlock()
        {
            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    var position = new BoardPosition(row, column);

                    if (board.GetCell(position).IsEmpty)
                    {
                        continue;
                    }

                    if (ConnectedGroupFinder.FindConnectedGroup(board, position).Count == 1)
                    {
                        return position;
                    }
                }
            }

            return new BoardPosition(-1, -1);
        }

        private static GoalTracker UnreachableGoal() =>
            new GoalTracker(new[] { new ColorGoal(BlockColor.Blue, 9999) });

        [UnityTest]
        public IEnumerator ValidMove_ConsumesExactlyOneMove()
        {
            yield return LoadScene();
            controller.Initialise(board, DemoColors(), new System.Random(1), 20, UnreachableGoal());

            int before = controller.MovesRemaining;
            controller.HandleBlockSelected(LargestGroup(out _, out _));

            Assert.That(controller.MovesRemaining, Is.EqualTo(before - 1));
            Assert.That(controller.State, Is.EqualTo(GameplayState.WaitingForInput));
            // Block views PLUS crate views account for every cell. A level with crates has
            // fewer block views than cells, and destroying a crate frees its cell for
            // refill -- so asserting ViewCount alone would depend on which level the scene
            // happens to reference.
            Assert.That(boardView.ViewCount + boardView.CrateViewCount,
                Is.EqualTo(board.Rows * board.Columns),
                "Every cell must hold either a block or a crate after refill.");
        }

        [UnityTest]
        public IEnumerator SeveralValidMoves_ConsumeOneEach()
        {
            yield return LoadScene();
            controller.Initialise(board, DemoColors(), new System.Random(1), 20, UnreachableGoal());

            for (int expected = 19; expected >= 17; expected--)
            {
                controller.HandleBlockSelected(LargestGroup(out _, out _));
                Assert.That(controller.MovesRemaining, Is.EqualTo(expected));
                Assert.That(boardView.ViewCount + boardView.CrateViewCount,
                    Is.EqualTo(board.Rows * board.Columns));
            }
        }

        [UnityTest]
        public IEnumerator InvalidSelection_DoesNotConsumeMove()
        {
            yield return LoadScene();
            controller.Initialise(board, DemoColors(), new System.Random(1), 20, UnreachableGoal());

            BoardPosition isolated = IsolatedBlock();

            if (isolated.Row < 0)
            {
                Assert.Ignore("This board happens to contain no isolated block.");
            }

            int before = controller.MovesRemaining;
            controller.HandleBlockSelected(isolated);

            Assert.That(controller.MovesRemaining, Is.EqualTo(before));
            Assert.That(controller.State, Is.EqualTo(GameplayState.WaitingForInput));
        }

        [UnityTest]
        public IEnumerator FinalMoveWithoutGoalCompletion_ResultsInLoss()
        {
            yield return LoadScene();
            controller.Initialise(board, DemoColors(), new System.Random(1), 1, UnreachableGoal());

            controller.HandleBlockSelected(LargestGroup(out _, out _));

            Assert.That(controller.MovesRemaining, Is.Zero);
            Assert.That(controller.State, Is.EqualTo(GameplayState.Lost));
        }

        [UnityTest]
        public IEnumerator FinalMoveCompletingGoal_ResultsInWin()
        {
            yield return LoadScene();

            // Target exactly the group about to be blasted, on the last available move, so
            // goals complete and moves hit zero in the same instant.
            BoardPosition target = LargestGroup(out int size, out BlockColor color);
            var goals = new GoalTracker(new[] { new ColorGoal(color, size) });
            controller.Initialise(board, DemoColors(), new System.Random(1), 1, goals);

            controller.HandleBlockSelected(target);

            Assert.That(controller.MovesRemaining, Is.Zero, "Moves are exhausted...");
            Assert.That(goals.AreAllGoalsComplete, Is.True, "...and the goal completed on that same move.");
            Assert.That(controller.State, Is.EqualTo(GameplayState.Won),
                "Winning on the final move must beat running out of moves.");
        }

        [UnityTest]
        public IEnumerator WinningBeforeMovesReachZero_ResultsInWin()
        {
            yield return LoadScene();

            BoardPosition target = LargestGroup(out int size, out BlockColor color);
            var goals = new GoalTracker(new[] { new ColorGoal(color, size) });
            controller.Initialise(board, DemoColors(), new System.Random(1), 5, goals);

            controller.HandleBlockSelected(target);

            Assert.That(controller.State, Is.EqualTo(GameplayState.Won));
            Assert.That(controller.MovesRemaining, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator WonState_IgnoresFurtherSelections()
        {
            yield return LoadScene();

            BoardPosition target = LargestGroup(out int size, out BlockColor color);
            controller.Initialise(board, DemoColors(), new System.Random(1), 5,
                new GoalTracker(new[] { new ColorGoal(color, size) }));
            controller.HandleBlockSelected(target);
            Assert.That(controller.State, Is.EqualTo(GameplayState.Won));

            int movesAfterWin = controller.MovesRemaining;
            int viewsAfterWin = boardView.ViewCount;

            controller.HandleBlockSelected(LargestGroup(out _, out _));

            Assert.That(controller.State, Is.EqualTo(GameplayState.Won), "A won level stays won.");
            Assert.That(controller.MovesRemaining, Is.EqualTo(movesAfterWin));
            Assert.That(boardView.ViewCount, Is.EqualTo(viewsAfterWin));
        }

        [UnityTest]
        public IEnumerator LostState_IgnoresFurtherSelections()
        {
            yield return LoadScene();
            controller.Initialise(board, DemoColors(), new System.Random(1), 1, UnreachableGoal());

            controller.HandleBlockSelected(LargestGroup(out _, out _));
            Assert.That(controller.State, Is.EqualTo(GameplayState.Lost));

            int viewsAfterLoss = boardView.ViewCount;
            controller.HandleBlockSelected(LargestGroup(out _, out _));

            Assert.That(controller.State, Is.EqualTo(GameplayState.Lost), "A lost level stays lost.");
            Assert.That(controller.MovesRemaining, Is.Zero, "Moves can never go below zero.");
            Assert.That(boardView.ViewCount, Is.EqualTo(viewsAfterLoss));
        }


        // Replaces whatever is at a position with the given block, keeping the view in step
        // by using the same production APIs gameplay uses. This is how a controlled
        // power-up is placed on a randomly generated board.
        private void PlaceBlock(BoardPosition position, Block block)
        {
            Cell cell = board.GetCell(position);

            if (cell.HasBlock)
            {
                Block old = board.RemoveBlock(position);
                boardView.RemoveViews(new[] { old });
            }
            else if (cell.HasObstacle)
            {
                // The scene's level may have placed a crate here. Board refuses to put a
                // block on one -- correctly -- so clear it first.
                Obstacle crate = board.RemoveObstacle(position);
                boardView.RemoveObstacleViews(new[] { new ObstacleRemoval(position, crate) });
            }

            board.SetBlock(position, block);
            boardView.AddViews(new[] { new BlockSpawn(block, position) });
        }

        // Five in a row of one colour, so the next tap earns a Rocket.
        private BoardPosition MakeRocketSizedGroup(int row, BlockColor color)
        {
            for (int column = 0; column < PowerUps.PowerUpRules.RocketThreshold; column++)
            {
                PlaceBlock(new BoardPosition(row, column), Block.CreateNormal(color));
            }

            // Break the run on both sides so the group is exactly the length we made.
            BlockColor other = color == BlockColor.Red ? BlockColor.Blue : BlockColor.Red;
            int after = PowerUps.PowerUpRules.RocketThreshold;
            if (after < board.Columns)
            {
                PlaceBlock(new BoardPosition(row, after), Block.CreateNormal(other));
            }

            return new BoardPosition(row, 0);
        }

        [UnityTest]
        public IEnumerator CreatingPowerUp_ConsumesExactlyOneMove()
        {
            yield return LoadScene();
            controller.Initialise(board, DemoColors(), new System.Random(1), 20, UnreachableGoal());

            BoardPosition tapped = MakeRocketSizedGroup(0, BlockColor.Green);
            int before = controller.MovesRemaining;

            controller.HandleBlockSelected(tapped);

            Assert.That(controller.MovesRemaining, Is.EqualTo(before - 1));
            Assert.That(boardView.ViewCount + boardView.CrateViewCount,
                Is.EqualTo(board.Rows * board.Columns));
        }

        [UnityTest]
        public IEnumerator ActivatingRocket_ConsumesExactlyOneMove()
        {
            yield return LoadScene();
            controller.Initialise(board, DemoColors(), new System.Random(1), 20, UnreachableGoal());

            var at = new BoardPosition(2, 2);
            PlaceBlock(at, Block.CreateRocket(BlockColor.Red, RocketDirection.Horizontal));
            int before = controller.MovesRemaining;

            controller.HandleBlockSelected(at);

            Assert.That(controller.MovesRemaining, Is.EqualTo(before - 1));
            Assert.That(controller.State, Is.EqualTo(GameplayState.WaitingForInput));
            Assert.That(boardView.ViewCount + boardView.CrateViewCount,
                Is.EqualTo(board.Rows * board.Columns), "Refill restored a full board.");
        }

        [UnityTest]
        public IEnumerator ActivatingBomb_ConsumesExactlyOneMove()
        {
            yield return LoadScene();
            controller.Initialise(board, DemoColors(), new System.Random(1), 20, UnreachableGoal());

            var at = new BoardPosition(3, 3);
            PlaceBlock(at, Block.CreateBomb(BlockColor.Red));
            int before = controller.MovesRemaining;

            controller.HandleBlockSelected(at);

            Assert.That(controller.MovesRemaining, Is.EqualTo(before - 1));
            Assert.That(boardView.ViewCount + boardView.CrateViewCount,
                Is.EqualTo(board.Rows * board.Columns));
        }

        [UnityTest]
        public IEnumerator PowerUpActivation_ReturnsToWaitingForInputWhenLevelContinues()
        {
            yield return LoadScene();
            controller.Initialise(board, DemoColors(), new System.Random(1), 20, UnreachableGoal());

            var at = new BoardPosition(4, 4);
            PlaceBlock(at, Block.CreateRocket(BlockColor.Blue, RocketDirection.Vertical));

            controller.HandleBlockSelected(at);

            Assert.That(controller.State, Is.EqualTo(GameplayState.WaitingForInput));
            Assert.That(controller.MovesRemaining, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator FinalMovePowerUpCanWinLevel()
        {
            yield return LoadScene();

            // One move left, and a goal the rocket's row will satisfy exactly.
            var at = new BoardPosition(1, 1);
            PlaceBlock(at, Block.CreateRocket(BlockColor.Red, RocketDirection.Horizontal));

            // Fill the rest of that row with Green so the sweep destroys a known count.
            int greens = 0;
            for (int column = 0; column < board.Columns; column++)
            {
                if (column == at.Column) continue;
                PlaceBlock(new BoardPosition(at.Row, column), Block.CreateNormal(BlockColor.Green));
                greens++;
            }

            var goals = new GoalTracker(new[] { new ColorGoal(BlockColor.Green, greens) });
            controller.Initialise(board, DemoColors(), new System.Random(1), 1, goals);

            controller.HandleBlockSelected(at);

            Assert.That(controller.MovesRemaining, Is.Zero, "Moves exhausted...");
            Assert.That(goals.AreAllGoalsComplete, Is.True, "...and the goal completed on that move.");
            Assert.That(controller.State, Is.EqualTo(GameplayState.Won),
                "The rocket itself must not have counted toward the Green goal.");
        }

        private static IReadOnlyList<BlockColor> DemoColors() => new[]
        {
            BlockColor.Red, BlockColor.Blue, BlockColor.Green, BlockColor.Yellow, BlockColor.Purple
        };
    }
}
