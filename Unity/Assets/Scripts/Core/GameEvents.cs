using System.Collections.Generic;

namespace StardustAtelier.Core
{
    public enum GameEventKind
    {
        Swapped, SwapRejected, CellHit, BlockCleared, SpecialCreated, SpecialActivated,
        IceDamaged, ObstacleDamaged, ChainBroken, Fell, Spawned, RelicCollected,
        VineSpread, Shuffled, GoalChanged, MovesChanged, StepEnded, TurnEnded,
        // GaugeChanged: index = 파티 슬롯, value = 게이지 단위. SkillUsed: index = 슬롯.
        GaugeChanged, SkillUsed
    }
    public enum SessionOutcome { Idle, Clear, Fail }
    public sealed class GameEvent
    {
        public GameEventKind kind;
        public CellPosition position, other;
        public Occupant occupant;
        public Cell cell;
        public Board board;
        public int index, value;
        public GameEvent(GameEventKind type, CellPosition at) { kind = type; position = at; }
    }
    public sealed class GoalTracker
    {
        private readonly GoalData[] goals;
        private readonly int[] remaining;
        public GoalTracker(GoalData[] data)
        {
            goals = data; remaining = new int[data.Length];
            for (int i = 0; i < data.Length; i++) remaining[i] = data[i].count;
        }
        public int Count { get { return goals.Length; } }
        public int Remaining(int index) { return remaining[index]; }
        public GoalData Goal(int index) { return goals[index]; }
        public int[] Amounts { get { return (int[])remaining.Clone(); } }
        public bool Complete
        {
            get { foreach (int count in remaining) if (count > 0) return false; return true; }
        }
        // 색 블록이 제거되거나 아이템으로 바뀌는 순간 한 번 불립니다. 파티 게이지 충전(12.3)에 씁니다.
        public System.Action<BlockColor,List<GameEvent>> ColorCollected;
        public void Record(string type, BlockColor color, List<GameEvent> events)
        {
            if (type == "Collect" && ColorCollected != null) ColorCollected(color,events);
            for (int i = 0; i < goals.Length; i++)
            {
                if (remaining[i] == 0 || goals[i].type != type) continue;
                if (type == "Collect" && goals[i].color != color.ToString()) continue;
                remaining[i]--;
                events.Add(new GameEvent(GameEventKind.GoalChanged,default(CellPosition)) { index = i, value = remaining[i] });
            }
        }
    }
}
