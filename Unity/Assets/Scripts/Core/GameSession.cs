using System;
using System.Collections.Generic;

namespace StardustAtelier.Core
{
    public sealed class GameSession
    {
        public Board Board { get; private set; }
        public LevelData Level { get; private set; }
        public GoalTracker Goals { get; private set; }
        public int MovesLeft { get; private set; }
        // 끝난 스왑 턴 수(단독 클릭 발동 포함). 스킬 난수 스트림 번호에도 씁니다(15.1).
        public int Turns { get; private set; }
        public SessionOutcome Outcome { get; private set; }
        // 파티(12장). 길이는 레벨의 partySlots이며 빈 슬롯은 null입니다.
        public PartyMember[] Party { get; private set; }
        public int[] Gauge { get; private set; }
        // 스킬 하나를 쓰면 파티 전체가 잠기고, 다음 스왑 턴의 턴 마무리 첫 단계에서 풀립니다(12.3, 7.2).
        public bool SkillLocked { get; private set; }
        // 이동 0이고 목표가 남았지만 쓸 수 있는 스킬이 있을 때(10.3). 스왑은 받지 않고 스킬·[종료]만 받습니다.
        public bool LastChance { get; private set; }
        private readonly GameConfig config;
        private readonly Resolver resolver;
        private readonly ulong seed;
        private Pcg32 refill, shuffle;
        private readonly bool assist;
        // 실패 보정(10.5)이 켜진 판인지. 플레이어에게는 표시하지 않습니다.
        public bool Assisted { get { return assist; } }
        // 스킬 턴 안에서는 충전이 절반이고, 발동한 캐릭터 자신은 충전하지 않습니다.
        private int skillSlot = -1;

        public GameSession(LevelData level, GameConfig settings, ulong seed, bool assisted = false)
            : this(level,settings,seed,new PartyMember[level.partySlots],assisted) { }
        public GameSession(LevelData level, GameConfig settings, ulong gameSeed, PartyMember[] party, bool assisted = false)
        {
            Level = level; config = settings; MovesLeft = level.moves; assist = assisted; seed = gameSeed;
            Pcg32 generation = new Pcg32(seed,1);
            refill = new Pcg32(seed,2); shuffle = new Pcg32(seed,4);
            Board = BoardGenerator.Generate(level,config.rules,ref generation,ref shuffle).board;
            Goals = new GoalTracker(level.goals); resolver = new Resolver(Board,Goals,seed);
            Party = party ?? new PartyMember[0];
            Gauge = new int[Party.Length];
            // 레벨 데이터의 startGauge에 있는 캐릭터는 가득 찬 상태로 시작합니다(레벨 8의 세이지).
            for (int i = 0; i < Party.Length; i++)
                if (Party[i] != null && level.startGauge != null && Array.IndexOf(level.startGauge,Party[i].character.id) >= 0)
                    Gauge[i] = Required(i);
            Goals.ColorCollected = Charge;
        }
        public int Stars
        {
            get
            {
                if (Outcome != SessionOutcome.Clear) return 0;
                if (MovesLeft >= StarMoves(Level.moves,config.rules.star3MovesRatio)) return 3;
                return MovesLeft >= StarMoves(Level.moves,config.rules.star2MovesRatio) ? 2 : 1;
            }
        }
        // 별을 받으려면 남겨야 하는 이동 수(올림). 진입 팝업의 별 기준 안내와 같은 계산을 씁니다.
        // 0.2f 같은 비율은 float 오차로 20×0.2가 4.0000001이 되어 5로 올림되므로, 아주 작은 값을 빼고 올립니다.
        public static int StarMoves(int moves, float ratio) { return (int)Math.Ceiling(moves*(double)ratio-0.0001); }
        public List<GameEvent> Swap(CellPosition a, CellPosition b)
        {
            List<GameEvent> events = new List<GameEvent>();
            if (Outcome != SessionOutcome.Idle || MovesLeft <= 0) return events;
            if (!Board.Contains(a) || !Board.Contains(b) || !Board[a].CanSwap || !Board[b].CanSwap ||
                Math.Abs(a.r-b.r)+Math.Abs(a.c-b.c) != 1) return events;
            bool valid = MoveFinder.IsValid(Board,a,b,Level.features.squareMatch);
            events.Add(new GameEvent(GameEventKind.Swapped,a) { other = b });
            if (!valid)
            {
                events.Add(new GameEvent(GameEventKind.SwapRejected,a) { other = b });
                return events;
            }
            bool combo = Board[a].occupant.IsSpecial && Board[b].occupant.IsSpecial ||
                Board[a].occupant.kind == OccupantKind.Prism || Board[b].occupant.kind == OccupantKind.Prism;
            // 교환 전 칸을 기준으로 옮겨진 특수 블록이 어디에 도착하는지 정합니다. a의 특수 블록은 b에 도착합니다.
            CellPosition? moved = null;
            if (!combo && MoveFinder.IsMovedSpecial(Board[a].occupant,Board[b].occupant)) moved = b;
            else if (!combo && MoveFinder.IsMovedSpecial(Board[b].occupant,Board[a].occupant)) moved = a;
            MovesLeft--; Turns++;
            events.Insert(0,new GameEvent(GameEventKind.MovesChanged,default(CellPosition)) { value = MovesLeft });
            Board.Swap(a,b); resolver.BeginTurn();
            if (combo) { resolver.ResolveCombo(a,b,events); ApplyGravity(events); }
            // 이동 발동은 단독 효과를 첫 행동으로 처리하므로 스왑 기준 칸(4.4)을 쓰지 않습니다.
            else if (moved.HasValue) { resolver.Activate(moved.Value,events); ApplyGravity(events); }
            bool plain = !combo && !moved.HasValue;
            ResolveTurn(events,plain ? (CellPosition?)a : null,plain ? (CellPosition?)b : null,false);
            return events;
        }
        // 단독 발동은 교환 없이 이동 한 번을 소비하고 같은 턴 종료 판정을 사용합니다.
        public List<GameEvent> Activate(CellPosition p)
        {
            List<GameEvent> events = new List<GameEvent>();
            if (Outcome != SessionOutcome.Idle || MovesLeft <= 0 || !MoveFinder.CanActivate(Board,p)) return events;
            MovesLeft--; Turns++;
            events.Add(new GameEvent(GameEventKind.MovesChanged,default(CellPosition)) { value = MovesLeft });
            resolver.BeginTurn(); resolver.Activate(p,events); ApplyGravity(events);
            ResolveTurn(events,null,null,false);
            return events;
        }

