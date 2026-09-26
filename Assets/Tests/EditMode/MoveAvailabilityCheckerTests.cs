using BlastPuzzle.Boards;
using BlastPuzzle.Gameplay;
using NUnit.Framework;

namespace BlastPuzzle.Tests.EditMode
{
    // "Can the player do anything?" -- the query that decides whether a board needs rescuing.
    public sealed class MoveAvailabilityCheckerTests
    {
        private const int MinimumGroupSize = 2;

        [Test]
        public void EmptyBoard_HasNoValidMove()
        {
            Board board = BoardLayout.Build(
                "...",
                "...",
                "...");

            Assert.That(MoveAvailabilityChecker.HasAnyValidMove(board, MinimumGroupSize), Is.False);
        }

        // A checkerboard: every block touches only blocks of the other colour.
        [Test]
        public void IsolatedNormalBlocks_HaveNoValidMove()
        {
            Board board = BoardLayout.Build(
                "RBRB",
                "BRBR",
                "RBRB",
                "BRBR");

            Assert.That(MoveAvailabilityChecker.HasAnyValidMove(board, MinimumGroupSize), Is.False);
        }

        [Test]
        public void AdjacentPair_HasValidMove()
        {
            Board board = BoardLayout.Build(
                "RBRB",
                "BRBR",
                "RBRB",
                "BRRR");

            Assert.That(MoveAvailabilityChecker.HasAnyValidMove(board, MinimumGroupSize), Is.True);
        }

        // Diagonal neighbours are not connected, here or anywhere else in the game.
        [Test]
        public void DiagonalSameColor_DoesNotCreateValidMove()
        {
            Board board = BoardLayout.Build(
                "RB",
                "BR");

            Assert.That(MoveAvailabilityChecker.HasAnyValidMove(board, MinimumGroupSize), Is.False);
        }

        // Crates are destroyed BY moves; they are never a move themselves.
        [Test]
        public void Crates_DoNotCountAsMoves()
        {
            Board board = BoardLayout.Build(
                "RCB",
                "CCC",
                "BCR");

            Assert.That(MoveAvailabilityChecker.HasAnyValidMove(board, MinimumGroupSize), Is.False);
        }

        [TestCase(BoardLayout.HorizontalRocketCell)]
        [TestCase(BoardLayout.VerticalRocketCell)]
        [TestCase(BoardLayout.BombCell)]
        public void PowerUpAlone_IsEnoughForValidMove(char powerUp)
        {
            Board board = BoardLayout.Build(
                "RB" + powerUp,
                "BRB",
                "RBR");

            Assert.That(MoveAvailabilityChecker.HasAnyValidMove(board, MinimumGroupSize), Is.True);
        }

        // The threshold is a parameter, not a constant baked into the checker.
        [Test]
        public void GroupSmallerThanTheThreshold_IsNotAValidMove()
        {
            Board board = BoardLayout.Build(
                "RRB",
                "BRB",
                "RBR");

            Assert.That(MoveAvailabilityChecker.HasAnyValidMove(board, 3), Is.True,
                "Three connected reds should satisfy a threshold of three.");
            Assert.That(MoveAvailabilityChecker.HasAnyValidMove(board, 4), Is.False,
                "The same three should not satisfy a threshold of four.");
        }

        private static string Describe(Board board)
        {
            var text = new System.Text.StringBuilder();

            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    Cell cell = board.GetCell(row, column);
                    text.Append(cell.HasObstacle ? "C" : cell.HasBlock ? cell.Block.ToString() : ".");
                    text.Append('|');
                }
            }

            return text.ToString();
        }
    }
}
