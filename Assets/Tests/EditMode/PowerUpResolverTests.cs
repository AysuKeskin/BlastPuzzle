using System.Collections.Generic;
using System.Linq;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;
using BlastPuzzle.Goals;
using BlastPuzzle.Obstacles;
using BlastPuzzle.PowerUps;
using NUnit.Framework;

namespace BlastPuzzle.Tests.EditMode
{
    public sealed class RocketTests
    {
        private static BoardPosition At(int row, int column) => new BoardPosition(row, column);

        [Test]
        public void HorizontalRocket_RemovesBlocksAcrossEntireRow()
        {
            Board board = BoardLayout.Build(
                "B B B B",
                "R H G Y");

            PowerUpActivationResult result = PowerUpResolver.Activate(board, At(0, 1));

            Assert.That(result.RemovedBlockPositions,
                Is.EquivalentTo(new[] { At(0, 0), At(0, 1), At(0, 2), At(0, 3) }));

            for (int column = 0; column < 4; column++)
            {
                Assert.That(board.GetCell(0, column).IsEmpty, Is.True);
            }
        }

        [Test]
        public void VerticalRocket_RemovesBlocksAcrossEntireColumn()
        {
            Board board = BoardLayout.Build(
                "B R",
                "V R",
                "G R");

            PowerUpActivationResult result = PowerUpResolver.Activate(board, At(1, 0));

            Assert.That(result.RemovedBlockPositions,
                Is.EquivalentTo(new[] { At(0, 0), At(1, 0), At(2, 0) }));

            // Column 1 untouched.
            for (int row = 0; row < 3; row++)
            {
                Assert.That(board.GetCell(row, 1).HasBlock, Is.True);
            }
        }

        [Test]
        public void Rocket_HitsCrateInItsPath()
        {
            Board board = BoardLayout.Build("R H C G");

            PowerUpActivationResult result = PowerUpResolver.Activate(board, At(0, 1));

            Assert.That(result.RemovedObstacles, Has.Count.EqualTo(1));
            Assert.That(result.RemovedObstacles[0].Position, Is.EqualTo(At(0, 2)));
            Assert.That(board.GetCell(0, 2).HasObstacle, Is.False);
        }

        [Test]
        public void Rocket_ContinuesPastCrate()
        {
            // The crate at c2 must NOT stop the blast reaching c3 and c4.
            Board board = BoardLayout.Build("R H C G Y");

            PowerUpActivationResult result = PowerUpResolver.Activate(board, At(0, 1));

            Assert.That(result.RemovedBlockPositions,
                Is.EquivalentTo(new[] { At(0, 0), At(0, 1), At(0, 3), At(0, 4) }),
                "Every block in the row goes, on both sides of the crate.");
            Assert.That(board.GetCell(0, 4).IsEmpty, Is.True);
        }

    }

    public sealed class BombTests
    {
        private static BoardPosition At(int row, int column) => new BoardPosition(row, column);

        [Test]
        public void Bomb_RemovesBlocksInSquare()
        {
            Board board = BoardLayout.Build(
                "B B B B B",
                "B R R R B",
                "B R X R B",
                "B R R R B",
                "B B B B B");

            PowerUpActivationResult result = PowerUpResolver.Activate(board, At(2, 2));

            // The whole 3x3 square, diagonals included; the outer ring survives.
            Assert.That(result.RemovedBlockPositions, Is.EquivalentTo(new[]
            {
                At(1, 1), At(1, 2), At(1, 3),
                At(2, 1), At(2, 2), At(2, 3),
                At(3, 1), At(3, 2), At(3, 3)
            }));
            Assert.That(board.GetCell(0, 0).HasBlock, Is.True);
            Assert.That(board.GetCell(4, 2).HasBlock, Is.True);
        }

        [Test]
        public void BombAtCorner_ClampsToBoard()
        {
            Board board = BoardLayout.Build(
                "R R R",
                "R R R",
                "X R R");

            PowerUpActivationResult result = PowerUpResolver.Activate(board, At(0, 0));
            Assert.That(result.RemovedBlockPositions,
                Is.EquivalentTo(new[] { At(0, 0), At(1, 0), At(0, 1), At(1, 1) }),
                "A corner bomb affects the four cells of its square that are on the board.");
        }

        [Test]
        public void Bomb_HitsCratesInsideArea()
        {
            Board board = BoardLayout.Build(
                "R C R",
                "R X R",
                "R R C");

            PowerUpActivationResult result = PowerUpResolver.Activate(board, At(1, 1));

            Assert.That(result.RemovedObstacles, Has.Count.EqualTo(2),
                "Crates on an edge and on a corner of the bomb both break.");
        }
    }
    public sealed class PowerUpChainTests
    {
        private static BoardPosition At(int row, int column) => new BoardPosition(row, column);

        [Test]
        public void RocketSweepingABomb_SetsTheBombOff()
        {
            Board board = BoardLayout.Build(
                "B B B B B",
                "R H R X R",
                "B B B B B");

            PowerUpActivationResult result = PowerUpResolver.Activate(board, At(1, 1));

            // The rocket's own row is gone, as before.
            for (int column = 0; column < 5; column++)
            {
                Assert.That(board.GetCell(1, column).IsEmpty, Is.True, $"r1c{column}");
            }

            // ...and the bomb it swept went off, clearing its 3x3 square above and below.
            for (int column = 2; column <= 4; column++)
            {
                Assert.That(board.GetCell(2, column).HasBlock, Is.False, $"r2c{column}");
                Assert.That(board.GetCell(0, column).HasBlock, Is.False, $"r0c{column}");
            }

            // Outside the square nothing else goes.
            Assert.That(board.GetCell(2, 1).HasBlock, Is.True);

            Assert.That(result.RemovedBlockPositions, Has.Count.EqualTo(11),
                "Five in the rocket's row plus the six the bomb added.");
            Assert.That(result.ActivatedPowerUps, Has.Count.EqualTo(2),
                "The rocket the player tapped, and the bomb it set off.");
        }

