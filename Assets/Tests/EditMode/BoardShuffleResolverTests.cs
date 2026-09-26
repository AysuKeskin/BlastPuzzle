using System;
using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;
using BlastPuzzle.Gameplay;
using BlastPuzzle.Goals;
using NUnit.Framework;

namespace BlastPuzzle.Tests.EditMode
{
    public sealed class BoardShuffleResolverTests
    {
        private const int MinimumGroupSize = 2;

        // A checkerboard has no connected pair anywhere, so it is deadlocked by construction.
        private static Board DeadlockedBoard() => BoardLayout.Build(
            "RBRB",
            "BRBR",
            "RBRB",
            "BRBR");

        private static List<Block> BlocksOf(Board board)
        {
            var blocks = new List<Block>();

            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    Cell cell = board.GetCell(row, column);

                    if (cell.HasBlock)
                    {
                        blocks.Add(cell.Block);
                    }
                }
            }

            return blocks;
        }

        private static Dictionary<BlockColor, int> ColorCounts(Board board)
        {
            var counts = new Dictionary<BlockColor, int>();

            foreach (Block block in BlocksOf(board))
            {
                counts.TryGetValue(block.Color, out int existing);
                counts[block.Color] = existing + 1;
            }

            return counts;
        }

        private static List<BoardPosition> CratePositions(Board board)
        {
            var crates = new List<BoardPosition>();

            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    if (board.GetCell(row, column).HasObstacle)
                    {
                        crates.Add(new BoardPosition(row, column));
                    }
                }
            }

            return crates;
        }

        [Test]
        public void DeadlockedBoard_IsRecognisedAndShuffled()
        {
            Board board = DeadlockedBoard();
            Assert.That(MoveAvailabilityChecker.HasAnyValidMove(board, MinimumGroupSize), Is.False);

            ShuffleResult result = BoardShuffleResolver.Shuffle(board, MinimumGroupSize, new Random(1));

            Assert.That(result.WasNeeded, Is.True);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Attempts, Is.GreaterThan(0));
        }

        [Test]
        public void Shuffle_ProducesPlayableBoardWhenPossible()
        {
            Board board = DeadlockedBoard();

            BoardShuffleResolver.Shuffle(board, MinimumGroupSize, new Random(1));

            Assert.That(MoveAvailabilityChecker.HasAnyValidMove(board, MinimumGroupSize), Is.True,
                "The whole point of the shuffle is that the board is playable afterwards.");
        }

        [Test]
        public void AlreadyPlayableBoard_DoesNotNeedShuffle()
        {
            Board board = BoardLayout.Build(
                "RRBR",
                "BRBR",
                "RBRB",
                "BRBR");

            string before = Describe(board);
            ShuffleResult result = BoardShuffleResolver.Shuffle(board, MinimumGroupSize, new Random(1));

            Assert.That(result.WasNeeded, Is.False);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Moves, Is.Empty);
            Assert.That(Describe(board), Is.EqualTo(before), "A board that needed nothing must not be touched.");
        }
        [Test]
        public void BoardWithPowerUp_IsNeverShuffled()
        {
            Board board = BoardLayout.Build(
                "RBRX",
                "BRBR",
                "RBRB",
                "BRBR");

            string before = Describe(board);
            ShuffleResult result = BoardShuffleResolver.Shuffle(board, MinimumGroupSize, new Random(1));

            Assert.That(result.WasNeeded, Is.False);
            Assert.That(Describe(board), Is.EqualTo(before));
        }

        // The identity guarantee that lets pooled BlockViews survive a shuffle untouched.
        [Test]
        public void Shuffle_PreservesBlockInstances()
        {
            Board board = DeadlockedBoard();
            List<Block> before = BlocksOf(board);

            BoardShuffleResolver.Shuffle(board, MinimumGroupSize, new Random(1));

            List<Block> after = BlocksOf(board);

            Assert.That(after.Count, Is.EqualTo(before.Count));

            foreach (Block block in before)
            {
                Assert.That(after, Has.Some.SameAs(block),
                    "A shuffle must move the existing blocks, not replace them.");
            }
        }

        [Test]
        public void Shuffle_PreservesColorCounts()
        {
            Board board = DeadlockedBoard();
            Dictionary<BlockColor, int> before = ColorCounts(board);

            BoardShuffleResolver.Shuffle(board, MinimumGroupSize, new Random(1));

            Assert.That(ColorCounts(board), Is.EquivalentTo(before));
        }

        [Test]
        public void Shuffle_PreservesCratePositions()
        {
            Board board = BoardLayout.Build(
                "RBRB",
                "BCCR",
                "RCCB",
                "BRBR");

            List<BoardPosition> before = CratePositions(board);
            Assume.That(MoveAvailabilityChecker.HasAnyValidMove(board, MinimumGroupSize), Is.False);

            BoardShuffleResolver.Shuffle(board, MinimumGroupSize, new Random(4));

            Assert.That(CratePositions(board), Is.EquivalentTo(before),
                "Crates are placed by the level and must not move.");
        }

        [Test]
        public void Shuffle_WithSameSeed_IsReproducible()
        {
            Board first = DeadlockedBoard();
            Board second = DeadlockedBoard();

            BoardShuffleResolver.Shuffle(first, MinimumGroupSize, new Random(12345));
            BoardShuffleResolver.Shuffle(second, MinimumGroupSize, new Random(12345));

            Assert.That(Describe(second), Is.EqualTo(Describe(first)));
        }

        [Test]
        public void ReportedMoves_MatchWhereTheBlocksActuallyEndedUp()
        {
            Board board = DeadlockedBoard();
            var startedAt = new Dictionary<Block, BoardPosition>();

            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    Cell cell = board.GetCell(row, column);

                    if (cell.HasBlock)
                    {
                        startedAt[cell.Block] = cell.Position;
                    }
                }
            }

            ShuffleResult result = BoardShuffleResolver.Shuffle(board, MinimumGroupSize, new Random(7));

            Assert.That(result.Moves, Is.Not.Empty);

            foreach (BlockMove move in result.Moves)
            {
                Assert.That(move.From, Is.EqualTo(startedAt[move.Block]),
                    "A reported move does not start where that block actually was.");
                Assert.That(board.GetCell(move.To).Block, Is.SameAs(move.Block),
                    "A reported move does not end where that block actually is.");
                Assert.That(move.From, Is.Not.EqualTo(move.To),
                    "A block that did not move should not be reported as moving.");
            }
        }

        // An arrangement that cannot be made playable must fail, not spin.
        [Test]
        public void ImpossibleArrangement_FailsClearlyWithoutInfiniteLoop()
        {
            // Four blocks, all different colours: no permutation puts two alike together.
            Board board = BoardLayout.Build(
                "RB",
                "GY");

            ShuffleResult result = BoardShuffleResolver.Shuffle(board, MinimumGroupSize, new Random(1), 20);

            Assert.That(result.WasNeeded, Is.True);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Attempts, Is.EqualTo(20), "Every attempt should have been used before giving up.");
        }

        // Failure must leave the board intact rather than in the last rejected permutation.
        [Test]
        public void FailedShuffle_RestoresTheOriginalArrangement()
        {
            Board board = BoardLayout.Build(
                "RB",
                "GY");

            string before = Describe(board);

            ShuffleResult result = BoardShuffleResolver.Shuffle(board, MinimumGroupSize, new Random(1), 20);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(Describe(board), Is.EqualTo(before),
                "A failed shuffle must not leave the board half-mutated.");
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