        // ---- 파티 스킬(12장) ----
        public int Required(int slot) { return Party[slot].RequiredUnits(config.rules.gaugeUnitsPerBlock); }
        public SkillState GetSkillState(int slot)
        {
            if (slot < 0 || slot >= Party.Length || Party[slot] == null) return SkillState.Empty;
            if (Gauge[slot] < Required(slot)) return SkillState.Charging;
            if (SkillLocked) return SkillState.Locked;
            if (SkillTargets.NeedsTarget(Party[slot].skill) && SkillTargetsFor(slot).Count == 0) return SkillState.NoTarget;
            return SkillState.Ready;
        }
        public bool AnySkillReady()
        {
            for (int i = 0; i < Party.Length; i++) if (GetSkillState(i) == SkillState.Ready) return true;
            return false;
        }
        // 미리보기를 열 때마다 PCG32(판 시드, 100 + 스왑 턴 수 × 3 + 슬롯)를 새로 만듭니다.
        // 그래서 보드가 그대로면 다시 열어도 같은 대상이 나오고, 다른 난수 스트림을 소모하지 않습니다.
        public List<CellPosition> SkillTargetsFor(int slot)
        {
            if (slot < 0 || slot >= Party.Length || Party[slot] == null) return new List<CellPosition>();
            Pcg32 random = new Pcg32(seed,(ulong)(100+Turns*3+slot));
            return SkillTargets.Pick(Board,Party[slot].skill,ref random);
        }
        public List<GameEvent> UseSkill(int slot)
        {
            List<GameEvent> events = new List<GameEvent>();
            if (Outcome != SessionOutcome.Idle || GetSkillState(slot) != SkillState.Ready) return events;
            SkillData skill = Party[slot].skill;
            List<CellPosition> targets = SkillTargetsFor(slot);
            // 발동 순간: 게이지 0, 파티 잠금, 스킬 턴 시작. 이동은 줄지 않습니다.
            Gauge[slot] = 0; SkillLocked = true; LastChance = false; skillSlot = slot;
            events.Add(new GameEvent(GameEventKind.SkillUsed,default(CellPosition)) { index = slot });
            events.Add(new GameEvent(GameEventKind.GaugeChanged,default(CellPosition)) { index = slot, value = 0 });
            switch (skill.type)
            {
                case "HitObstaclesThenBlocks":
                case "HitBottomRow":
                    resolver.HitAll(targets,events); break;
                case "ConvertToSpecial":
                    OccupantKind kind = skill.special == "Bomb" ? OccupantKind.Bomb : OccupantKind.Meteor;
                    foreach (CellPosition p in targets) resolver.Convert(p,kind,events);
                    break;
                case "AddMoves":
                    MovesLeft += skill.count;
                    events.Add(new GameEvent(GameEventKind.MovesChanged,default(CellPosition)) { value = MovesLeft });
                    break;
            }
            ApplyGravity(events);
            ResolveTurn(events,null,null,true);
            skillSlot = -1;
            return events;
        }
        // 마지막 기회의 [종료]: 확인 없이 실패합니다(10.3).
        public List<GameEvent> EndLastChance()
        {
            List<GameEvent> events = new List<GameEvent>();
            if (!LastChance || Outcome != SessionOutcome.Idle) return events;
            LastChance = false; Outcome = SessionOutcome.Fail;
            events.Add(new GameEvent(GameEventKind.TurnEnded,default(CellPosition)) { value = (int)Outcome });
            return events;
        }
        // 제거된 색 블록 색의 속성을 가진 파티원이 n명이면 각자 12/n(스킬 턴 안에서는 그 절반). 필요량을 넘치면 버립니다.
        private void Charge(BlockColor color, List<GameEvent> events)
        {
            string attr = color.ToString();
            int members = 0;
            for (int i = 0; i < Party.Length; i++) if (Party[i] != null && Party[i].character.attr == attr) members++;
            if (members == 0) return;
            int amount = config.rules.gaugeUnitsPerBlock/members;
            if (skillSlot >= 0) amount /= 2;
            for (int i = 0; i < Party.Length; i++)
            {
                if (Party[i] == null || Party[i].character.attr != attr || i == skillSlot) continue;
                int next = Math.Min(Required(i),Gauge[i]+amount);
                if (next == Gauge[i]) continue;
                Gauge[i] = next;
                events.Add(new GameEvent(GameEventKind.GaugeChanged,default(CellPosition)) { index = i, value = next });
            }
        }

