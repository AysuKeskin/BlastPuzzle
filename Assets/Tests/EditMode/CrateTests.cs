using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;
using BlastPuzzle.Gameplay;
using BlastPuzzle.Goals;
using BlastPuzzle.Obstacles;
using NUnit.Framework;

namespace BlastPuzzle.Tests.EditMode
{
    // 'C' in a layout string is a crate. Layouts are TOP ROW FIRST; row 0 is the bottom.
    public sealed class CrateBoardTests
    {
        private static BoardPosition At(int row, int column) => new BoardPosition(row, column);

        [Test]
        public void CrateCanBePlacedInEmptyCell()
        {
            var board = new Board(2, 2);
            board.PlaceObstacle(At(0, 0), new Obstacle(ObstacleType.Crate));

            Cell cell = board.GetCell(0, 0);
            Assert.That(cell.HasObstacle, Is.True);
            Assert.That(cell.HasBlock, Is.False);
            Assert.That(cell.IsEmpty, Is.False, "A crate cell is occupied, not empty.");
            Assert.That(board.GetObstacle(At(0, 0)).Type, Is.EqualTo(ObstacleType.Crate));
        }

        [Test]
        public void CannotPlaceBlockOnCrate()
        {
            Board board = BoardLayout.Build("C .");

            Assert.Throws<InvalidOperationException>(
                () => board.SetBlock(At(0, 0), Block.CreateNormal(BlockColor.Red)));
        }

        [Test]
        public void CannotPlaceCrateOnBlock()
        {
            Board board = BoardLayout.Build("R .");

            Assert.Throws<InvalidOperationException>(
                () => board.PlaceObstacle(At(0, 0), new Obstacle(ObstacleType.Crate)));
        }

        [Test]
        public void ConnectedGroupDoesNotPassThroughCrate()
        {
            //  R C R   the crate breaks the line; neither Red can reach the other
            Board board = BoardLayout.Build("R C R");

            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, At(0, 0));

            Assert.That(group, Is.EquivalentTo(new[] { At(0, 0) }));
            Assert.That(group, Has.No.Member(At(0, 2)));
        }

