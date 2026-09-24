using System;
using System.Collections.Generic;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;

namespace BlastPuzzle.PowerUps
{
    // Decides whether a blasted group earns a power-up, and which one.
    //
    // The thresholds live here and nowhere else. Scattering "if (group.Count >= 5)" through
    // the controller and the view would mean three places to change when the numbers are
    // tuned, and no single place to test them. These are gameplay rules rather than level
    // configuration for now; moving them onto LevelDefinition later is a change to this
    // class's callers, not to the rule itself.
    //
    // Pure C#: no UnityEngine.
    public static class PowerUpRules
    {
        public const int RocketThreshold = 5;
        public const int BombThreshold = 7;

        // Returns the power-up this group earns, or null for an ordinary blast.
        //
        //   2-4  nothing
        //   5-6  Rocket
        //   7+   Bomb
        //
        // The colour is carried over from the group purely so the view can tint it; it never
        // counts toward a colour goal.
        public static Block TryCreatePowerUp(IReadOnlyList<BoardPosition> group, BlockColor color)
        {
            if (group == null)
            {
                throw new ArgumentNullException(nameof(group));
            }

            if (group.Count >= BombThreshold)
            {
                // Clears a plus shape: its own cell and the four cells sharing an edge.
                return Block.CreateBomb(color);
            }

            if (group.Count >= RocketThreshold)
            {
                return Block.CreateRocket(color, DirectionFor(group));
            }

            return null;
        }

        // Deterministic, with no randomness: the group's BOUNDING BOX decides.
        //
        //   width >= height  ->  Horizontal
        //   otherwise        ->  Vertical
        //
        // A wide, flat group produces a rocket that flies along its long axis, which reads
        // naturally. The tie (a square group, width == height) resolves to Horizontal --
        // stated explicitly so it is a documented rule rather than an accident of the
        // comparison operator, and so a test can pin it.
        public static RocketDirection DirectionFor(IReadOnlyList<BoardPosition> group)
        {
            if (group == null)
            {
                throw new ArgumentNullException(nameof(group));
            }

            if (group.Count == 0)
            {
                throw new ArgumentException("An empty group has no shape.", nameof(group));
            }

            int minRow = int.MaxValue, maxRow = int.MinValue;
            int minColumn = int.MaxValue, maxColumn = int.MinValue;

            foreach (BoardPosition position in group)
            {
                minRow = Math.Min(minRow, position.Row);
                maxRow = Math.Max(maxRow, position.Row);
                minColumn = Math.Min(minColumn, position.Column);
                maxColumn = Math.Max(maxColumn, position.Column);
            }

            int width = maxColumn - minColumn + 1;
            int height = maxRow - minRow + 1;

            return width >= height ? RocketDirection.Horizontal : RocketDirection.Vertical;
        }
    }
}
