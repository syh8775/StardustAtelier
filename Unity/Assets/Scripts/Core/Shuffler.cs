using System;
using System.Collections.Generic;

namespace StardustAtelier.Core
{
    public static class Shuffler
    {
        // 셔플 대상 칸. 유물은 옮길 수 있지만 셔플 대상은 아닙니다(9.3).
        private static List<CellPosition> Cells(Board board)
        {
            List<CellPosition> cells = new List<CellPosition>();
            foreach (CellPosition p in board.Positions()) if (board[p].CanSwap && board[p].occupant.kind != OccupantKind.Relic) cells.Add(p);
            return cells;
        }
        // 한 번 섞습니다. 섞은 결과에 둘 수 있는 수가 있으면(매치는 없어야 함) true입니다.
        public static bool Permute(Board board, LevelData level, ref Pcg32 random)
        {
            List<CellPosition> cells = Cells(board);
            for (int i = cells.Count-1; i > 0; i--) board.Swap(cells[i],cells[random.NextInt(i+1)]);
            return BoardGenerator.IsStableStart(board,level.features.squareMatch);
        }
        // 섞기를 shuffleMaxTries번 하고도 수가 없으면 마지막 수단까지 씁니다(화면에 한 번만 보여 줄 때 쓰는 묶음).
        public static bool Shuffle(Board board, LevelData level, RuleConfig rules, ref Pcg32 random)
        {
            for (int attempt = 0; attempt < rules.shuffleMaxTries; attempt++)
                if (Permute(board,level,ref random)) return true;
            return LastResort(board,level,rules,ref random);
        }
        // 마지막 수단: 색 블록을 다시 깔고, 그래도 안 되면 블록 하나를 프리즘으로 바꿉니다. 이것도 안 되면 false(실패).
        public static bool LastResort(Board board, LevelData level, RuleConfig rules, ref Pcg32 random)
        {
            List<CellPosition> cells = Cells(board);
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
