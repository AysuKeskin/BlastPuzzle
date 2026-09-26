using System;
using System.Collections.Generic;
using System.Linq;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;
using BlastPuzzle.Gameplay;
using NUnit.Framework;

namespace BlastPuzzle.Tests.EditMode
{
    public sealed class RefillResolverTests
    {
        private const int Seed = 12345;

        private static readonly IReadOnlyList<BlockColor> AllColors = new[]
        {
            BlockColor.Red, BlockColor.Blue, BlockColor.Green, BlockColor.Yellow, BlockColor.Purple
        };

        private static BoardPosition At(int row, int column) => new BoardPosition(row, column);

        private static Random Rng() => new Random(Seed);

        [Test]
        public void EmptyBoard_FillsEveryCell()
        {
            Board board = BoardLayout.Build(
                ". . .",
                ". . .");

            IReadOnlyList<BlockSpawn> spawns = RefillResolver.ApplyRefill(board, AllColors, Rng());

            Assert.That(spawns, Has.Count.EqualTo(6));
            Assert.That(CountOccupied(board), Is.EqualTo(board.Rows * board.Columns));
        }

        [Test]
        public void PartiallyEmptyBoard_FillsOnlyEmptyCells()
        {
            Board board = BoardLayout.Build(
                ". . B",
                "R . G");

            IReadOnlyList<BlockSpawn> spawns = RefillResolver.ApplyRefill(board, AllColors, Rng());

            // Three holes: (r1,c0), (r1,c1) and (r0,c1).
            Assert.That(spawns.Select(s => s.Position),
                Is.EquivalentTo(new[] { At(1, 0), At(1, 1), At(0, 1) }));
            Assert.That(CountOccupied(board), Is.EqualTo(6));
        }

        [Test]
        public void FullBoard_ProducesNoSpawns()
        {
            Board board = BoardLayout.Build(
                "R B",
                "G Y");

            Assert.That(RefillResolver.ApplyRefill(board, AllColors, Rng()), Is.Empty);
        }

        [Test]
        public void Refill_DoesNotReplaceExistingBlocks()
        {
            Board board = BoardLayout.Build(
                ". .",
                "R B");

            Block red = board.GetCell(0, 0).Block;
            Block blue = board.GetCell(0, 1).Block;

            RefillResolver.ApplyRefill(board, AllColors, Rng());

            Assert.That(board.GetCell(0, 0).Block, Is.SameAs(red));
            Assert.That(board.GetCell(0, 1).Block, Is.SameAs(blue));
            Assert.That(board.GetCell(0, 0).Block.Color, Is.EqualTo(BlockColor.Red));
            Assert.That(board.GetCell(0, 1).Block.Color, Is.EqualTo(BlockColor.Blue));
        }

        [Test]
        public void Refill_PreservesExistingBlockIdentity()
        {
            Board board = BoardLayout.Build(
                ". . .",
                "R B G");

            Block[] before = { board.GetCell(0, 0).Block, board.GetCell(0, 1).Block, board.GetCell(0, 2).Block };

            IReadOnlyList<BlockSpawn> spawns = RefillResolver.ApplyRefill(board, AllColors, Rng());

            for (int column = 0; column < 3; column++)
            {
                Assert.That(board.GetCell(0, column).Block, Is.SameAs(before[column]));
            }

            // And none of the survivors was reported as a spawn.
            foreach (Block survivor in before)
            {
                Assert.That(spawns.Any(s => ReferenceEquals(s.Block, survivor)), Is.False);
            }
        }

        [Test]
        public void Refill_ReturnsOneSpawnPerEmptyCell()
        {
            Board board = BoardLayout.Build(
                ". . .",
                ". R .",
                "R . B");

            int holesBefore = board.Rows * board.Columns - CountOccupied(board);
            int occupiedBefore = CountOccupied(board);

            IReadOnlyList<BlockSpawn> spawns = RefillResolver.ApplyRefill(board, AllColors, Rng());

            Assert.That(holesBefore, Is.EqualTo(6));
            Assert.That(spawns, Has.Count.EqualTo(holesBefore));
            Assert.That(CountOccupied(board), Is.EqualTo(occupiedBefore + holesBefore));

            // Each hole is reported exactly once.
            Assert.That(spawns.Select(s => s.Position).Distinct().Count(), Is.EqualTo(spawns.Count));
        }

