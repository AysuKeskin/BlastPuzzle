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
    // These assert that the feedback layer was ASKED for the right thing, the right number
    // of times. They do NOT assert that a speaker moved or that a phone buzzed -- an Editor
    // test cannot observe either, and pretending otherwise would be a test that passes while
    // the device is silent.
    //
    // Haptics specifically: HapticRequestCount records the DECISION to vibrate. Whether the
    // platform actually vibrates needs a physical device.
    public sealed class FeedbackPlayModeTests
    {
        private const string GameplaySceneName = "Gameplay";

        private GameplayController controller;
        private BoardView boardView;
        private GameFeedback feedback;
        private Board board;

        private IEnumerator LoadScene()
        {
            SceneManager.LoadScene(GameplaySceneName);
            yield return null;
            yield return null;

            controller = Object.FindFirstObjectByType<GameplayController>();
            boardView = Object.FindFirstObjectByType<BoardView>();
            feedback = Object.FindFirstObjectByType<GameFeedback>();

            Assert.That(controller, Is.Not.Null);
            Assert.That(boardView, Is.Not.Null);
            Assert.That(feedback, Is.Not.Null, "The scene has no GameFeedback component.");

            board = (Board)typeof(GameplayController)
                .GetField("board", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(controller);

            controller.Initialise(board, DemoColors(), new System.Random(1), 20, UnreachableGoal());
            feedback.ResetCounters();
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

        private IEnumerator Tap(BoardPosition position)
        {
            controller.HandleBlockSelected(position);
            yield return WaitUntilResolved();
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

                    if (board.GetCell(position).HasBlock &&
                        ConnectedGroupFinder.FindConnectedGroup(board, position).Count >= 2)
                    {
                        return position;
                    }
                }
            }

            return new BoardPosition(-1, -1);
        }

        private BoardPosition IsolatedBlock()
        {
            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    var position = new BoardPosition(row, column);

                    if (board.GetCell(position).HasBlock &&
                        ConnectedGroupFinder.FindConnectedGroup(board, position).Count == 1)
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

        [UnityTest]
        public IEnumerator ValidBlast_RequestsBlastFeedbackOnce()
        {
            yield return LoadScene();

            BoardPosition target = FirstValidGroup();
            int groupSize = ConnectedGroupFinder.FindConnectedGroup(board, target).Count;
            Assert.That(groupSize, Is.GreaterThan(1));

            yield return Tap(target);

            // One sound for the whole action, however many blocks were destroyed.
            Assert.That(feedback.BlastCount, Is.EqualTo(1),
                $"{groupSize} blocks were removed but the blast must be requested once.");
        }

        [UnityTest]
        public IEnumerator InvalidTap_RequestsNoBlastFeedback()
        {
            yield return LoadScene();

            BoardPosition isolated = IsolatedBlock();

            if (isolated.Row < 0)
            {
                Assert.Ignore("No isolated block on this board.");
            }

            yield return Tap(isolated);

            Assert.That(feedback.BlastCount, Is.Zero);
            Assert.That(feedback.HapticRequestCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator DestroyedCrates_RequestCrateFeedbackOncePerBatch()
        {
            yield return LoadScene();

            // Two crates side by side, with a blastable pair touching both.
            PlaceBlock(new BoardPosition(2, 2), Block.CreateNormal(BlockColor.Purple));
            PlaceBlock(new BoardPosition(2, 3), Block.CreateNormal(BlockColor.Purple));

            foreach (var at in new[] { new BoardPosition(3, 2), new BoardPosition(3, 3) })
            {
                Cell cell = board.GetCell(at);

                if (cell.HasBlock)
                {
                    boardView.RemoveViews(new[] { board.RemoveBlock(at) });
                }

                if (!cell.HasObstacle)
                {
                    board.PlaceObstacle(at, new Obstacle(ObstacleType.Crate));
                }
            }

            boardView.Build(board);
            feedback.ResetCounters();

            yield return Tap(new BoardPosition(2, 2));

            Assert.That(feedback.BlastCount, Is.EqualTo(1));
            Assert.That(feedback.CrateBreakCount, Is.EqualTo(1),
                "Two crates broke, but that is one batch and therefore one sound.");
        }

        [UnityTest]
        public IEnumerator RocketActivation_RequestsRocketFeedback()
        {
            yield return LoadScene();

            var at = new BoardPosition(2, 2);
            PlaceBlock(at, Block.CreateRocket(BlockColor.Red, RocketDirection.Horizontal));
            feedback.ResetCounters();

            yield return Tap(at);

            Assert.That(feedback.RocketActivationCount, Is.EqualTo(1));
            Assert.That(feedback.BombActivationCount, Is.Zero);
            Assert.That(feedback.HapticRequestCount, Is.GreaterThan(0),
                "A power-up activation requests a haptic.");
        }

        [UnityTest]
        public IEnumerator BombActivation_RequestsBombFeedback()
        {
            yield return LoadScene();

            var at = new BoardPosition(3, 3);
            PlaceBlock(at, Block.CreateBomb(BlockColor.Red));
            feedback.ResetCounters();

            yield return Tap(at);

            Assert.That(feedback.BombActivationCount, Is.EqualTo(1));
            Assert.That(feedback.RocketActivationCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator AffectedButNotActivatedBomb_DoesNotRequestBombActivationFeedback()
        {
            yield return LoadScene();

            // A horizontal rocket that will sweep a bomb sitting in the same row.
            const int row = 4;
            var rocketAt = new BoardPosition(row, 0);
            PlaceBlock(rocketAt, Block.CreateRocket(BlockColor.Blue, RocketDirection.Horizontal));
            PlaceBlock(new BoardPosition(row, 5), Block.CreateBomb(BlockColor.Purple));
            feedback.ResetCounters();

            yield return Tap(rocketAt);

            Assert.That(feedback.RocketActivationCount, Is.EqualTo(1), "The tapped rocket fired...");
            Assert.That(feedback.BombActivationCount, Is.Zero,
                "...and the bomb it destroyed was removed, not activated, so it stays silent.");
        }

        [UnityTest]
        public IEnumerator AffectedButNotActivatedRocket_DoesNotRequestRocketActivationFeedback()
        {
            yield return LoadScene();

            // A bomb whose plus-shaped footprint catches a rocket directly above it.
            var bombAt = new BoardPosition(3, 3);
            PlaceBlock(bombAt, Block.CreateBomb(BlockColor.Red));
            PlaceBlock(new BoardPosition(4, 3), Block.CreateRocket(BlockColor.Green, RocketDirection.Horizontal));
            feedback.ResetCounters();

            yield return Tap(bombAt);

            Assert.That(feedback.BombActivationCount, Is.EqualTo(1));
            Assert.That(feedback.RocketActivationCount, Is.Zero,
                "The rocket inside the blast was destroyed, not fired.");
        }

        [UnityTest]
        public IEnumerator Win_RequestsWinFeedbackOnlyAfterResolution()
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
            controller.Initialise(board, DemoColors(), new System.Random(1), 5, goals);
            feedback.ResetCounters();

            controller.HandleBlockSelected(at);

            // The goal is satisfied logically at once, but the fanfare must wait for the
            // board to finish settling.
            Assert.That(goals.AreAllGoalsComplete, Is.True);
            Assert.That(feedback.WinCount, Is.Zero, "Win feedback fired before the board settled.");

            yield return WaitUntilResolved();

            Assert.That(controller.State, Is.EqualTo(GameplayState.Won));
            Assert.That(feedback.WinCount, Is.EqualTo(1));
            Assert.That(feedback.LoseCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Lost_RequestsLoseFeedbackOnlyAfterResolution()
        {
            yield return LoadScene();
            controller.Initialise(board, DemoColors(), new System.Random(1), 1, UnreachableGoal());
            feedback.ResetCounters();

            controller.HandleBlockSelected(FirstValidGroup());

            Assert.That(controller.MovesRemaining, Is.Zero, "The move is spent at once...");
            Assert.That(feedback.LoseCount, Is.Zero, "...but the lose sound waits for the visuals.");

            yield return WaitUntilResolved();

            Assert.That(controller.State, Is.EqualTo(GameplayState.Lost));
            Assert.That(feedback.LoseCount, Is.EqualTo(1));
            Assert.That(feedback.WinCount, Is.Zero);
        }
    }
}
