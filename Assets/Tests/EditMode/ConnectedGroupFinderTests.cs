using System.Collections.Generic;
using System.Text;
using BlastPuzzle.Boards;
using BlastPuzzle.Gameplay;
using NUnit.Framework;

namespace BlastPuzzle.Tests.EditMode
{
    public sealed class ConnectedGroupFinderTests
    {
        private static BoardPosition At(int row, int column) => new BoardPosition(row, column);

        [Test]
        public void SingleBlock_ReturnsOne()
        {
            // The lone Red touches only Blues.
            Board board = BoardLayout.Build(
                "R B",
                "B B");

            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, At(1, 0));

            // Size 1, NOT zero: finding a group and judging a move are separate jobs.
            Assert.That(group, Is.EquivalentTo(new[] { At(1, 0) }));
        }

        [Test]
        public void OrthogonallyConnectedGroup_ReturnsAllMembers()
        {
            Board board = BoardLayout.Build(
                "R R B",
                "R B B",
                "R R G");

            // Start from the top-left Red.
            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, At(2, 0));

            Assert.That(group, Is.EquivalentTo(new[]
            {
                At(2, 0), At(2, 1),
                At(1, 0),
                At(0, 0), At(0, 1)
            }));
        }

        [Test]
        public void DiagonalBlocks_AreNotConnected()
        {
            // The two Reds touch only at a corner.
            Board board = BoardLayout.Build(
                "R B",
                "B R");

            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, At(1, 0));

            Assert.That(group, Is.EquivalentTo(new[] { At(1, 0) }));
            Assert.That(group, Has.No.Member(At(0, 1)), "A diagonal neighbour must never join the group.");
        }

        [Test]
        public void DifferentColors_AreNotConnected()
        {
            Board board = BoardLayout.Build("R B R");

            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, At(0, 0));

            Assert.That(group, Is.EquivalentTo(new[] { At(0, 0) }));
        }

        [Test]
        public void RegionBendingAroundCorners_IsFoundEntirely()
        {
            Board board = BoardLayout.Build(
                "R R B R",
                "B R B R",
                "R R R R");

            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, At(2, 0));

            Assert.That(group, Is.EquivalentTo(new[]
            {
                At(2, 0), At(2, 1), At(2, 3),
                At(1, 1), At(1, 3),
                At(0, 0), At(0, 1), At(0, 2), At(0, 3)
            }));

            // Every Blue stays out.
            Assert.That(group, Has.No.Member(At(2, 2)));
            Assert.That(group, Has.No.Member(At(1, 0)));
            Assert.That(group, Has.No.Member(At(1, 2)));
        }

        [Test]
        public void DisconnectedRegionOfSameColour_IsNotIncluded()
        {
            Board board = BoardLayout.Build(
                "R R B",
                "B B B",
                "R R B");

            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, At(2, 0));

            Assert.That(group, Is.EquivalentTo(new[] { At(2, 0), At(2, 1) }));
            Assert.That(group, Has.No.Member(At(0, 0)));
            Assert.That(group, Has.No.Member(At(0, 1)));
        }

        // Full board state as text, so a difference shows up as a readable diff.
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
