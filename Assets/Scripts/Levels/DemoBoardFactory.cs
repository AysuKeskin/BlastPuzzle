using System;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;

namespace BlastPuzzle.Levels
{
    // TEMPORARY -- milestone scaffolding only.
    //
    // Not the real board generator: Milestone 7 (RefillResolver) owns block generation and
    // Milestone 10 (LevelDefinition) owns level layout. When those arrive, delete this file
    // and the one call in GameBootstrap.
    //
    // Deliberately hand-authored and deterministic rather than random, so group sizes can
    // be checked by eye against the screen. The earlier (row + column) % 5 pattern was
    // replaced because orthogonal neighbours always differ by one in (row + column), so no
    // two adjacent blocks ever shared a colour and every group was size 1 -- useless for
    // exercising connected-group detection.
    //
    // Plain C#: it builds domain objects, so it has no business knowing Unity.
    public static class DemoBoardFactory
    {
        private const char EmptyCell = '.';

        // Written TOP ROW FIRST, the way the board looks on screen. Row 0 is the bottom,
        // so this array is flipped during construction.
        //
        // Contains on purpose: several multi-block regions, a region that bends around a
        // corner, an isolated single block (Green, bottom-left), and two empty cells in the
        // top-left so "an empty cell produces no BlockView" stays visible.
        private static readonly string[] Layout =
        {
            ". . G G Y Y P P",
            "R R G G Y Y P P",
            "R R R B B Y Y P",
            "R R B B B B Y P",
            "Y Y B P B B G G",
            "Y P P P P B G G",
            "Y Y P R R B B G",
            "G Y P R R R B B"
        };

        public static Board CreateDemoBoard()
        {
            int rows = Layout.Length;
            int columns = Strip(Layout[0]).Length;
            var board = new Board(rows, columns);

            for (int line = 0; line < rows; line++)
            {
                string text = Strip(Layout[line]);

                // The first line is the TOP row, which is the highest row index.
                int row = rows - 1 - line;

                for (int column = 0; column < columns; column++)
                {
                    char symbol = text[column];

                    if (symbol == EmptyCell)
                    {
                        continue;
                    }

                    board.SetBlock(new BoardPosition(row, column), new Block(ToColor(symbol)));
                }
            }

            return board;
        }

        private static string Strip(string row) => row.Replace(" ", string.Empty);

        private static BlockColor ToColor(char symbol) => symbol switch
        {
            'R' => BlockColor.Red,
            'B' => BlockColor.Blue,
            'G' => BlockColor.Green,
            'Y' => BlockColor.Yellow,
            'P' => BlockColor.Purple,
            _ => throw new ArgumentException($"Unknown block symbol '{symbol}'.", nameof(symbol))
        };
    }
}