        [Test]
        public void SpawnRecordsReferenceBlocksActuallyStoredInBoard()
        {
            Board board = BoardLayout.Build(
                ". . .",
                ". . .");

            IReadOnlyList<BlockSpawn> spawns = RefillResolver.ApplyRefill(board, AllColors, Rng());

            foreach (BlockSpawn spawn in spawns)
            {
                Assert.That(board.GetCell(spawn.Position).Block, Is.SameAs(spawn.Block),
                    $"{spawn} does not match the block actually in the board.");
            }
        }

        [Test]
        public void SpawnedColorsComeOnlyFromAvailableColors()
        {
            IReadOnlyList<BlockColor> allowed = new[] { BlockColor.Red, BlockColor.Blue };
            Board board = BoardLayout.Build(
                ". . . .",
                ". . . .",
                ". . . .");

            IReadOnlyList<BlockSpawn> spawns = RefillResolver.ApplyRefill(board, allowed, Rng());

            Assert.That(spawns, Has.Count.EqualTo(12));
            foreach (BlockSpawn spawn in spawns)
            {
                Assert.That(allowed, Has.Member(spawn.Block.Color));
            }
        }

        [Test]
        public void Refill_WithOneAvailableColor_UsesThatColor()
        {
            IReadOnlyList<BlockColor> onlyGreen = new[] { BlockColor.Green };
            Board board = BoardLayout.Build(
                ". .",
                ". .");

            IReadOnlyList<BlockSpawn> spawns = RefillResolver.ApplyRefill(board, onlyGreen, Rng());

            Assert.That(spawns, Has.Count.EqualTo(4));
            Assert.That(spawns.All(s => s.Block.Color == BlockColor.Green), Is.True);
        }

        [Test]
        public void Refill_WithNoAvailableColors_FailsClearly()
        {
            Board board = BoardLayout.Build(". .");

            Assert.Throws<ArgumentException>(
                () => RefillResolver.ApplyRefill(board, Array.Empty<BlockColor>(), Rng()));
            Assert.Throws<ArgumentNullException>(
                () => RefillResolver.ApplyRefill(board, null, Rng()));
            Assert.Throws<ArgumentNullException>(
                () => RefillResolver.ApplyRefill(board, AllColors, null));
        }

        [Test]
        public void Refill_WithSameSeedAndSameBoard_IsReproducible()
        {
            string First() => RunAndDescribe(new Random(Seed));
            string Second() => RunAndDescribe(new Random(Seed));

            Assert.That(First(), Is.EqualTo(Second()));
            Assert.That(RunAndDescribe(new Random(Seed)), Is.Not.EqualTo(RunAndDescribe(new Random(999))));
        }

        [Test]
        public void Refill_RestoresFullBoardAfterGravity()
        {
            // The whole loop: blast, settle, refill.
            Board board = BoardLayout.Build(
                "R R B",
                "R B B",
                "R R G");

            int cells = board.Rows * board.Columns;
            Assert.That(CountOccupied(board), Is.EqualTo(cells));

            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, At(2, 0));
            IReadOnlyList<Block> removed = GroupRemover.TryRemoveGroup(board, group, 2);
            Assert.That(CountOccupied(board), Is.EqualTo(cells - removed.Count));

            IReadOnlyList<BlockMove> moves = GravityResolver.ApplyGravity(board);
            Assert.That(CountOccupied(board), Is.EqualTo(cells - removed.Count),
                "Gravity moves blocks but must not change how many there are.");
            Assert.That(moves, Is.Not.Empty);

            IReadOnlyList<BlockSpawn> spawns = RefillResolver.ApplyRefill(board, AllColors, Rng());

            // Spawn count equals removed count, because gravity changed positions, not count.
            Assert.That(spawns, Has.Count.EqualTo(removed.Count));
            Assert.That(CountOccupied(board), Is.EqualTo(cells));

            // Every cell holds exactly one block, and no removed instance came back.
            for (int row = 0; row < board.Rows; row++)
            {
                for (int column = 0; column < board.Columns; column++)
                {
                    Assert.That(board.GetCell(row, column).IsEmpty, Is.False);
                }
            }

            foreach (Block gone in removed)
            {
                Assert.That(spawns.Any(s => ReferenceEquals(s.Block, gone)), Is.False,
                    "A removed Block instance must not be recycled into a spawn.");
            }
        }

        private static string RunAndDescribe(Random random)
        {
            Board board = BoardLayout.Build(
                ". . . .",
                ". . . .",
                ". . . .");

            RefillResolver.ApplyRefill(board, AllColors, random);
            return Describe(board);
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
            var text = new System.Text.StringBuilder();

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
