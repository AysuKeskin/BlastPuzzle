using System;
using BlastPuzzle.Blocks;
using BlastPuzzle.Boards;

namespace BlastPuzzle.Tests.EditMode
{
    // Builds a Board from text so each test reads like the board it describes.
    //
    // Lines are given TOP ROW FIRST, because that is how a board looks written down, while
    // the domain puts row 0 at the bottom. This helper does the flip in one place so no
    // individual test has to think about it.
    internal static class BoardLayout
    {
        internal const char EmptyCell = '.';

        internal static Board Build(params string[] rowsTopFirst)
        {
            if (rowsTopFirst == null || rowsTopFirst.Length == 0)
            {
                throw new ArgumentException("A layout needs at least one row.", nameof(rowsTopFirst));
            }

            int rows = rowsTopFirst.Length;
            int columns = Strip(rowsTopFirst[0]).Length;
            var board = new Board(rows, columns);

            for (int line = 0; line < rows; line++)
            {
                string text = Strip(rowsTopFirst[line]);

                if (text.Length != columns)
                {
                    throw new ArgumentException($"Row '{rowsTopFirst[line]}' has {text.Length} cells, expected {columns}.");
                }

                // First line is the TOP row, which is the highest row index.
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

        // Spaces are allowed in layout strings purely so tests can be written "R R B".
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
