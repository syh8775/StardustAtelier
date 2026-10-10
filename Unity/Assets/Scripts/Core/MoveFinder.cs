using System;
using System.Collections.Generic;

namespace StardustAtelier.Core
{
    public struct ValidMove
    {
        public CellPosition a, b;
        public ValidMove(CellPosition first, CellPosition second) { a = first; b = second; }
    }
    public static class MoveFinder
    {
        public static bool IsValid(Board board, CellPosition a, CellPosition b, bool squareMatch)
        {
            if (!board.Contains(a) || !board.Contains(b) || Math.Abs(a.r-b.r) + Math.Abs(a.c-b.c) != 1) return false;
            if (!board[a].CanSwap || !board[b].CanSwap) return false;
            Occupant first = board[a].occupant, second = board[b].occupant;
            if (first.IsSpecial && second.IsSpecial) return true;
            if ((first.kind == OccupantKind.Prism && second.kind == OccupantKind.ColorBlock) ||
                (second.kind == OccupantKind.Prism && first.kind == OccupantKind.ColorBlock)) return true;
            // 로켓·폭탄·별똥별은 색 블록과 바꾸면 매치가 없어도 도착 칸에서 발동합니다(기획서 4.2, 2026-10-09 변경).
            if (IsMovedSpecial(first,second) || IsMovedSpecial(second,first)) return true;
            // [실제 코드에 남길 주석] 수 탐색은 보드·난수 상태를 바꾸지 않는 조회입니다.
            board.Swap(a,b);
            try
            {
                foreach (MatchGroup group in MatchFinder.Find(board,squareMatch))
                    if (group.Contains(a) || group.Contains(b)) return true;
                return false;
            }
            finally { board.Swap(a,b); }
        }
        // 프리즘은 색 블록과 바꾸면 조합(지정 색)으로 처리하므로 여기서 제외합니다.
        public static bool IsMovedSpecial(Occupant special, Occupant other)
        // 유물도 색 블록처럼 자리를 내줍니다(2026-10-10 변경): 아이템을 유물 칸으로 옮기면 그 칸에서 발동하고 유물은 아이템이 있던 칸으로 갑니다.
        { return special.IsSpecial && special.kind != OccupantKind.Prism && (other.kind == OccupantKind.ColorBlock || other.kind == OccupantKind.Relic); }
        public static bool CanActivate(Board board, CellPosition p)
        // 얼음에 갇힌 특수 블록도 눌러서 발동할 수는 있습니다(옮기지만 못합니다).
        { return board.Contains(p) && board[p].exists && !board[p].chained && board[p].occupant.IsSpecial; }
        public static CellPosition? FindActivation(Board board)
        {
            foreach (CellPosition p in board.Positions()) if (CanActivate(board,p)) return p;
            return null;
        }
        // 단독 발동할 수 있는 특수 블록도 유효한 행동으로 보아 불필요한 셔플을 막습니다.
        public static bool HasAction(Board board, bool squareMatch)
        { return FindActivation(board).HasValue || Find(board,squareMatch).Count > 0; }
        public static List<ValidMove> Find(Board board, bool squareMatch)
        {
            List<ValidMove> result = new List<ValidMove>();
            foreach (CellPosition p in board.Positions())
            {
                CellPosition right = new CellPosition(p.r,p.c+1), below = new CellPosition(p.r+1,p.c);
                if (IsValid(board,p,right,squareMatch)) result.Add(new ValidMove(p,right));
                if (IsValid(board,p,below,squareMatch)) result.Add(new ValidMove(p,below));
            }
            return result;
        }
    }
}
