using System.Collections.Generic;
using System.Linq;
using System.Text;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;
using BlastPuzzle.Gameplay;
using NUnit.Framework;

namespace BlastPuzzle.Tests.EditMode
{
    public sealed class GravityResolverTests
    {
        private static BoardPosition At(int row, int column) => new BoardPosition(row, column);

        [Test]
        public void EmptyBoard_ProducesNoMoves()
        {
            Board board = BoardLayout.Build(
                ". . .",
                ". . .");

            Assert.That(GravityResolver.ApplyGravity(board), Is.Empty);
        }

        [Test]
        public void FullBoard_ProducesNoMoves()
        {
            Board board = BoardLayout.Build(
                "R B G",
                "Y P R");

            string before = Describe(board);

            Assert.That(GravityResolver.ApplyGravity(board), Is.Empty);
            Assert.That(Describe(board), Is.EqualTo(before), "A full board is already settled.");
        }

        [Test]
        public void AlreadySettledColumn_ProducesNoMoves()
        {
            // Blocks already packed at the bottom, holes already at the top.
            Board board = BoardLayout.Build(
                ". .",
                ". .",
                "R B",
                "G Y");

            Assert.That(GravityResolver.ApplyGravity(board), Is.Empty);
        }

        [Test]
        public void SingleBlockAboveHole_FallsToBottom()
        {
            Board board = BoardLayout.Build(
                "R",
                ".",
                ".");

            IReadOnlyList<BlockMove> moves = GravityResolver.ApplyGravity(board);

            Assert.That(moves, Has.Count.EqualTo(1));
            Assert.That(moves[0].From, Is.EqualTo(At(2, 0)));
            Assert.That(moves[0].To, Is.EqualTo(At(0, 0)));
            Assert.That(board.GetCell(0, 0).IsEmpty, Is.False);
            Assert.That(board.GetCell(2, 0).IsEmpty, Is.True);
            AssertSettled(board);
        }

        [Test]
        public void MultipleBlocksInColumn_CompactDownward()
        {
            Board board = BoardLayout.Build(
                "R",
                ".",
                "B",
                ".",
                "G");

            GravityResolver.ApplyGravity(board);

            Assert.That(Describe(board), Is.EqualTo("././R/B/G/"));
            AssertSettled(board);
        }

        [Test]
        public void Gravity_PreservesVerticalOrder()
        {
            // Bottom-to-top the column reads G, B, R before and after; only the gaps close.
            Board board = BoardLayout.Build(
                "R",
                ".",
                "B",
                ".",
                "G");

            List<BlockColor> orderBefore = ColumnFromBottom(board, 0);
            GravityResolver.ApplyGravity(board);
            List<BlockColor> orderAfter = ColumnFromBottom(board, 0);

            Assert.That(orderAfter, Is.EqualTo(orderBefore).AsCollection);
            Assert.That(orderAfter, Is.EqualTo(new[] { BlockColor.Green, BlockColor.Blue, BlockColor.Red }).AsCollection);
        }

        [Test]
        public void Gravity_DoesNotMoveBlocksHorizontally()
        {
            Board board = BoardLayout.Build(
                "R . B .",
                ". Y . P",
                "G . . .");

            IReadOnlyList<BlockMove> moves = GravityResolver.ApplyGravity(board);

            Assert.That(moves, Is.Not.Empty);
            foreach (BlockMove move in moves)
            {
                Assert.That(move.To.Column, Is.EqualTo(move.From.Column),
                    $"{move} changed column; gravity must only move vertically.");
                Assert.That(move.To.Row, Is.LessThan(move.From.Row), $"{move} did not move downward.");
            }
        }

        [Test]
        public void Gravity_DoesNotChangeOccupiedBlockCount()
        {
            Board board = BoardLayout.Build(
                "R . B .",
                ". Y . P",
                "G . . .");

            int before = CountOccupied(board);
            GravityResolver.ApplyGravity(board);

            Assert.That(CountOccupied(board), Is.EqualTo(before));
            Assert.That(before, Is.EqualTo(5));
        }

        [Test]
        public void Gravity_PreservesBlockIdentity()
        {
            Board board = BoardLayout.Build(
                "R",
                ".",
                "B",
                ".",
                "G");

            // The exact instances, captured before anything moves.
            Block red = board.GetCell(4, 0).Block;
            Block blue = board.GetCell(2, 0).Block;
            Block green = board.GetCell(0, 0).Block;

            GravityResolver.ApplyGravity(board);

            // Same objects, new cells -- not copies, not replacements.
            Assert.That(board.GetCell(2, 0).Block, Is.SameAs(red));
            Assert.That(board.GetCell(1, 0).Block, Is.SameAs(blue));
            Assert.That(board.GetCell(0, 0).Block, Is.SameAs(green));
        }

