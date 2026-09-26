using System.Collections.Generic;
using System.Text;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;
using BlastPuzzle.Gameplay;
using NUnit.Framework;

namespace BlastPuzzle.Tests.EditMode
{
    public sealed class GroupRemoverTests
    {
        private const int MinimumGroupSize = 2;

        private static BoardPosition At(int row, int column) => new BoardPosition(row, column);

        [Test]
        public void ValidGroup_RemovesAllMembers()
        {
            Board board = BoardLayout.Build(
                "R R B",
                "R B B",
                "R R G");

            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, At(2, 0));
            IReadOnlyList<Block> removed = GroupRemover.TryRemoveGroup(board, group, MinimumGroupSize);

            Assert.That(removed, Has.Count.EqualTo(5));

            foreach (BoardPosition position in group)
            {
                Assert.That(board.GetCell(position).IsEmpty, Is.True, $"{position} should be empty after removal.");
            }
        }

        [Test]
        public void InvalidSingleBlock_DoesNotMutateBoard()
        {
            // The lone Red is a real connected component of size 1, but below the threshold.
            Board board = BoardLayout.Build(
                "R B",
                "B B");

            string before = Describe(board);
            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, At(1, 0));

            Assert.That(group, Has.Count.EqualTo(1), "The finder must still report the true component.");

            IReadOnlyList<Block> removed = GroupRemover.TryRemoveGroup(board, group, MinimumGroupSize);

            Assert.That(removed, Is.Empty);
            Assert.That(Describe(board), Is.EqualTo(before), "An invalid group must leave the board untouched.");
        }

        [Test]
        public void Removal_DoesNotRemoveDisconnectedSameColorBlocks()
        {
            Board board = BoardLayout.Build(
                "R R B",
                "B B B",
                "R R B");

            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, At(2, 0));
            GroupRemover.TryRemoveGroup(board, group, MinimumGroupSize);

            // Top region gone.
            Assert.That(board.GetCell(2, 0).IsEmpty, Is.True);
            Assert.That(board.GetCell(2, 1).IsEmpty, Is.True);

            // Bottom region, same colour but not connected, untouched.
            Assert.That(board.GetCell(0, 0).IsEmpty, Is.False);
            Assert.That(board.GetCell(0, 1).IsEmpty, Is.False);
            Assert.That(board.GetCell(0, 0).Block.Color, Is.EqualTo(BlockColor.Red));
        }

        [Test]
        public void RemoveBlock_LeavesCellEmpty()
        {
            Board board = BoardLayout.Build("R R");

            board.RemoveBlock(At(0, 0));

            Cell cell = board.GetCell(0, 0);
            Assert.That(cell.IsEmpty, Is.True);
            Assert.That(cell.Block, Is.Null);
            // The Cell itself still exists -- only its contents were cleared.
            Assert.That(cell.Position, Is.EqualTo(At(0, 0)));
        }

        [Test]
        public void RemovedBlock_IsReturnedByBoard()
        {
            Board board = BoardLayout.Build("G R");

            Block original = board.GetCell(0, 0).Block;
            Block returned = board.RemoveBlock(At(0, 0));
            Assert.That(returned, Is.SameAs(original));
            Assert.That(returned.Color, Is.EqualTo(BlockColor.Green));

            // Removing from an already-empty cell reports null rather than throwing.
            Assert.That(board.RemoveBlock(At(0, 0)), Is.Null);
        }

        [Test]
        public void RemovingOneGroup_PreservesOtherBlocks()
        {
            Board board = BoardLayout.Build(
                "R R B R",
                "B R B R",
                "R R R R");

            Assert.That(CountOccupied(board), Is.EqualTo(12));

            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, At(2, 0));
            IReadOnlyList<Block> removed = GroupRemover.TryRemoveGroup(board, group, MinimumGroupSize);

            Assert.That(removed, Has.Count.EqualTo(9));
            Assert.That(CountOccupied(board), Is.EqualTo(3), "Only the three Blues should remain.");
            Assert.That(Describe(board), Is.EqualTo("..B./B.B./..../"));
        }

        [Test]
        public void GroupPositionsRemainUsableDuringRemoval()
        {
            Board board = BoardLayout.Build(
                "R R R",
                "R R R",
                "R R R");

            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, At(1, 1));
            Assert.That(group, Has.Count.EqualTo(9));

            IReadOnlyList<Block> removed = GroupRemover.TryRemoveGroup(board, group, MinimumGroupSize);

            // Every position was still addressable after earlier iterations had emptied cells.
            Assert.That(removed, Has.Count.EqualTo(9));
            Assert.That(CountOccupied(board), Is.EqualTo(0));

            foreach (BoardPosition position in group)
            {
                Assert.That(board.GetCell(position).IsEmpty, Is.True);
            }
        }

        [Test]
        public void OccupiedCountDropsByExactlyTheGroupSize()
        {
            Board board = BoardLayout.Build(
                "R R B R",
                "B R B R",
                "R R R R");

            int before = CountOccupied(board);
            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, At(0, 0));
            IReadOnlyList<Block> removed = GroupRemover.TryRemoveGroup(board, group, MinimumGroupSize);

            Assert.That(CountOccupied(board), Is.EqualTo(before - removed.Count));
        }

        [Test]
        public void EmptyStartProducesNoRemoval()
        {
            Board board = BoardLayout.Build("R . R");

            string before = Describe(board);
            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, At(0, 1));
            IReadOnlyList<Block> removed = GroupRemover.TryRemoveGroup(board, group, MinimumGroupSize);

            Assert.That(removed, Is.Empty);
            Assert.That(Describe(board), Is.EqualTo(before));
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
