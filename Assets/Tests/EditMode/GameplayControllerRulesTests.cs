using System.Reflection;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;
using BlastPuzzle.Gameplay;
using BlastPuzzle.Goals;
using NUnit.Framework;
using UnityEngine;

namespace BlastPuzzle.Tests.EditMode
{
    public sealed class GameplayControllerRulesTests
    {
        private GameObject host;
        private GameplayController controller;
        private ColorGoal blueGoal;

        private static BoardPosition At(int row, int column) => new BoardPosition(row, column);

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("GameplayControllerUnderTest");
            controller = host.AddComponent<GameplayController>();
            blueGoal = new ColorGoal(BlockColor.Blue, 10);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
        }

        private void Initialise(Board board, int moveLimit = 20)
        {
            controller.Initialise(
                board,
                new[] { BlockColor.Red, BlockColor.Blue },
                new System.Random(1),
                moveLimit,
                new GoalTracker(new[] { blueGoal }));
        }

        private static void ForceState(GameplayController target, GameplayState value) =>
            typeof(GameplayController)
                .GetField("state", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);

        [Test]
        public void Initialise_SetsStartingMovesAndClearsState()
        {
            Initialise(BoardLayout.Build("R R"), moveLimit: 15);

            Assert.That(controller.MovesRemaining, Is.EqualTo(15));
            Assert.That(controller.State, Is.EqualTo(GameplayState.WaitingForInput));
            Assert.That(controller.Goals.AreAllGoalsComplete, Is.False);
        }

        [Test]
        public void UnplayableInitialBoard_OffersRetryAndRejectsInput()
        {
            Initialise(BoardLayout.Build("R B"));
            Assert.That(controller.State, Is.EqualTo(GameplayState.Blocked));
            controller.HandleBlockSelected(At(0, 0));
            Assert.That(controller.MovesRemaining, Is.EqualTo(20));
            Assert.That(blueGoal.CurrentCount, Is.Zero);
        }

        [Test]
        public void Initialise_RejectsNonsenseMoveLimit()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Initialise(BoardLayout.Build("R R"), moveLimit: 0));
        }

        [Test]
        public void InvalidSelection_DoesNotConsumeMove()
        {
            Initialise(BoardLayout.Build(
                "R B",
                "B B"));

            controller.HandleBlockSelected(At(1, 0));

            Assert.That(controller.MovesRemaining, Is.EqualTo(20));
            Assert.That(blueGoal.CurrentCount, Is.Zero);
            Assert.That(controller.State, Is.EqualTo(GameplayState.WaitingForInput));
        }

        [Test]
        public void EmptySelection_DoesNotConsumeMove()
        {
            Initialise(BoardLayout.Build("R . R R"));

            controller.HandleBlockSelected(At(0, 1));

            Assert.That(controller.MovesRemaining, Is.EqualTo(20));
            Assert.That(blueGoal.CurrentCount, Is.Zero);
            Assert.That(controller.State, Is.EqualTo(GameplayState.WaitingForInput));
        }

        [Test]
        public void OutOfBoardSelection_DoesNotConsumeMove()
        {
            Initialise(BoardLayout.Build("R R"));

            controller.HandleBlockSelected(At(99, 99));

            Assert.That(controller.MovesRemaining, Is.EqualTo(20));
            Assert.That(controller.State, Is.EqualTo(GameplayState.WaitingForInput));
        }

        [Test]
        public void SelectingCrate_DoesNothingAndDoesNotThrow()
        {
            Initialise(BoardLayout.Build(
                "R R",
                "C B"));

            int moves = controller.MovesRemaining;

            Assert.DoesNotThrow(() => controller.HandleBlockSelected(At(0, 0)));
            Assert.That(controller.MovesRemaining, Is.EqualTo(moves), "Tapping a crate costs nothing.");
            Assert.That(controller.State, Is.EqualTo(GameplayState.WaitingForInput));
            Assert.That(controller.Goals.CrateGoal, Is.Null);
        }

        [Test]
        public void WonState_IgnoresPowerUpTap()
        {
            Board board = BoardLayout.Build(
                "H R",
                "B B");
            Initialise(board);
            ForceState(controller, GameplayState.Won);

            int moves = controller.MovesRemaining;
            controller.HandleBlockSelected(At(1, 0));

            Assert.That(controller.State, Is.EqualTo(GameplayState.Won));
            Assert.That(controller.MovesRemaining, Is.EqualTo(moves));
            Assert.That(board.GetCell(1, 0).HasBlock, Is.True, "The rocket was not activated.");
        }

        [Test]
        public void LostState_IgnoresPowerUpTap()
        {
            Board board = BoardLayout.Build(
                "X R",
                "B B");
            Initialise(board);
            ForceState(controller, GameplayState.Lost);

            int moves = controller.MovesRemaining;
            controller.HandleBlockSelected(At(1, 0));

            Assert.That(controller.State, Is.EqualTo(GameplayState.Lost));
            Assert.That(controller.MovesRemaining, Is.EqualTo(moves));
            Assert.That(board.GetCell(1, 0).HasBlock, Is.True, "The bomb was not activated.");
        }

        [Test]
        public void TappingPowerUpIsNotAnUndersizedGroup()
        {
            Board board = BoardLayout.Build(
                "H R",
                "B B");
            Initialise(board);

            Assert.That(ConnectedGroupFinder.FindConnectedGroup(board, At(1, 0)), Is.Empty,
                "The rocket forms no colour group at all...");
            Assert.That(board.GetCell(1, 0).Block.IsPowerUp, Is.True,
                "...so the controller must route it down the activation branch instead.");
        }

        [Test]
        public void WonState_IgnoresFurtherSelections()
        {
            Initialise(BoardLayout.Build(
                "R R",
                "B B"));
            ForceState(controller, GameplayState.Won);

            int moves = controller.MovesRemaining;
            controller.HandleBlockSelected(At(1, 0));

            Assert.That(controller.State, Is.EqualTo(GameplayState.Won));
            Assert.That(controller.MovesRemaining, Is.EqualTo(moves));
        }

        [Test]
        public void LostState_IgnoresFurtherSelections()
        {
            Initialise(BoardLayout.Build(
                "R R",
                "B B"));
            ForceState(controller, GameplayState.Lost);

            int moves = controller.MovesRemaining;
            controller.HandleBlockSelected(At(1, 0));

            Assert.That(controller.State, Is.EqualTo(GameplayState.Lost));
            Assert.That(controller.MovesRemaining, Is.EqualTo(moves));
        }
    }
}