        [Test]
        public void Gravity_ReturnsCorrectFromAndToPositions()
        {
            Board board = BoardLayout.Build(
                "R",
                ".",
                "B",
                ".",
                "G");

            Block red = board.GetCell(4, 0).Block;
            Block blue = board.GetCell(2, 0).Block;

            IReadOnlyList<BlockMove> moves = GravityResolver.ApplyGravity(board);

            // Compared as a set: the order moves are reported in is not part of the contract.
            Assert.That(moves.Select(m => $"{m.From}->{m.To}"),
                Is.EquivalentTo(new[] { "(r2, c0)->(r1, c0)", "(r4, c0)->(r2, c0)" }));

            // Each record carries the block that actually moved.
            Assert.That(moves.Single(m => m.From == At(4, 0)).Block, Is.SameAs(red));
            Assert.That(moves.Single(m => m.From == At(2, 0)).Block, Is.SameAs(blue));

            // The Green at the bottom never moved, so it is not reported at all.
            Assert.That(moves.Any(m => m.From == At(0, 0)), Is.False);
        }

        [Test]
        public void Gravity_HandlesMultipleColumnsIndependently()
        {
            //  col0 needs to fall two rows, col1 is already settled, col2 falls one.
            Board board = BoardLayout.Build(
                "R . B",
                ". . .",
                ". Y G");

            GravityResolver.ApplyGravity(board);

            Assert.That(Describe(board), Is.EqualTo(".../..B/RYG/"));
            AssertSettled(board);
        }

        [Test]
        public void EntireTopSectionCanCollapse()
        {
            // Everything sits at the top with nothing beneath it; the whole block falls.
            Board board = BoardLayout.Build(
                "R B G",
                "Y P R",
                ". . .",
                ". . .");

            IReadOnlyList<BlockMove> moves = GravityResolver.ApplyGravity(board);

            Assert.That(moves, Has.Count.EqualTo(6), "Every block moved.");
            Assert.That(Describe(board), Is.EqualTo(".../.../RBG/YPR/"));
            AssertSettled(board);
        }

        [Test]
        public void GravityAfterRemoval_SettlesTheBoard()
        {
            // The whole Milestone 5 + 6 sequence on one board.
            Board board = BoardLayout.Build(
                "R R B",
                "R B B",
                "R R G");

            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, At(2, 0));
            GroupRemover.TryRemoveGroup(board, group, 2);
            Assert.That(Describe(board), Is.EqualTo("..B/.BB/..G/"));

            GravityResolver.ApplyGravity(board);
            Assert.That(Describe(board), Is.EqualTo("..B/..B/.BG/"));
            AssertSettled(board);
            Assert.That(CountOccupied(board), Is.EqualTo(4));
        }
        private static void AssertSettled(Board board)
        {
            for (int column = 0; column < board.Columns; column++)
            {
                bool seenEmpty = false;

                for (int row = 0; row < board.Rows; row++)
                {
                    bool empty = board.GetCell(row, column).IsEmpty;

                    if (empty)
                    {
                        seenEmpty = true;
                    }
                    else if (seenEmpty)
                    {
                        Assert.Fail($"Column {column} is not settled: an occupied cell sits above an empty one at row {row}.");
                    }
                }
            }
        }

        private static List<BlockColor> ColumnFromBottom(Board board, int column)
        {
            var colors = new List<BlockColor>();

            for (int row = 0; row < board.Rows; row++)
            {
                Cell cell = board.GetCell(row, column);

                if (!cell.IsEmpty)
                {
                    colors.Add(cell.Block.Color);
                }
            }

            return colors;
        }

        private static int CountOccupied(Board board)
        {
            int count = 0;

            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    if (!board.GetCell(row, column).IsEmpty)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        // Top row first, '/' between rows.
        private static string Describe(Board board)
        {
            var text = new StringBuilder();

            for (int row = board.Rows - 1; row >= 0; row--)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    Cell cell = board.GetCell(row, column);
                    text.Append(cell.IsEmpty ? "." : cell.Block.Color.ToString()[0].ToString());
                }

                text.Append('/');
            }

            return text.ToString();
        }
    }
}
