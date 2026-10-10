using System.Collections.Generic;

namespace StardustAtelier.Core
{
    public enum GravityEventKind { Fell, Spawned, RelicCollected }
    public struct GravityEvent
    {
        public GravityEventKind kind;
        public CellPosition from, to;
        public Occupant occupant;
        public GravityEvent(GravityEventKind type, CellPosition source, CellPosition destination, Occupant value)
        { kind = type; from = source; to = destination; occupant = value; }
    }

    public static class Gravity
    {
        public static List<GravityEvent> Apply(Board board, string[] colors, ref Pcg32 refill, bool assist, RuleConfig rules, bool squareMatch)
        {
            List<GravityEvent> events = new List<GravityEvent>();
            // [실제 코드에 남길 주석] 유물을 모두 수집한 뒤 리필합니다. 구멍은 낙하를 막지 않습니다.
            bool collected;
            do
            {
                Collapse(board,events);
                collected = false;
                for (int c = 0; c < board.Width; c++)
                {
                    int bottom = board.Height - 1;
                    while (bottom >= 0 && !board[bottom,c].exists) bottom--;
                    if (bottom < 0 || board[bottom,c].occupant.kind != OccupantKind.Relic) continue;
                    CellPosition p = new CellPosition(bottom,c);
                    events.Add(new GravityEvent(GravityEventKind.RelicCollected,p,p,board[p].occupant));
                    board[p].occupant = Occupant.Empty; collected = true;
                }
            } while (collected);
            for (int c = 0; c < board.Width; c++)
            {
                List<CellPosition> topSegment = new List<CellPosition>();
                int firstValid = -1;
                for (int r = 0; r < board.Height; r++)
                {
                    if (!board[r,c].exists) continue;
                    if (firstValid < 0) firstValid = r;
                    if (board[r,c].IsFixed) break;
                    topSegment.Add(new CellPosition(r,c));
                }
                for (int i = topSegment.Count - 1; i >= 0; i--)
                {
                    CellPosition p = topSegment[i];
                    if (board[p].occupant.kind != OccupantKind.Empty) continue;
                    bool chooseMatch = assist && refill.NextInt(10000) < (int)(rules.assistRefillRate * 10000);
                    List<BlockColor> candidates = new List<BlockColor>();
                    if (chooseMatch)
                        foreach (string code in colors)
                        {
                            BlockColor color = BoardGenerator.ParseColor(code[0]);
                            board[p].occupant = Occupant.Block(color);
                            foreach (MatchGroup group in MatchFinder.Find(board, squareMatch))
                                if (group.Contains(p)) { candidates.Add(color); break; }
                        }
                    board[p].occupant = Occupant.Empty;
                    if (candidates.Count == 0) foreach (string code in colors) candidates.Add(BoardGenerator.ParseColor(code[0]));
                    Occupant value = Occupant.Block(candidates[refill.NextInt(candidates.Count)]);
                    board[p].occupant = value;
                    events.Add(new GravityEvent(GravityEventKind.Spawned,new CellPosition(firstValid-1,c),p,value));
                }
            }
            return events;
        }

        public static void Collapse(Board board, List<GravityEvent> events)
        {
            for (int c = 0; c < board.Width; c++)
            {
                List<CellPosition> segment = new List<CellPosition>();
                for (int r = board.Height - 1; r >= -1; r--)
                {
                    if (r >= 0 && !board[r,c].exists) continue;
                    if (r < 0 || board[r,c].IsFixed)
                    {
                        Compact(board,segment,events); segment.Clear();
                    }
                    else segment.Add(new CellPosition(r,c));
                }
            }
        }
        private static void Compact(Board board, List<CellPosition> segment, List<GravityEvent> events)
        {
            int target = 0;
            foreach (CellPosition source in segment)
            {
                Occupant value = board[source].occupant;
                if (value.kind == OccupantKind.Empty) continue;
                CellPosition destination = segment[target++];
                if (source.Equals(destination)) continue;
                board[destination].occupant = value; board[source].occupant = Occupant.Empty;
                events.Add(new GravityEvent(GravityEventKind.Fell,source,destination,value));
            }
        }
    }
}
