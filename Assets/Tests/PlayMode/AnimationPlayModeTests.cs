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
    // Animation behaviour: sequencing, the input guard holding for the whole sequence, and
    // views landing exactly where the board says they are.
    //
    // No test asserts a duration or a frame count -- those are tuning values that should be
    // free to change. They assert what must be TRUE: input is rejected while resolving, the
    // outcome is published only after the visuals finish, and every view ends on its cell.
    public sealed class AnimationPlayModeTests
    {
        private const string GameplaySceneName = "Gameplay";

        private GameplayController controller;
        private BoardView boardView;
        private Board board;

        private IEnumerator LoadScene()
        {
            SceneManager.LoadScene(GameplaySceneName);
            yield return null;
            yield return null;

            controller = Object.FindFirstObjectByType<GameplayController>();
            boardView = Object.FindFirstObjectByType<BoardView>();

            FieldInfo field = typeof(GameplayController)
                .GetField("board", BindingFlags.NonPublic | BindingFlags.Instance);
            board = (Board)field.GetValue(controller);

            Assert.That(controller, Is.Not.Null);
            Assert.That(boardView, Is.Not.Null);
            Assert.That(board, Is.Not.Null);
        }

        private IEnumerator WaitUntilResolved(float timeoutSeconds = 5f)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;

            while (controller.State == GameplayState.ResolvingMove)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail($"Resolution did not finish within {timeoutSeconds}s.");
                }

                yield return null;
            }
        }

        private static GoalTracker UnreachableGoal() =>
            new GoalTracker(new[] { new ColorGoal(BlockColor.Blue, 9999) });

        private static IReadOnlyList<BlockColor> DemoColors() => new[]
        {
            BlockColor.Red, BlockColor.Blue, BlockColor.Green, BlockColor.Yellow, BlockColor.Purple
        };

        private BoardPosition FirstValidGroup()
        {
            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    var position = new BoardPosition(row, column);

                    if (!board.GetCell(position).HasBlock)
                    {
                        continue;
                    }

                    if (ConnectedGroupFinder.FindConnectedGroup(board, position).Count >= 2)
                    {
                        return position;
                    }
                }
            }

            return new BoardPosition(-1, -1);
        }

        private void PlaceBlock(BoardPosition position, Block block)
        {
            Cell cell = board.GetCell(position);

            if (cell.HasBlock)
            {
                boardView.RemoveViews(new[] { board.RemoveBlock(position) });
            }
            else if (cell.HasObstacle)
            {
                Obstacle crate = board.RemoveObstacle(position);
                boardView.RemoveObstacleViews(new[] { new ObstacleRemoval(position, crate) });
            }

            board.SetBlock(position, block);
            boardView.AddViews(new[] { new BlockSpawn(block, position) });
        }

        // Every view must sit at the cell its block actually occupies, with the transform
        // matching that cell's local position.
        private void AssertViewsAtExactPositions()
        {
            MethodInfo mapper = typeof(BoardView).GetMethod("BoardPositionToLocalPosition");
            var views = Object.FindObjectsByType<BlockView>(FindObjectsSortMode.None);

            foreach (BlockView view in views)
            {
                Cell cell = board.GetCell(view.Position);
                Assert.That(cell.HasBlock, Is.True, $"{view.name} sits on a cell with no block.");
                Assert.That(cell.Block, Is.SameAs(view.Block), $"{view.name} shows the wrong block.");

                var expected = (Vector3)mapper.Invoke(boardView, new object[] { view.Position });
                Assert.That(Vector3.Distance(view.transform.localPosition, expected), Is.LessThan(0.001f),
                    $"{view.name} stopped at {view.transform.localPosition}, expected {expected}.");
            }
        }

        [UnityTest]
        public IEnumerator SuccessfulMove_EventuallyReturnsToWaitingForInput()
        {
            yield return LoadScene();
            controller.Initialise(board, DemoColors(), new System.Random(1), 20, UnreachableGoal());

            controller.HandleBlockSelected(FirstValidGroup());

            // It must NOT resolve instantly any more -- the sequence spans frames.
            Assert.That(controller.State, Is.EqualTo(GameplayState.ResolvingMove),
                "The move should still be resolving immediately after the tap.");

            yield return WaitUntilResolved();
            Assert.That(controller.State, Is.EqualTo(GameplayState.WaitingForInput));
        }

        [UnityTest]
        public IEnumerator InputDuringAnimation_IsIgnored()
        {
            yield return LoadScene();
            controller.Initialise(board, DemoColors(), new System.Random(1), 20, UnreachableGoal());

            controller.HandleBlockSelected(FirstValidGroup());
            int movesAfterFirstTap = controller.MovesRemaining;

            // Hammer input across the whole sequence: removal, gravity and refill all get
            // taps. Every one must be swallowed by the guard.
            int rejected = 0;

            while (controller.State == GameplayState.ResolvingMove)
            {
                BoardPosition another = FirstValidGroup();

                if (another.Row >= 0)
                {
                    controller.HandleBlockSelected(another);
                    rejected++;
                }

                Assert.That(controller.MovesRemaining, Is.EqualTo(movesAfterFirstTap),
                    "A tap during resolution consumed a move.");
                yield return null;
            }

            Assert.That(rejected, Is.GreaterThan(0), "The test never actually tapped mid-animation.");
            Assert.That(controller.MovesRemaining, Is.EqualTo(movesAfterFirstTap),
                "Exactly one move was consumed despite repeated tapping.");
        }

        [UnityTest]
        public IEnumerator GravityAndRefillAnimation_EndAtExactBoardPositions()
        {
            yield return LoadScene();
            controller.Initialise(board, DemoColors(), new System.Random(1), 20, UnreachableGoal());

            for (int i = 0; i < 3; i++)
            {
                controller.HandleBlockSelected(FirstValidGroup());
                yield return WaitUntilResolved();

                AssertViewsAtExactPositions();
                Assert.That(boardView.ViewCount + boardView.CrateViewCount,
                    Is.EqualTo(board.Rows * board.Columns));
            }
        }

        [UnityTest]
        public IEnumerator RemovedViews_AreDestroyedAfterAnimation()
        {
            yield return LoadScene();
            controller.Initialise(board, DemoColors(), new System.Random(1), 20, UnreachableGoal());

            BoardPosition target = FirstValidGroup();
            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, target);
            var doomed = new List<Block>();

            foreach (BoardPosition position in group)
            {
                doomed.Add(board.GetCell(position).Block);
            }

            controller.HandleBlockSelected(target);

            // The mapping must be dropped IMMEDIATELY, before the animation finishes.
            foreach (Block block in doomed)
            {
                Assert.That(boardView.GetViewFor(block), Is.Null,
                    "A removed block is still queryable through BoardView.");
            }

            yield return WaitUntilResolved();

            var live = Object.FindObjectsByType<BlockView>(FindObjectsSortMode.None);
            foreach (BlockView view in live)
            {
                Assert.That(doomed, Has.No.Member(view.Block), "A destroyed block's view survived.");
            }
        }

        [UnityTest]
        public IEnumerator SurvivingBlockViewIdentity_IsPreservedThroughAnimatedGravity()
        {
            yield return LoadScene();
            controller.Initialise(board, DemoColors(), new System.Random(1), 20, UnreachableGoal());

            // Snapshot every block-to-view pair before the move.
            var before = new Dictionary<Block, BlockView>();

            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    Cell cell = board.GetCell(row, column);

                    if (cell.HasBlock)
                    {
                        before[cell.Block] = boardView.GetViewFor(cell.Block);
                    }
                }
            }

            controller.HandleBlockSelected(FirstValidGroup());
            yield return WaitUntilResolved();

            int survivors = 0;

            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    Cell cell = board.GetCell(row, column);

                    if (!cell.HasBlock || !before.TryGetValue(cell.Block, out BlockView original))
                    {
                        continue;
                    }

                    Assert.That(boardView.GetViewFor(cell.Block), Is.SameAs(original),
                        "A surviving block was given a different view by the animation.");
                    survivors++;
                }
            }

            Assert.That(survivors, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator PowerUpCreatedThenFalling_KeepsSameView()
        {
            yield return LoadScene();
            controller.Initialise(board, DemoColors(), new System.Random(1), 20, UnreachableGoal());

            // A run of five on a middle row, isolated above and below so the group is exact.
            const int row = 4;
            for (int column = 0; column < 5; column++)
            {
                PlaceBlock(new BoardPosition(row, column), Block.CreateNormal(BlockColor.Green));
            }

            for (int column = 0; column < board.Columns; column++)
            {
                PlaceBlock(new BoardPosition(row + 1, column), Block.CreateNormal(BlockColor.Yellow));
                if (column >= 5)
                {
                    PlaceBlock(new BoardPosition(row, column), Block.CreateNormal(BlockColor.Yellow));
                }
                PlaceBlock(new BoardPosition(row - 1, column), Block.CreateNormal(BlockColor.Yellow));
            }

            var tapped = new BoardPosition(row, 2);
            Assert.That(ConnectedGroupFinder.FindConnectedGroup(board, tapped).Count, Is.EqualTo(5));

            controller.HandleBlockSelected(tapped);
            yield return WaitUntilResolved();

            // Find the created power-up and confirm it has exactly one view, which follows it.
            Block powerUp = null;
            for (int r = 0; r < board.Rows && powerUp == null; r++)
            {
                for (int c = 0; c < board.Columns; c++)
                {
                    Cell cell = board.GetCell(r, c);
                    if (cell.HasBlock && cell.Block.IsPowerUp) { powerUp = cell.Block; break; }
                }
            }

            Assert.That(powerUp, Is.Not.Null, "The group of five should have created a power-up.");
            BlockView view = boardView.GetViewFor(powerUp);
            Assert.That(view, Is.Not.Null, "The power-up has no view.");
            Assert.That(view.Block, Is.SameAs(powerUp));
            AssertViewsAtExactPositions();
        }

        [UnityTest]
        public IEnumerator FinalMoveWin_EntersWonOnlyAfterResolutionCompletes()
        {
            yield return LoadScene();

            var at = new BoardPosition(1, 1);
            PlaceBlock(at, Block.CreateRocket(BlockColor.Red, RocketDirection.Horizontal));

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

            // The goal is already satisfied logically, but the level must NOT be declared won
            // while blocks are still visibly falling.
            Assert.That(goals.AreAllGoalsComplete, Is.True, "Goals complete immediately, logically...");
            Assert.That(controller.State, Is.EqualTo(GameplayState.ResolvingMove),
                "...but the state must still be resolving while the board settles.");

            yield return WaitUntilResolved();
            Assert.That(controller.State, Is.EqualTo(GameplayState.Won));
        }

        [UnityTest]
        public IEnumerator FinalMoveLoss_EntersLostOnlyAfterResolutionCompletes()
        {
            yield return LoadScene();
            controller.Initialise(board, DemoColors(), new System.Random(1), 1, UnreachableGoal());

            controller.HandleBlockSelected(FirstValidGroup());

            Assert.That(controller.MovesRemaining, Is.Zero, "The move is spent immediately...");
            Assert.That(controller.State, Is.EqualTo(GameplayState.ResolvingMove),
                "...but the loss is not declared until the board finishes settling.");

            yield return WaitUntilResolved();
            Assert.That(controller.State, Is.EqualTo(GameplayState.Lost));
        }

        [UnityTest]
        public IEnumerator CrateView_IsGoneBeforeGravityBegins()
        {
            yield return LoadScene();
            controller.Initialise(board, DemoColors(), new System.Random(1), 20, UnreachableGoal());

            // A crate with a blastable pair directly beneath it.
            var crateAt = new BoardPosition(3, 3);
            Cell crateCell = board.GetCell(crateAt);

            if (!crateCell.HasObstacle)
            {
                if (crateCell.HasBlock)
                {
                    boardView.RemoveViews(new[] { board.RemoveBlock(crateAt) });
                }

                board.PlaceObstacle(crateAt, new Obstacle(ObstacleType.Crate));
                boardView.Build(board);
            }

            PlaceBlock(new BoardPosition(2, 3), Block.CreateNormal(BlockColor.Purple));
            PlaceBlock(new BoardPosition(2, 4), Block.CreateNormal(BlockColor.Purple));

            int cratesBefore = boardView.CrateViewCount;
            controller.HandleBlockSelected(new BoardPosition(2, 3));

            // Watch: the crate's view must disappear before any block is seen to have moved
            // into the freed cell.
            bool crateGoneObserved = false;

            while (controller.State == GameplayState.ResolvingMove)
            {
                if (boardView.CrateViewCount < cratesBefore)
                {
                    crateGoneObserved = true;
                }

                if (!board.GetCell(crateAt).IsEmpty && board.GetCell(crateAt).HasBlock)
                {
                    Assert.That(crateGoneObserved, Is.True,
                        "A block occupied the crate's cell before the crate view had gone.");
                }

                yield return null;
            }

            Assert.That(boardView.CrateViewCount, Is.LessThan(cratesBefore), "The crate was destroyed.");
        }
    }
}