        // 턴 마무리(7.2): 잠금 해제 → 클리어 → 이동 0(마지막 기회/실패) → 덩굴 번짐 → 셔플 → 대기.
        private void ResolveTurn(List<GameEvent> events, CellPosition? a, CellPosition? b, bool skillTurn)
        {
            bool first = a.HasValue;
            int steps = 0;
            while (steps <= config.rules.maxStepsPerResolution)
            {
                List<MatchGroup> groups = MatchFinder.Find(Board,Level.features.squareMatch);
                if (groups.Count == 0) break;
                if (steps >= config.rules.maxStepsPerResolution) throw new InvalidOperationException("Resolution exceeded step limit.");
                resolver.ResolveMatches(groups,first ? a : null,first ? b : null,events);
                ApplyGravity(events);
                steps++; first = false;
                events.Add(new GameEvent(GameEventKind.StepEnded,default(CellPosition)) { value = steps });
            }
            // 1. 스왑 턴이면 스킬 잠금 해제. 다른 상태로 가더라도 이 단계는 항상 먼저 합니다.
            if (!skillTurn) SkillLocked = false;
            LastChance = false;
            if (Goals.Complete) Outcome = SessionOutcome.Clear;
            else if (MovesLeft == 0)
            {
                // 스킬 턴 뒤에는 잠금이 걸려 있으므로, 이동이 여전히 0이면 실패입니다.
                skillSlot = -1;
                if (AnySkillReady()) LastChance = true; else Outcome = SessionOutcome.Fail;
            }
            else
            {
                // 덩굴은 스왑 턴에만 번집니다(11.4).
                if (!skillTurn) resolver.SpreadVine(events);
                if (!MoveFinder.HasAction(Board,Level.features.squareMatch))
                {
                    bool found = Shuffler.Shuffle(Board,Level,config.rules,ref shuffle);
                    events.Add(new GameEvent(GameEventKind.Shuffled,default(CellPosition)) { board = Board.Clone() });
                    if (!found) Outcome = SessionOutcome.Fail;
                }
            }
            events.Add(new GameEvent(GameEventKind.TurnEnded,default(CellPosition)) { value = (int)Outcome });
        }
        private void ApplyGravity(List<GameEvent> events)
        {
            foreach (GravityEvent item in Gravity.Apply(Board,Level.colors,ref refill,assist,config.rules,Level.features.squareMatch))
            {
                GameEventKind kind = item.kind == GravityEventKind.Fell ? GameEventKind.Fell :
                    item.kind == GravityEventKind.Spawned ? GameEventKind.Spawned : GameEventKind.RelicCollected;
                events.Add(new GameEvent(kind,item.to) { other = item.from, occupant = item.occupant });
                if (kind == GameEventKind.RelicCollected) Goals.Record("Relic",BlockColor.None,events);
            }
        }
    }
}
