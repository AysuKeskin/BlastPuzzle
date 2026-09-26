using System.Linq;
using BlastPuzzle.Boards;
using BlastPuzzle.Gameplay;
using NUnit.Framework;

namespace BlastPuzzle.Tests.EditMode
{
    public sealed class DiagonalGravityTests
    {
        [Test]
        public void CoveredGap_ReceivesExistingUpperSideBlock()
        {
            var board = BoardLayout.Build("R C B", "G . Y");
            var red = board.GetCell(1, 0).Block;
            var blue = board.GetCell(1, 2).Block;
            var moves = GravityResolver.ApplyDiagonalGravity(board);
            Assert.That(moves, Has.Count.EqualTo(1));
            Assert.That(moves[0].To, Is.EqualTo(new BoardPosition(0, 1)));
            Assert.That(board.GetCell(0, 1).Block == red || board.GetCell(0, 1).Block == blue, Is.True);
            Assert.That(board.GetCell(1, 1).HasObstacle, Is.True);
        }

        [Test]
        public void SolidSideWalls_DoNotAllowDiagonalEntry()
        {
            var board = BoardLayout.Build("R C B", "C . C");
            Assert.That(GravityResolver.ApplyDiagonalGravity(board), Is.Empty);
        }

        [Test]
        public void OpenColumn_KeepsNormalVerticalGravity()
        {
            var board = BoardLayout.Build("R . B", "G . Y");
            Assert.That(GravityResolver.ApplyDiagonalGravity(board), Is.Empty);
        }

        [Test]
        public void RefillFromTop_DoesNotCreateBlockUnderCrate()
        {
            var board = BoardLayout.Build(". C .", "G . Y");
            var spawns = RefillResolver.ApplyRefill(board,
                new[] { BlastPuzzle.Blocks.BlockColor.Blue }, new System.Random(1), true);
            Assert.That(spawns, Has.Count.EqualTo(2));
            Assert.That(board.GetCell(0, 1).IsEmpty, Is.True);
            Assert.That(GravityResolver.ApplyDiagonalGravity(board), Has.Count.EqualTo(1));
        }

        [Test]
        public void Wave_MovesEachBlockOnlyOnce()
        {
            var board = BoardLayout.Build("R C B", "G . Y", "P . G");
            var moves = GravityResolver.ApplyDiagonalGravity(board);
            Assert.That(moves.Select(m => m.Block).Distinct().Count(), Is.EqualTo(moves.Count));
            Assert.That(moves.All(m => m.To.Row == m.From.Row - 1), Is.True);
        }
    }
}
