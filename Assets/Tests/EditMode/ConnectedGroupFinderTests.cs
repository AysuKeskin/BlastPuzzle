using System.Collections.Generic;
using System.Text;
using BlastPuzzle.Boards;
using BlastPuzzle.Gameplay;
using NUnit.Framework;

namespace BlastPuzzle.Tests.EditMode
{
    // Layout strings are written TOP ROW FIRST; BoardLayout flips them so row 0 is the
    // bottom, matching the domain. Assertions compare MEMBERSHIP, never order: the result
    // is semantically a set, and locking tests to BFS's visit order would freeze an
    // implementation detail that no gameplay rule depends on.
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
            //  R R B
            //  R B B
            //  R R G
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
        public void EmptyStart_ReturnsEmpty()
        {
            Board board = BoardLayout.Build("R . R");

            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, At(0, 1));

            Assert.That(group, Is.Empty);
        }

        [Test]
        public void OutsideBoard_ReturnsEmpty()
        {
            // Documents the chosen contract: a query about a cell that cannot exist
            // answers "nothing", rather than throwing.
            Board board = BoardLayout.Build("R R");

            Assert.That(ConnectedGroupFinder.FindConnectedGroup(board, At(99, 99)), Is.Empty);
            Assert.That(ConnectedGroupFinder.FindConnectedGroup(board, At(-1, 0)), Is.Empty);
        }

        [Test]
        public void CornerCell_WorksCorrectly()
        {
            //  R R
            //  R B     <- start at bottom-left corner, which has only two neighbours
            Board board = BoardLayout.Build(
                "R R",
                "R B");

            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, At(0, 0));

            Assert.That(group, Is.EquivalentTo(new[] { At(0, 0), At(1, 0), At(1, 1) }));
        }

        [Test]
        public void EdgeCell_WorksCorrectly()
        {
            //  B R B
            //  R R R   <- start at the left edge, which has three neighbours
            //  B R B
            Board board = BoardLayout.Build(
                "B R B",
                "R R R",
                "B R B");

            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, At(1, 0));

            Assert.That(group, Is.EquivalentTo(new[]
            {
                At(1, 0), At(1, 1), At(1, 2),
                At(2, 1),
                At(0, 1)
            }));
        }

        [Test]
        public void RegionBendingAroundCorners_IsFoundEntirely()
        {
            //  R R B R     Reaching the top-right Red from the top-left one means going
            //  B R B R     down, across the bottom and back up: BFS has to follow the
            //  R R R R     winding path rather than a straight line.
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
            //  R R B     Two Red regions of the same colour, fully separated by Blue.
            //  B B B     Only the one containing the start may come back.
            //  R R B
            Board board = BoardLayout.Build(
                "R R B",
                "B B B",
                "R R B");

            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, At(2, 0));

            Assert.That(group, Is.EquivalentTo(new[] { At(2, 0), At(2, 1) }));
            Assert.That(group, Has.No.Member(At(0, 0)));
            Assert.That(group, Has.No.Member(At(0, 1)));
        }

        [Test]
        public void GroupDetection_DoesNotMutateBoard()
        {
            Board board = BoardLayout.Build(
                "R R B R",
                "B R B R",
                "R R R R");

            string before = Describe(board);
            ConnectedGroupFinder.FindConnectedGroup(board, At(2, 0));
            string after = Describe(board);

            Assert.That(after, Is.EqualTo(before), "Finding a group must leave the board untouched.");
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