        [Test]
        public void SelectingCrateCellFindsNoGroup()
        {
            Board board = BoardLayout.Build("R C R");

            Assert.That(ConnectedGroupFinder.FindConnectedGroup(board, At(0, 1)), Is.Empty);
        }
    }

    public sealed class ObstacleResolverTests
    {
        private static BoardPosition At(int row, int column) => new BoardPosition(row, column);

        private static IReadOnlyList<ObstacleRemoval> BlastAt(Board board, BoardPosition start)
        {
            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, start);
            GroupRemover.TryRemoveGroup(board, group, 2);
            return ObstacleResolver.ResolveAdjacentHits(board, group);
        }

        [Test]
        public void AdjacentBlastDestroysCrate()
        {
            //  R R
            //  C B     the crate sits directly below the left Red
            Board board = BoardLayout.Build(
                "R R",
                "C B");

            IReadOnlyList<ObstacleRemoval> removed = BlastAt(board, At(1, 0));

            Assert.That(removed, Has.Count.EqualTo(1));
            Assert.That(removed[0].Position, Is.EqualTo(At(0, 0)));
            Assert.That(board.GetCell(0, 0).HasObstacle, Is.False);
            Assert.That(board.GetCell(0, 0).IsEmpty, Is.True, "The cell is free once the crate is gone.");
        }

        [Test]
        public void NonAdjacentBlastDoesNotDestroyCrate()
        {
            //  R R .
            //  . . .     the crate is two cells away from everything blasted
            //  . . C
            Board board = BoardLayout.Build(
                "R R .",
                ". . .",
                ". . C");

            IReadOnlyList<ObstacleRemoval> removed = BlastAt(board, At(2, 0));

            Assert.That(removed, Is.Empty);
            Assert.That(board.GetCell(0, 2).HasObstacle, Is.True);
        }

        [Test]
        public void SameBlastHitsSameCrateOnlyOnce()
        {
            //  . R .
            //  R C R     one crate touched from FOUR sides by a single connected group
            //  . R .
            Board board = BoardLayout.Build(
                ". R .",
                "R C R",
                ". R .");

            // The four Reds are not connected to each other -- they are separated by the
            // crate -- so blast them as one group by using a shape that is connected.
            Board connected = BoardLayout.Build(
                "R R R",
                "R C R",
                "R R R");

            IReadOnlyList<ObstacleRemoval> removed = BlastAt(connected, At(2, 0));

            Assert.That(removed, Has.Count.EqualTo(1),
                "A crate adjacent to four removed blocks must still be destroyed exactly once.");
            Assert.That(removed[0].Position, Is.EqualTo(At(1, 1)));
            Assert.That(board, Is.Not.Null);
        }

        [Test]
        public void MultipleDifferentCratesCanBeDestroyedByOneBlast()
        {
            //  C R C
            //  . R .     one Red column touching two separate crates
            Board board = BoardLayout.Build(
                "C R C",
                ". R .");

            IReadOnlyList<ObstacleRemoval> removed = BlastAt(board, At(1, 1));

            Assert.That(removed, Has.Count.EqualTo(2));
            Assert.That(removed.Select(r => r.Position), Is.EquivalentTo(new[] { At(1, 0), At(1, 2) }));
        }

        [Test]
        public void ResolvingHitsWithNoCratesChangesNothing()
        {
            Board board = BoardLayout.Build(
                "R R",
                "B B");

            Assert.That(BlastAt(board, At(1, 0)), Is.Empty);
        }
    }

    public sealed class CrateGravityAndRefillTests
    {
        private static BoardPosition At(int row, int column) => new BoardPosition(row, column);

        private static string Describe(Board board)
        {
            var text = new StringBuilder();

            for (int row = board.Rows - 1; row >= 0; row--)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    Cell cell = board.GetCell(row, column);
                    text.Append(cell.HasObstacle ? 'C' : cell.IsEmpty ? '.' : cell.Block.Color.ToString()[0]);
                }

                text.Append('/');
            }

            return text.ToString();
        }

        [Test]
        public void GravityDoesNotMoveCrate()
        {
            //  C
            //  .    a crate with nothing beneath it must NOT fall
            Board board = BoardLayout.Build(
                "C",
                ".");

            IReadOnlyList<BlockMove> moves = GravityResolver.ApplyGravity(board);

            Assert.That(moves, Is.Empty);
            Assert.That(board.GetCell(1, 0).HasObstacle, Is.True, "The crate stayed put.");
            Assert.That(board.GetCell(0, 0).IsEmpty, Is.True);
        }

        [Test]
        public void GravityDoesNotMoveBlockThroughCrate()
        {
            //  R
            //  C     the Red cannot fall past the crate
            //  .
            Board board = BoardLayout.Build(
                "R",
                "C",
                ".");

            IReadOnlyList<BlockMove> moves = GravityResolver.ApplyGravity(board);

            Assert.That(moves, Is.Empty, "There is nowhere for the Red to fall to.");
            Assert.That(Describe(board), Is.EqualTo("R/C/./"));
        }

        [Test]
        public void GravitySettlesSegmentsAboveAndBelowCrate()
        {
            //  R        .
            //  .        R
            //  B   ->   B      the crate splits the column into two independent segments
            //  C        C
            //  .        .
            //  G        G
            Board board = BoardLayout.Build(
                "R",
                ".",
                "B",
                "C",
                ".",
                "G");

            GravityResolver.ApplyGravity(board);

            Assert.That(Describe(board), Is.EqualTo("./R/B/C/./G/"));
            Assert.That(board.GetCell(2, 0).HasObstacle, Is.True, "The crate never moved.");
        }

        [Test]
        public void RefillDoesNotFillCrateCell()
        {
            Board board = BoardLayout.Build(
                ". C .",
                ". . .");

            IReadOnlyList<BlockSpawn> spawns = RefillResolver.ApplyRefill(
                board, new[] { BlockColor.Red }, new Random(1));

            // Five empty cells, one crate cell left alone.
            Assert.That(spawns, Has.Count.EqualTo(5));
            Assert.That(spawns.Select(s => s.Position), Has.No.Member(At(1, 1)));
            Assert.That(board.GetCell(1, 1).HasObstacle, Is.True);
            Assert.That(board.GetCell(1, 1).HasBlock, Is.False);
        }

        [Test]
        public void RefillFillsOtherEmptyCells()
        {
            Board board = BoardLayout.Build(
                ". C",
                ". .");

            RefillResolver.ApplyRefill(board, new[] { BlockColor.Blue }, new Random(1));

            Assert.That(board.GetCell(1, 0).HasBlock, Is.True);
            Assert.That(board.GetCell(0, 0).HasBlock, Is.True);
            Assert.That(board.GetCell(0, 1).HasBlock, Is.True);
            Assert.That(board.GetCell(1, 1).HasObstacle, Is.True);
        }

        [Test]
        public void DestroyedCrateCellBecomesAvailableToGravityAndRefill()
        {
            //  R R
            //  C .    blasting the Reds frees the crate cell
            Board board = BoardLayout.Build(
                "R R",
                "C .");

            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, At(1, 0));
            GroupRemover.TryRemoveGroup(board, group, 2);
            ObstacleResolver.ResolveAdjacentHits(board, group);

            Assert.That(board.GetCell(0, 0).IsEmpty, Is.True);

            GravityResolver.ApplyGravity(board);
            RefillResolver.ApplyRefill(board, new[] { BlockColor.Green }, new Random(1));

            // Every cell is now a normal block cell -- the board is rectangular again.
            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    Assert.That(board.GetCell(row, column).HasBlock, Is.True);
                }
            }
        }
    }

    public sealed class CrateGoalTests
    {
        private static BoardPosition At(int row, int column) => new BoardPosition(row, column);

        [Test]
        public void CrateGoalProgressesFromDestroyedCrates()
        {
            var crateGoal = new CrateGoal(3);
            var tracker = new GoalTracker(Array.Empty<ColorGoal>(), crateGoal);

            Board board = BoardLayout.Build(
                "C R C",
                ". R .");
            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, At(1, 1));
            GroupRemover.TryRemoveGroup(board, group, 2);
            IReadOnlyList<ObstacleRemoval> crates = ObstacleResolver.ResolveAdjacentHits(board, group);

            tracker.ProcessRemovedObstacles(crates);

            Assert.That(crateGoal.CurrentCount, Is.EqualTo(2));
            Assert.That(crateGoal.Remaining, Is.EqualTo(1));
            Assert.That(tracker.AreAllGoalsComplete, Is.False);
        }

        [Test]
        public void ColorGoalDoesNotProgressFromCrateRemoval()
        {
            var blue = new ColorGoal(BlockColor.Blue, 5);
            var tracker = new GoalTracker(new[] { blue }, new CrateGoal(2));

            tracker.ProcessRemovedObstacles(new[]
            {
                new ObstacleRemoval(At(0, 0), new Obstacle(ObstacleType.Crate)),
                new ObstacleRemoval(At(0, 1), new Obstacle(ObstacleType.Crate))
            });

            Assert.That(blue.CurrentCount, Is.Zero, "Destroying crates must not advance a colour goal.");
            Assert.That(tracker.CrateGoal.CurrentCount, Is.EqualTo(2));
        }

        [Test]
        public void CrateGoalDoesNotProgressFromBlockRemoval()
        {
            var crateGoal = new CrateGoal(3);
            var tracker = new GoalTracker(new[] { new ColorGoal(BlockColor.Blue, 5) }, crateGoal);

            tracker.ProcessRemovedBlocks(new[] { Block.CreateNormal(BlockColor.Blue), Block.CreateNormal(BlockColor.Red) });

            Assert.That(crateGoal.CurrentCount, Is.Zero, "Destroying blocks must not advance the crate goal.");
        }

        [Test]
        public void CrateGoalCompletesTheLevelOnlyWithColorGoals()
        {
            var blue = new ColorGoal(BlockColor.Blue, 1);
            var crateGoal = new CrateGoal(1);
            var tracker = new GoalTracker(new[] { blue }, crateGoal);

            tracker.ProcessRemovedObstacles(new[] { new ObstacleRemoval(At(0, 0), new Obstacle(ObstacleType.Crate)) });
            Assert.That(tracker.AreAllGoalsComplete, Is.False, "The colour goal is still open.");

            tracker.ProcessRemovedBlocks(new[] { Block.CreateNormal(BlockColor.Blue) });
            Assert.That(tracker.AreAllGoalsComplete, Is.True);
        }

        [Test]
        public void CrateGoalProgressIsCappedAtTarget()
        {
            var crateGoal = new CrateGoal(2);
            crateGoal.RecordDestroyed(5);

            Assert.That(crateGoal.CurrentCount, Is.EqualTo(2));
            Assert.That(crateGoal.Remaining, Is.Zero);
        }
    }
}
