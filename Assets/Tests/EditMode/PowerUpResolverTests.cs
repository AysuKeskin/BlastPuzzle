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
    // Layout legend: '.' empty, 'C' crate, 'H' horizontal rocket, 'V' vertical rocket,
    // 'X' bomb, letters are normal colours. TOP ROW FIRST; row 0 is the bottom.
    public sealed class RocketTests
    {
        private static BoardPosition At(int row, int column) => new BoardPosition(row, column);

        [Test]
        public void HorizontalRocket_RemovesBlocksAcrossEntireRow()
        {
            //  B B B B
            //  R H G Y    <- rocket on row 0
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
            //  B R
            //  V R     <- vertical rocket in column 0
            //  G R
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
        public void Rocket_RemovesItself()
        {
            Board board = BoardLayout.Build("R H G");

            PowerUpActivationResult result = PowerUpResolver.Activate(board, At(0, 1));

            Assert.That(board.GetCell(0, 1).IsEmpty, Is.True);
            Assert.That(result.RemovedBlocks.Any(b => b.Kind == BlockKind.Rocket), Is.True,
                "The activated rocket is itself among the removed blocks.");
        }

        [Test]
        public void Rocket_DoesNotAffectOtherRowsOrColumns()
        {
            //  B B B
            //  R H G   <- only this row clears
            //  Y Y Y
            Board board = BoardLayout.Build(
                "B B B",
                "R H G",
                "Y Y Y");

            PowerUpResolver.Activate(board, At(1, 1));

            for (int column = 0; column < 3; column++)
            {
                Assert.That(board.GetCell(2, column).HasBlock, Is.True, "Top row survives.");
                Assert.That(board.GetCell(0, column).HasBlock, Is.True, "Bottom row survives.");
                Assert.That(board.GetCell(1, column).IsEmpty, Is.True, "Rocket row cleared.");
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

        [Test]
        public void RocketAtBoardEdge_Works()
        {
            //  Rocket in the very first column, vertical: clears its column and nothing else.
            Board board = BoardLayout.Build(
                "V R",
                "G Y");

            PowerUpActivationResult result = PowerUpResolver.Activate(board, At(1, 0));

            Assert.That(result.RemovedBlockPositions, Is.EquivalentTo(new[] { At(1, 0), At(0, 0) }));
            Assert.That(board.GetCell(1, 1).HasBlock, Is.True);
        }
    }

    public sealed class BombTests
    {
        private static BoardPosition At(int row, int column) => new BoardPosition(row, column);

        [Test]
        public void Bomb_RemovesBlocksIn3x3Area()
        {
            //  B B B B B
            //  B R R R B
            //  B R X R B   <- bomb at centre (r2,c2)
            //  B R R R B
            //  B B B B B
            Board board = BoardLayout.Build(
                "B B B B B",
                "B R R R B",
                "B R X R B",
                "B R R R B",
                "B B B B B");

            PowerUpActivationResult result = PowerUpResolver.Activate(board, At(2, 2));

            Assert.That(result.RemovedBlockPositions, Has.Count.EqualTo(9));
            for (int row = 1; row <= 3; row++)
            {
                for (int column = 1; column <= 3; column++)
                {
                    Assert.That(board.GetCell(row, column).IsEmpty, Is.True, $"r{row}c{column} should be cleared.");
                }
            }
        }

        [Test]
        public void Bomb_RemovesItself()
        {
            Board board = BoardLayout.Build(
                "R R R",
                "R X R",
                "R R R");

            PowerUpActivationResult result = PowerUpResolver.Activate(board, At(1, 1));

            Assert.That(board.GetCell(1, 1).IsEmpty, Is.True);
            Assert.That(result.RemovedBlocks.Any(b => b.Kind == BlockKind.Bomb), Is.True);
        }

        [Test]
        public void Bomb_DoesNotAffectCellsOutsideRadius()
        {
            Board board = BoardLayout.Build(
                "B B B B B",
                "B R R R B",
                "B R X R B",
                "B R R R B",
                "B B B B B");

            PowerUpResolver.Activate(board, At(2, 2));

            // The whole outer ring survives.
            for (int i = 0; i < 5; i++)
            {
                Assert.That(board.GetCell(4, i).HasBlock, Is.True);
                Assert.That(board.GetCell(0, i).HasBlock, Is.True);
                Assert.That(board.GetCell(i, 0).HasBlock, Is.True);
                Assert.That(board.GetCell(i, 4).HasBlock, Is.True);
            }
        }

        [Test]
        public void BombAtCorner_ClampsToBoard()
        {
            //  R R R
            //  R R R
            //  X R R    <- bomb at the bottom-left corner
            Board board = BoardLayout.Build(
                "R R R",
                "R R R",
                "X R R");

            PowerUpActivationResult result = PowerUpResolver.Activate(board, At(0, 0));

            Assert.That(result.RemovedBlockPositions,
                Is.EquivalentTo(new[] { At(0, 0), At(0, 1), At(1, 0), At(1, 1) }),
                "A corner bomb affects four cells, not nine.");
        }

        [Test]
        public void BombAtEdge_ClampsToBoard()
        {
            //  R R R
            //  X R R    <- bomb on the left edge, middle row
            //  R R R
            Board board = BoardLayout.Build(
                "R R R",
                "X R R",
                "R R R");

            PowerUpActivationResult result = PowerUpResolver.Activate(board, At(1, 0));

            Assert.That(result.RemovedBlockPositions, Has.Count.EqualTo(6),
                "An edge bomb affects six cells.");
        }

        [Test]
        public void Bomb_HitsCratesInsideArea()
        {
            //  R C R
            //  R X R
            //  R R C     the bottom-right crate is inside the 3x3 too
            Board board = BoardLayout.Build(
                "R C R",
                "R X R",
                "R R C");

            PowerUpActivationResult result = PowerUpResolver.Activate(board, At(1, 1));

            Assert.That(result.RemovedObstacles, Has.Count.EqualTo(2));
            Assert.That(result.RemovedObstacles.Select(o => o.Position),
                Is.EquivalentTo(new[] { At(2, 1), At(0, 2) }));
        }
    }

    public sealed class PowerUpNoChainTests
    {
        private static BoardPosition At(int row, int column) => new BoardPosition(row, column);

        [Test]
        public void RocketRemovingBomb_DoesNotActivateBomb()
        {
            //  B B B B B
            //  R H R X R   <- rocket on row 1 will sweep across the bomb at c3
            //  B B B B B
            Board board = BoardLayout.Build(
                "B B B B B",
                "R H R X R",
                "B B B B B");

            PowerUpActivationResult result = PowerUpResolver.Activate(board, At(1, 1));

            // The bomb is gone as a piece...
            Assert.That(board.GetCell(1, 3).IsEmpty, Is.True);
            Assert.That(result.RemovedBlocks.Any(b => b.Kind == BlockKind.Bomb), Is.True);

            // ...but its 3x3 never fired: the rows above and below are untouched.
            for (int column = 0; column < 5; column++)
            {
                Assert.That(board.GetCell(2, column).HasBlock, Is.True,
                    $"r2c{column} would have been destroyed by a bomb explosion.");
                Assert.That(board.GetCell(0, column).HasBlock, Is.True,
                    $"r0c{column} would have been destroyed by a bomb explosion.");
            }

            Assert.That(result.RemovedBlockPositions, Has.Count.EqualTo(5),
                "Exactly the rocket's own row -- no more.");
        }

        [Test]
        public void BombRemovingRocket_DoesNotActivateRocket()
        {
            //  B B B B B
            //  B R H B B    the horizontal rocket at r1c2 is inside the bomb's 3x3
            //  B R X B B
            //  B B B B B
            Board board = BoardLayout.Build(
                "B B B B B",
                "B R H B B",
                "B R X B B",
                "B B B B B");

            PowerUpActivationResult result = PowerUpResolver.Activate(board, At(1, 2));

            // The rocket is gone as a piece...
            Assert.That(board.GetCell(2, 2).IsEmpty, Is.True);
            Assert.That(result.RemovedBlocks.Any(b => b.Kind == BlockKind.Rocket), Is.True);

            // ...but its row never cleared: r2c0 and r2c4 lie outside the bomb and survive.
            Assert.That(board.GetCell(2, 0).HasBlock, Is.True,
                "r2c0 would have been destroyed had the rocket fired.");
            Assert.That(board.GetCell(2, 4).HasBlock, Is.True,
                "r2c4 would have been destroyed had the rocket fired.");
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
        public void BombActivation_CountsOnlyNormalBlocksTowardColorGoals()
        {
            //  R R R
            //  R X H    the bomb's 3x3 catches the rocket at r1c2
            //  R R R
            Board board = BoardLayout.Build(
                "R R R",
                "R X H",
                "R R R");

            var red = new ColorGoal(BlockColor.Red, 20);
            var tracker = new GoalTracker(new[] { red });

            PowerUpActivationResult result = PowerUpResolver.Activate(board, At(1, 1));
            tracker.ProcessRemovedBlocks(result.RemovedBlocks);

            Assert.That(result.RemovedBlocks, Has.Count.EqualTo(9));
            Assert.That(red.CurrentCount, Is.EqualTo(7),
                "Nine pieces removed, but the bomb and the rocket are not Red normal blocks.");
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
