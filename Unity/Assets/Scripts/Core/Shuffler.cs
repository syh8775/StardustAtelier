using System;
using System.Collections.Generic;

namespace StardustAtelier.Core
{
    public static class Shuffler
    {
        public static bool Shuffle(Board board, LevelData level, RuleConfig rules, ref Pcg32 random)
        {
            List<CellPosition> cells = new List<CellPosition>();
            foreach (CellPosition p in board.Positions()) if (board[p].CanSwap) cells.Add(p);
            for (int attempt = 0; attempt < rules.shuffleMaxTries; attempt++)
            {
                for (int i = cells.Count-1; i > 0; i--) board.Swap(cells[i],cells[random.NextInt(i+1)]);
                if (BoardGenerator.IsStableStart(board,level.features.squareMatch)) return true;
            }
            // [실제 코드에 남길 주석] 셔플 실패 시 색 블록만 재생성하고 특수 블록·층은 보존합니다.
            for (int attempt = 0; attempt < rules.regenerateMaxTries; attempt++)
            {
                foreach (CellPosition p in cells)
                    if (board[p].occupant.kind == OccupantKind.ColorBlock) board[p].occupant = Occupant.Empty;
                foreach (CellPosition p in cells)
                {
                    if (board[p].occupant.kind != OccupantKind.Empty) continue;
                    List<BlockColor> candidates = new List<BlockColor>();
                    foreach (string code in level.colors)
                    {
                        BlockColor color = BoardGenerator.ParseColor(code[0]);
                        board[p].occupant = Occupant.Block(color);
                        bool matched = false;
                        foreach (MatchGroup group in MatchFinder.Find(board,level.features.squareMatch)) if (group.Contains(p)) matched = true;
                        if (!matched) candidates.Add(color);
                    }
                    if (candidates.Count == 0) foreach (string code in level.colors) candidates.Add(BoardGenerator.ParseColor(code[0]));
                    board[p].occupant = Occupant.Block(candidates[random.NextInt(candidates.Count)]);
                }
                if (BoardGenerator.IsStableStart(board,level.features.squareMatch)) return true;
            }
            List<CellPosition> emergency = new List<CellPosition>();
            foreach (CellPosition p in cells)
                if (board[p].occupant.kind == OccupantKind.ColorBlock)
                    foreach (CellPosition q in BoardGenerator.Neighbors(p))
                        if (board.Contains(q) && board[q].CanSwap) { emergency.Add(p); break; }
            if (emergency.Count == 0) return false;
            board[emergency[random.NextInt(emergency.Count)]].occupant = new Occupant { kind = OccupantKind.Prism, color = BlockColor.None };
            return true;
        }
    }
}
