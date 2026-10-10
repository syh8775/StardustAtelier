using System;
using System.Collections.Generic;

namespace StardustAtelier.Core
{
    public enum MatchShape { Horizontal, Vertical, Square }
    public sealed class MatchPattern
    {
        public MatchShape shape;
        public BlockColor color;
        public List<CellPosition> cells = new List<CellPosition>();
    }
    public sealed class MatchGroup
    {
        public BlockColor color;
        public List<CellPosition> cells = new List<CellPosition>();
        public List<MatchPattern> patterns = new List<MatchPattern>();
        public bool Contains(CellPosition p) { return cells.Contains(p); }
        public Occupant GetSpecial()
        {
            bool horizontal = false, vertical = false, square = false;
            foreach (MatchPattern pattern in patterns)
            {
                if (pattern.shape != MatchShape.Square && pattern.cells.Count >= 5)
                    return new Occupant { kind = OccupantKind.Prism, color = BlockColor.None };
                horizontal |= pattern.shape == MatchShape.Horizontal;
                vertical |= pattern.shape == MatchShape.Vertical;
                square |= pattern.shape == MatchShape.Square;
            }
            // 만들어지는 아이템은 묶음 색을 이어받지 않습니다(색 없음).
            if (horizontal && vertical) return new Occupant { kind = OccupantKind.Bomb, color = BlockColor.None };
            foreach (MatchPattern pattern in patterns)
                if (pattern.shape != MatchShape.Square && pattern.cells.Count == 4)
                    return new Occupant { kind = OccupantKind.Rocket, color = BlockColor.None,
                        orientation = pattern.shape == MatchShape.Horizontal ? Orientation.H : Orientation.V };
            if (square) return new Occupant { kind = OccupantKind.Meteor, color = BlockColor.None };
            return Occupant.Empty;
        }
        public CellPosition? GetCreationPosition(Board board, CellPosition? swappedA = null, CellPosition? swappedB = null)
        {
            // [실제 코드에 남길 주석] 스왑 기준 칸이 없으면 사슬 없는 최하단·최좌측 칸을 선택합니다.
            if (swappedB.HasValue && Contains(swappedB.Value) && !board[swappedB.Value].chained) return swappedB;
            if (swappedA.HasValue && Contains(swappedA.Value) && !board[swappedA.Value].chained) return swappedA;
            CellPosition? result = null;
            foreach (CellPosition p in cells)
                if (!board[p].chained && (!result.HasValue || p.r > result.Value.r || (p.r == result.Value.r && p.c < result.Value.c)))
                    result = p;
            return result;
        }
    }

    public static class MatchFinder
    {
        public static List<MatchGroup> Find(Board board, bool squareMatch)
        {
            List<MatchPattern> patterns = new List<MatchPattern>();
            // [실제 코드에 남길 주석] 탐색 순서가 로켓 방향과 이벤트 순서의 결정 기준입니다.
            for (int r = 0; r < board.Height; r++) FindLine(board, r, true, patterns);
            for (int c = 0; c < board.Width; c++) FindLine(board, c, false, patterns);
            if (squareMatch)
                for (int r = 0; r < board.Height - 1; r++)
                    for (int c = 0; c < board.Width - 1; c++)
                    {
                        CellPosition p = new CellPosition(r, c);
                        if (!board[p].CanMatch) continue;
                        BlockColor color = board[p].occupant.color;
                        CellPosition[] corners = { p, new CellPosition(r,c+1), new CellPosition(r+1,c), new CellPosition(r+1,c+1) };
                        bool matches = true;
                        foreach (CellPosition q in corners) matches &= board[q].CanMatch && board[q].occupant.color == color;
                        if (matches) patterns.Add(new MatchPattern { shape = MatchShape.Square, color = color, cells = new List<CellPosition>(corners) });
                    }
            int[] roots = new int[patterns.Count];
            for (int i = 0; i < roots.Length; i++) roots[i] = i;
            for (int i = 0; i < patterns.Count; i++)
                for (int j = i + 1; j < patterns.Count; j++)
                    if (Overlaps(patterns[i], patterns[j])) roots[Root(roots, j)] = Root(roots, i);
            List<MatchGroup> groups = new List<MatchGroup>();
            List<int> groupRoots = new List<int>();
            for (int i = 0; i < patterns.Count; i++)
            {
                int root = Root(roots, i), index = groupRoots.IndexOf(root);
                if (index < 0) { index = groups.Count; groupRoots.Add(root); groups.Add(new MatchGroup { color = patterns[i].color }); }
                MatchGroup group = groups[index]; group.patterns.Add(patterns[i]);
                foreach (CellPosition p in patterns[i].cells) if (!group.cells.Contains(p)) group.cells.Add(p);
            }
            foreach (MatchGroup group in groups) group.cells.Sort(ReadingOrder);
            groups.Sort((a,b) => ReadingOrder(a.cells[0], b.cells[0]));
            return groups;
        }
        public static int ReadingOrder(CellPosition a, CellPosition b) { int row = a.r.CompareTo(b.r); return row != 0 ? row : a.c.CompareTo(b.c); }
        private static int Root(int[] roots, int index) { while (roots[index] != index) index = roots[index]; return index; }
        private static bool Overlaps(MatchPattern a, MatchPattern b)
        {
            if (a.color != b.color) return false;
            foreach (CellPosition p in a.cells) if (b.cells.Contains(p)) return true;
            return false;
        }
        private static void FindLine(Board board, int line, bool horizontal, List<MatchPattern> result)
        {
            int length = horizontal ? board.Width : board.Height, start = 0;
            while (start < length)
            {
                CellPosition first = horizontal ? new CellPosition(line,start) : new CellPosition(start,line);
                if (!board[first].CanMatch) { start++; continue; }
                BlockColor color = board[first].occupant.color;
                int end = start + 1;
                while (end < length)
                {
                    CellPosition p = horizontal ? new CellPosition(line,end) : new CellPosition(end,line);
                    if (!board[p].CanMatch || board[p].occupant.color != color) break;
                    end++;
                }
                if (end - start >= 3)
                {
                    MatchPattern pattern = new MatchPattern { color = color, shape = horizontal ? MatchShape.Horizontal : MatchShape.Vertical };
                    for (int i = start; i < end; i++) pattern.cells.Add(horizontal ? new CellPosition(line,i) : new CellPosition(i,line));
                    result.Add(pattern);
                }
                start = end;
            }
        }
    }
}
