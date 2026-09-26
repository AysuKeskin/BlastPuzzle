using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;
using BlastPuzzle.Gameplay;
using BlastPuzzle.PowerUps;
using NUnit.Framework;

namespace BlastPuzzle.Tests.EditMode
{
    public sealed class PowerUpRulesTests
    {
        private static BoardPosition At(int row, int column) => new BoardPosition(row, column);

        // A horizontal run of n cells on row 0.
        private static List<BoardPosition> Row(int n)
        {
            var group = new List<BoardPosition>();
            for (int c = 0; c < n; c++)
            {
                group.Add(At(0, c));
            }

            return group;
        }

        private static List<BoardPosition> Column(int n)
        {
            var group = new List<BoardPosition>();
            for (int r = 0; r < n; r++)
            {
                group.Add(At(r, 0));
            }

            return group;
        }

        [Test]
        public void GroupSize2_DoesNotCreatePowerUp() =>
            Assert.That(PowerUpRules.TryCreatePowerUp(Row(2), BlockColor.Blue), Is.Null);

        [Test]
        public void GroupSize4_DoesNotCreatePowerUp() =>
            Assert.That(PowerUpRules.TryCreatePowerUp(Row(4), BlockColor.Blue), Is.Null);

        [Test]
        public void GroupSize5_CreatesRocket()
        {
            Block created = PowerUpRules.TryCreatePowerUp(Row(5), BlockColor.Blue);

            Assert.That(created, Is.Not.Null);
            Assert.That(created.Kind, Is.EqualTo(BlockKind.Rocket));
            Assert.That(created.Color, Is.EqualTo(BlockColor.Blue), "The power-up carries the group's colour for tinting.");
        }

        [Test]
        public void GroupSize6_CreatesRocket() =>
            Assert.That(PowerUpRules.TryCreatePowerUp(Row(6), BlockColor.Red).Kind, Is.EqualTo(BlockKind.Rocket));

        [Test]
        public void GroupSize7_CreatesBomb() =>
            Assert.That(PowerUpRules.TryCreatePowerUp(Row(7), BlockColor.Red).Kind, Is.EqualTo(BlockKind.Bomb));

        [Test]
        public void LargerGroup_CreatesBomb() =>
            Assert.That(PowerUpRules.TryCreatePowerUp(Row(20), BlockColor.Green).Kind, Is.EqualTo(BlockKind.Bomb));

        [Test]
        public void WideGroup_CreatesHorizontalRocket()
        {
            // 5 wide, 1 tall.
            Block rocket = PowerUpRules.TryCreatePowerUp(Row(5), BlockColor.Blue);
            Assert.That(rocket.Direction, Is.EqualTo(RocketDirection.Horizontal));
        }

        [Test]
        public void TallGroup_CreatesVerticalRocket()
        {
            // 1 wide, 5 tall.
            Block rocket = PowerUpRules.TryCreatePowerUp(Column(5), BlockColor.Blue);
            Assert.That(rocket.Direction, Is.EqualTo(RocketDirection.Vertical));
        }

        [Test]
        public void SquareGroup_UsesDocumentedTieRule()
        {
            var square = new List<BoardPosition>
            {
                At(0, 0), At(0, 1), At(0, 2),
                At(1, 0), At(1, 1), At(1, 2),
                At(2, 0), At(2, 1), At(2, 2)
            };

            Assert.That(PowerUpRules.DirectionFor(square), Is.EqualTo(RocketDirection.Horizontal),
                "width == height must resolve Horizontal by the documented tie rule.");
        }

        [Test]
        public void LShapedGroup_UsesBoundingBoxNotCellCount()
        {
            // Three cells tall, two wide: taller than wide, so Vertical.
            var lShape = new List<BoardPosition> { At(0, 0), At(1, 0), At(2, 0), At(2, 1), At(0, 1) };

            Assert.That(PowerUpRules.DirectionFor(lShape), Is.EqualTo(RocketDirection.Vertical));
        }
    }

    public sealed class PowerUpGroupInteractionTests
    {
        private static BoardPosition At(int row, int column) => new BoardPosition(row, column);

        [Test]
        public void ConnectedGroup_DoesNotTraverseRocket()
        {
            //  R R H R    H is a horizontal Rocket sitting between Reds
            Board board = BoardLayout.Build("R R H R");

            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, At(0, 0));

            Assert.That(group, Is.EquivalentTo(new[] { At(0, 0), At(0, 1) }));
            Assert.That(group, Has.No.Member(At(0, 3)), "The Red beyond the rocket is a separate group.");
        }

        [Test]
        public void ConnectedGroup_DoesNotTraverseBomb()
        {
            Board board = BoardLayout.Build("R R X R");

            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, At(0, 0));

            Assert.That(group, Is.EquivalentTo(new[] { At(0, 0), At(0, 1) }));
        }

        [Test]
        public void PowerUpWithSameStoredColor_IsNotPartOfNormalGroup()
        {
            Board board = BoardLayout.Build("R H R");

            IReadOnlyList<BoardPosition> group = ConnectedGroupFinder.FindConnectedGroup(board, At(0, 0));

            Assert.That(board.GetCell(0, 1).Block.Color, Is.EqualTo(BlockColor.Red), "Same stored colour...");
            Assert.That(group, Is.EquivalentTo(new[] { At(0, 0) }), "...and still not part of the group.");
        }

        [Test]
        public void SelectingPowerUpFindsNoNormalGroup()
        {
            Board board = BoardLayout.Build("R H R");

            Assert.That(ConnectedGroupFinder.FindConnectedGroup(board, At(0, 1)), Is.Empty);
        }
    }
}