        [Test]
        public void BombCatchingARocket_FiresTheWholeRow()
        {
            Board board = BoardLayout.Build(
                "B B B B B",
                "B R H B B",
                "B R X B B",
                "B B B B B");

            PowerUpActivationResult result = PowerUpResolver.Activate(board, At(1, 2));
            Assert.That(board.GetCell(2, 0).HasBlock, Is.False, "r2c0 is only reachable by the chained rocket");
            Assert.That(board.GetCell(2, 4).HasBlock, Is.False, "r2c4 is only reachable by the chained rocket");

            Assert.That(result.ActivatedPowerUps, Has.Count.EqualTo(2));
            Assert.That(result.ActivatedPowerUps[0].Kind, Is.EqualTo(BlockKind.Bomb),
                "The tapped piece fires first.");
            Assert.That(result.ActivatedPowerUps[1].Kind, Is.EqualTo(BlockKind.Rocket));
        }

        // The termination guarantee: two rockets that each reach the other.
        [Test]
        public void TwoRocketsInRangeOfEachOther_TerminateInsteadOfLooping()
        {
            //  H B H   both horizontal rockets share row 0
            Board board = BoardLayout.Build("H B H");

            PowerUpActivationResult result = PowerUpResolver.Activate(board, At(0, 0));

            Assert.That(result.ActivatedPowerUps, Has.Count.EqualTo(2),
                "Each rocket fires exactly once, however many times it is reached.");
            Assert.That(result.RemovedBlockPositions, Has.Count.EqualTo(3));
            Assert.That(result.RemovedBlockPositions.Distinct().Count(), Is.EqualTo(3));
        }

        [Test]
        public void CrossingBlasts_CountAsharedCrateOnlyOnce()
        {
            Board board = BoardLayout.Build(
                "B V B",
                "H C B",
                "B B B");

            PowerUpActivationResult result = PowerUpResolver.Activate(board, At(1, 0));

            Assert.That(result.RemovedObstacles.Count, Is.EqualTo(result.RemovedObstacles
                .Select(removal => removal.Position).Distinct().Count()),
                "A crate reached by two blasts must be reported once.");
        }

        [Test]
        public void AffectedPowerUpsAreRemovedExactlyOnce()
        {
            //  H X V    one rocket sweeping a row holding two other power-ups
            Board board = BoardLayout.Build("H X V");

            PowerUpActivationResult result = PowerUpResolver.Activate(board, At(0, 0));

            Assert.That(result.RemovedBlocks, Has.Count.EqualTo(3));
            Assert.That(result.RemovedBlockPositions.Distinct().Count(), Is.EqualTo(3),
                "No position may appear twice.");
            Assert.That(result.RemovedBlocks.Count(b => b.Kind == BlockKind.Bomb), Is.EqualTo(1));
            Assert.That(result.RemovedBlocks.Count(b => b.Kind == BlockKind.Rocket), Is.EqualTo(2));
        }
    }

    public sealed class PowerUpGoalTests
    {
        private static BoardPosition At(int row, int column) => new BoardPosition(row, column);

        [Test]
        public void RocketActivation_CountsOnlyNormalBlocksTowardColorGoals()
        {
            //  Row holds: 2 Red normal, the rocket itself, a bomb, 1 Blue normal
            Board board = BoardLayout.Build("R R H X B");

            var red = new ColorGoal(BlockColor.Red, 10);
            var blue = new ColorGoal(BlockColor.Blue, 10);
            var tracker = new GoalTracker(new[] { red, blue });

            PowerUpActivationResult result = PowerUpResolver.Activate(board, At(0, 2));
            tracker.ProcessRemovedBlocks(result.RemovedBlocks);

            Assert.That(result.RemovedBlocks, Has.Count.EqualTo(5), "All five pieces were removed...");
            Assert.That(red.CurrentCount, Is.EqualTo(2), "...but only the two normal Reds counted.");
            Assert.That(blue.CurrentCount, Is.EqualTo(1));
        }

        [Test]
        public void RemovingPowerUp_DoesNotAdvanceColorGoal()
        {
            // A lone rocket, tinted Red, on an otherwise empty row.
            Board board = BoardLayout.Build("H . .");

            var red = new ColorGoal(BlockColor.Red, 5);
            var tracker = new GoalTracker(new[] { red });

            PowerUpActivationResult result = PowerUpResolver.Activate(board, At(0, 0));
            tracker.ProcessRemovedBlocks(result.RemovedBlocks);

            Assert.That(result.RemovedBlocks, Has.Count.EqualTo(1));
            Assert.That(result.RemovedBlocks[0].Color, Is.EqualTo(BlockColor.Red), "It is tinted Red...");
            Assert.That(red.CurrentCount, Is.Zero, "...and still advances nothing.");
        }

        [Test]
        public void PowerUpDestroyedCrate_AdvancesCrateGoalOnce()
        {
            Board board = BoardLayout.Build("H C C");

            var crateGoal = new CrateGoal(5);
            var tracker = new GoalTracker(new[] { new ColorGoal(BlockColor.Red, 5) }, crateGoal);

            PowerUpActivationResult result = PowerUpResolver.Activate(board, At(0, 0));
            tracker.ProcessRemovedObstacles(result.RemovedObstacles);

            Assert.That(result.RemovedObstacles, Has.Count.EqualTo(2));
            Assert.That(crateGoal.CurrentCount, Is.EqualTo(2), "Two crates, two points -- once each.");
        }
    }
}
