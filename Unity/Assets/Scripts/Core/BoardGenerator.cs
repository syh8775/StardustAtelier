using System;
using System.Collections.Generic;

namespace StardustAtelier.Core
{
    public sealed class GeneratedBoard
    {
        public Board board;
        public bool emergency;
    }
    public static class BoardGenerator
    {
        public static GeneratedBoard Generate(LevelData level, RuleConfig rules, ref Pcg32 generation, ref Pcg32 shuffle)
        {
            Board board = CreateLayers(level);
            bool random = false;
            foreach (string row in level.layers.occupant) random |= row.IndexOf('?') >= 0;
            if (!random)
            {
                if (!IsStableStart(board,level.features.squareMatch)) throw new InvalidOperationException("Fixed board violates start conditions: " + level.id);
                return new GeneratedBoard { board = board };
            }
            for (int attempt = 0; attempt < rules.generateMaxTries; attempt++)
            {
                foreach (CellPosition p in board.Positions())
                    if (level.layers.occupant[p.r][p.c] == '?') board[p].occupant = Occupant.Empty;
                foreach (CellPosition p in board.Positions())
                    if (level.layers.occupant[p.r][p.c] == '?')
                    {
                        List<BlockColor> candidates = new List<BlockColor>();
                        foreach (string value in level.colors)
                        {
                            BlockColor color = ParseColor(value[0]);
                            bool forbidden = p.c >= 2 && Same(board,p.r,p.c-1,color) && Same(board,p.r,p.c-2,color);
                            forbidden |= p.r >= 2 && Same(board,p.r-1,p.c,color) && Same(board,p.r-2,p.c,color);
                            forbidden |= level.features.squareMatch && p.r > 0 && p.c > 0 &&
                                Same(board,p.r,p.c-1,color) && Same(board,p.r-1,p.c,color) && Same(board,p.r-1,p.c-1,color);
                            if (!forbidden) candidates.Add(color);
                        }
                        if (candidates.Count == 0) foreach (string value in level.colors) candidates.Add(ParseColor(value[0]));
                        board[p].occupant = Occupant.Block(candidates[generation.NextInt(candidates.Count)]);
                    }
                if (IsStableStart(board,level.features.squareMatch)) return new GeneratedBoard { board = board };
            }
            List<CellPosition> emergencyCandidates = new List<CellPosition>();
            foreach (CellPosition p in board.Positions())
            {
                if (!board[p].CanSwap || board[p].occupant.kind != OccupantKind.ColorBlock) continue;
                foreach (CellPosition q in Neighbors(p))
                    if (board.Contains(q) && board[q].CanSwap) { emergencyCandidates.Add(p); break; }
            }
            if (emergencyCandidates.Count == 0) throw new InvalidOperationException("No adjacent swappable cells: " + level.id);
            board[emergencyCandidates[shuffle.NextInt(emergencyCandidates.Count)]].occupant =
                new Occupant { kind = OccupantKind.Prism, color = BlockColor.None };
            return new GeneratedBoard { board = board, emergency = true };
        }
        public static bool IsStableStart(Board board, bool squareMatch) { return MatchFinder.Find(board,squareMatch).Count == 0 && MoveFinder.Find(board,squareMatch).Count > 0; }
        public static BlockColor ParseColor(char value)
        {
            switch (value) { case 'R': return BlockColor.R; case 'B': return BlockColor.B; case 'G': return BlockColor.G; case 'Y': return BlockColor.Y; case 'P': return BlockColor.P; default: throw new ArgumentException("Unknown color: " + value); }
        }
        public static Board CreateLayers(LevelData level)
        {
            Board board = new Board(level.width,level.height);
            foreach (CellPosition p in board.Positions())
            {
                Cell cell = board[p];
                cell.exists = level.layers.mask[p.r][p.c] != '#';
                cell.ice = level.layers.ice[p.r][p.c] - '0';
                cell.chained = level.layers.chain[p.r][p.c] == 'C';
                char token = level.layers.occupant[p.r][p.c];
                if ("RBGYP".IndexOf(token) >= 0) cell.occupant = Occupant.Block(ParseColor(token));
                else if (token == 'X') cell.occupant = new Occupant { kind = OccupantKind.Box, color = BlockColor.None, hp = 1 };
                else if (token == 'V') cell.occupant = new Occupant { kind = OccupantKind.Vine, color = BlockColor.None, hp = 1 };
                else if (token == 'O') cell.occupant = new Occupant { kind = OccupantKind.Relic, color = BlockColor.None };
            }
            return board;
        }
        public static IEnumerable<CellPosition> Neighbors(CellPosition p)
        {
            yield return new CellPosition(p.r-1,p.c);
            yield return new CellPosition(p.r,p.c-1);
            yield return new CellPosition(p.r,p.c+1);
            yield return new CellPosition(p.r+1,p.c);
        }
        private static bool Same(Board board, int r, int c, BlockColor color) { return board[r,c].CanMatch && board[r,c].occupant.color == color; }
    }
}
