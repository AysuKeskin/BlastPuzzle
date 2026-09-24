using System.Reflection;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;
using BlastPuzzle.Gameplay;
using BlastPuzzle.Goals;
using NUnit.Framework;
using UnityEngine;

namespace BlastPuzzle.Tests.EditMode
{
    // Move and end-state rules that never reach the BoardView, so they need no scene.
    // The ones that DO resolve a full move live in the PlayMode suite.
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
        public void Initialise_RejectsNonsenseMoveLimit()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Initialise(BoardLayout.Build("R R"), moveLimit: 0));
        }

        [Test]
        public void InvalidSelection_DoesNotConsumeMove()
        {
            //  R B   the lone Red is a real group, but below the minimum size
            //  B B
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
            Initialise(BoardLayout.Build("R . R"));

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
            // Regression: a crate cell is NOT empty, so an IsEmpty check here let the tap
            // fall through to cell.Block.Color and threw a NullReferenceException.
            //  R R
            //  C B
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
            // A power-up tap is a valid move with no minimum-group rule, so only the
            // terminal state can stop it. No BoardView is wired: if the guard failed, the
            // activation pipeline would run and throw rather than pass quietly.
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
            // A lone rocket would be a "group of 1" under the normal rule. It must still be
            // activatable -- the minimum-group rule does not apply to power-ups. Here we only
            // assert it is not REJECTED as undersized; the full activation is covered in
            // PlayMode where a BoardView exists.
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
            // A genuinely valid group: without the terminal guard this WOULD blast. No
            // BoardView is wired, so a failed guard throws rather than passing quietly.
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
