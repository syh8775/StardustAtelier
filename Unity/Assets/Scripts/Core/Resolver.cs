using System;
using System.Collections.Generic;

namespace StardustAtelier.Core
{
    public sealed class Resolver
    {
        private readonly Board board;
        private readonly GoalTracker goals;
        private readonly Queue<Activation> queue = new Queue<Activation>();
        private Pcg32 effects;
        public bool VineHitThisTurn { get; private set; }
        private struct Activation
        {
            public CellPosition at;
            public Occupant special;
            public Activation(CellPosition p, Occupant value) { at = p; special = value; }
        }
        private struct Creation
        {
            public CellPosition at;
            public Occupant value;
            public Creation(CellPosition p, Occupant special) { at = p; value = special; }
        }
        public Resolver(Board target, GoalTracker tracker, ulong seed)
        { board = target; goals = tracker; effects = new Pcg32(seed,3); }
        public void BeginTurn() { VineHitThisTurn = false; }

        // 단독 클릭도 기존 FIFO 효과 큐를 사용하므로 연쇄 발동 순서를 유지합니다.
        public void Activate(CellPosition p, List<GameEvent> events)
        {
            if (!MoveFinder.CanActivate(board,p)) return;
            Hit(p,events,true);
            Drain(events);
        }

        // 스킬 효과(세이지·루미): 지정한 칸을 읽기 순서로 1회씩 타격하는 발동 1개입니다.
        public void HitAll(List<CellPosition> cells, List<GameEvent> events)
        {
            List<CellPosition> unique = new List<CellPosition>();
            foreach (CellPosition p in cells) if (!unique.Contains(p)) unique.Add(p);
            unique.Sort(MatchFinder.ReadingOrder);
            foreach (CellPosition p in unique) Hit(p,events,true);
            Drain(events);
        }
        // 스킬 효과(피오·솔): 보통 블록을 색 없는 아이템으로 바꿉니다. 바로 발동하지 않고 보드에 남습니다.
        // 바뀌는 색 블록은 그 순간 원래 색으로 목표·게이지에 집계합니다(2026-10-10 규칙).
        public void Convert(CellPosition p, OccupantKind kind, List<GameEvent> events)
        {
            Cell cell = board[p];
            if (!SkillTargets.IsNormalBlock(cell)) return;
            goals.Record("Collect",cell.occupant.color,events);
            cell.occupant = new Occupant { kind = kind, color = BlockColor.None, orientation = Orientation.H };
            events.Add(new GameEvent(GameEventKind.SpecialCreated,p) { occupant = cell.occupant });
        }

        public void ResolveMatches(List<MatchGroup> groups, CellPosition? a, CellPosition? b, List<GameEvent> events)
        {
            List<CellPosition> matched = new List<CellPosition>();
            List<Creation> creations = new List<Creation>();
            foreach (MatchGroup group in groups)
            {
                foreach (CellPosition p in group.cells) if (!matched.Contains(p)) matched.Add(p);
                Occupant special = group.GetSpecial();
                CellPosition? at = group.GetCreationPosition(board,a,b);
                if (special.kind != OccupantKind.Empty && at.HasValue) creations.Add(new Creation(at.Value,special));
            }
            matched.Sort(MatchFinder.ReadingOrder);
            foreach (CellPosition p in matched) Hit(p,events,true);
            Drain(events);
            // [실제 코드에 남길 주석] 인접 타격은 효과 처리 후 남아 있는 장애물에 한 번만 적용합니다.
            List<CellPosition> adjacent = new List<CellPosition>();
            foreach (CellPosition p in matched)
                foreach (CellPosition q in BoardGenerator.Neighbors(p))
                    if (board.Contains(q) && !adjacent.Contains(q) &&
                        (board[q].occupant.kind == OccupantKind.Box || board[q].occupant.kind == OccupantKind.Vine))
                        adjacent.Add(q);
            adjacent.Sort(MatchFinder.ReadingOrder);
            foreach (CellPosition p in adjacent) Hit(p,events,true);
            // 새 특수 블록은 모든 타격이 끝난 뒤에 놓아 같은 단계의 효과로부터 보호합니다.
            foreach (Creation creation in creations)
            {
                board[creation.at].occupant = creation.value;
                events.Add(new GameEvent(GameEventKind.SpecialCreated,creation.at) { occupant = creation.value });
            }
        }

        public void Hit(CellPosition p, List<GameEvent> events, bool activate)
        {
            if (!board.Contains(p) || !board[p].exists) return;
            Cell cell = board[p];
            GameEvent hit = new GameEvent(GameEventKind.CellHit,p);
            events.Add(hit);
            if (cell.chained)
            {
                cell.chained = false;
                events.Add(new GameEvent(GameEventKind.ChainBroken,p));
                goals.Record("Chain",BlockColor.None,events);
            }
            else
            {
                Occupant value = cell.occupant;
                if (value.IsColored || value.IsSpecial)
                {
                    cell.occupant = Occupant.Empty;
                    events.Add(new GameEvent(GameEventKind.BlockCleared,p) { occupant = value });
                    // 색 수집 목표는 색 블록만 셉니다. 아이템은 색이 없어 집계하지 않습니다.
                    if (value.IsColored) goals.Record("Collect",value.color,events);
                    if (value.IsSpecial && activate) queue.Enqueue(new Activation(p,value));
                    DamageIce(p,events);
                }
                else if (value.kind == OccupantKind.Box || value.kind == OccupantKind.Vine)
                {
                    if (value.kind == OccupantKind.Vine) VineHitThisTurn = true;
                    value.hp--;
                    cell.occupant = value.hp > 0 ? value : Occupant.Empty;
                    events.Add(new GameEvent(GameEventKind.ObstacleDamaged,p) { value = Math.Max(0,value.hp), occupant = value });
                    if (value.hp <= 0) goals.Record(value.kind == OccupantKind.Box ? "Box" : "Vine",BlockColor.None,events);
                }
                else if (value.kind == OccupantKind.Empty) DamageIce(p,events);
            }
            hit.cell = cell.Clone();
        }
        private void DamageIce(CellPosition p, List<GameEvent> events)
        {
            if (board[p].ice <= 0) return;
            board[p].ice--;
            events.Add(new GameEvent(GameEventKind.IceDamaged,p) { value = board[p].ice });
            if (board[p].ice == 0) goals.Record("Ice",BlockColor.None,events);
        }
        private void Drain(List<GameEvent> events)
        {
            while (queue.Count > 0)
            {
                Activation current = queue.Dequeue();
                events.Add(new GameEvent(GameEventKind.SpecialActivated,current.at) { occupant = current.special });
                List<CellPosition> targets = Targets(current.at,current.special);
                ApplyTargets(targets,current.at,events,new List<CellPosition> { current.at });
            }
        }
        private void ApplyTargets(List<CellPosition> cells, CellPosition center, List<GameEvent> events, List<CellPosition> alreadyHit = null)
        {
            List<CellPosition> unique = new List<CellPosition>();
            foreach (CellPosition p in cells)
                if (board.Contains(p) && board[p].exists && !unique.Contains(p) && (alreadyHit == null || !alreadyHit.Contains(p))) unique.Add(p);
            unique.Sort((a,b) =>
            {
                int distanceA = Math.Max(Math.Abs(a.r-center.r),Math.Abs(a.c-center.c));
                int distanceB = Math.Max(Math.Abs(b.r-center.r),Math.Abs(b.c-center.c));
                int result = distanceA.CompareTo(distanceB);
                return result == 0 ? MatchFinder.ReadingOrder(a,b) : result;
            });
            foreach (CellPosition p in unique) Hit(p,events,true);
        }
        private List<CellPosition> Targets(CellPosition at, Occupant special)
        {
            List<CellPosition> targets = new List<CellPosition>();
            if (special.kind == OccupantKind.Rocket)
                foreach (CellPosition p in board.Positions())
                    if (special.orientation == Orientation.H ? p.r == at.r : p.c == at.c) targets.Add(p);
            if (special.kind == OccupantKind.Bomb) AddSquare(targets,at,1);
            if (special.kind == OccupantKind.Meteor)
            {
                CellPosition? target = PickMeteor(new List<CellPosition>());
                if (target.HasValue) targets.Add(target.Value);
            }
            if (special.kind == OccupantKind.Prism) AddColor(targets,MostCommonColor());
            return targets;
        }
        private BlockColor MostCommonColor()
        {
            int[] counts = new int[5];
            foreach (CellPosition p in board.Positions())
                if (board[p].exists && board[p].occupant.kind == OccupantKind.ColorBlock) counts[(int)board[p].occupant.color]++;
            int best = -1;
            for (int i = 0; i < counts.Length; i++) if (counts[i] > 0 && (best < 0 || counts[i] > counts[best])) best = i;
            return best < 0 ? BlockColor.None : (BlockColor)best;
        }
        private void AddColor(List<CellPosition> targets, BlockColor color)
        {
            foreach (CellPosition p in board.Positions())
                if (board[p].CanMatch && board[p].occupant.color == color) targets.Add(p);
        }
        private void AddSquare(List<CellPosition> targets, CellPosition at, int radius)
        {
            for (int r = at.r-radius; r <= at.r+radius; r++)
                for (int c = at.c-radius; c <= at.c+radius; c++) targets.Add(new CellPosition(r,c));
        }
        private CellPosition? PickMeteor(List<CellPosition> excluded)
        {
            for (int i = 0; i < goals.Count; i++)
            {
                if (goals.Remaining(i) == 0) continue;
                GoalData goal = goals.Goal(i);
                List<CellPosition> candidates = new List<CellPosition>();
                foreach (CellPosition p in board.Positions())
                {
                    Cell cell = board[p];
                    if (!cell.exists || cell.occupant.IsSpecial || excluded.Contains(p)) continue;
                    bool candidate = goal.type == "Collect" && cell.occupant.kind == OccupantKind.ColorBlock && cell.occupant.color.ToString() == goal.color;
                    candidate |= goal.type == "Ice" && cell.ice > 0;
                    candidate |= goal.type == "Box" && cell.occupant.kind == OccupantKind.Box;
                    candidate |= goal.type == "Vine" && cell.occupant.kind == OccupantKind.Vine;
                    candidate |= goal.type == "Chain" && cell.chained;
                    if (goal.type == "Relic" && p.r > 0 && cell.occupant.kind == OccupantKind.ColorBlock)
                        candidate |= board[p.r-1,p.c].exists && board[p.r-1,p.c].occupant.kind == OccupantKind.Relic;
                    if (candidate) candidates.Add(p);
                }
                if (candidates.Count > 0) return candidates[effects.NextInt(candidates.Count)];
            }
            List<CellPosition> fallback = new List<CellPosition>();
            foreach (CellPosition p in board.Positions())
                if (board[p].exists && board[p].occupant.kind == OccupantKind.ColorBlock && !excluded.Contains(p)) fallback.Add(p);
            return fallback.Count == 0 ? (CellPosition?)null : fallback[effects.NextInt(fallback.Count)];
        }

        public void ResolveCombo(CellPosition a, CellPosition b, List<GameEvent> events)
        {
            Occupant first = board[a].occupant, second = board[b].occupant;
            List<CellPosition> consumed = new List<CellPosition> { a, b };
            bool firstPrism = first.kind == OccupantKind.Prism, secondPrism = second.kind == OccupantKind.Prism;
            // 조합 이벤트: occupant = 도착 칸(B)의 것, index = 옮겨 온 것(A)의 종류. 화면이 조합별 연출을 고르는 데 씁니다.
            events.Add(new GameEvent(GameEventKind.SpecialActivated,b) { occupant = second, value = 2, index = (int)first.kind });
            if ((firstPrism && second.kind == OccupantKind.ColorBlock) || (secondPrism && first.kind == OccupantKind.ColorBlock))
            {
                BlockColor color = firstPrism ? second.color : first.color;
                Hit(firstPrism ? a : b,events,false);
                List<CellPosition> colorCells = new List<CellPosition>(); AddColor(colorCells,color);
                ApplyTargets(colorCells,b,events,new List<CellPosition> { firstPrism ? a : b }); Drain(events); return;
            }
            Hit(a,events,false); Hit(b,events,false);
            if (firstPrism && secondPrism)
            {
                ApplyTargets(new List<CellPosition>(board.Positions()),b,events,consumed);
            }
            else if (firstPrism || secondPrism)
            {
                Occupant model = firstPrism ? second : first;
                BlockColor color = MostCommonColor();
                List<CellPosition> converted = new List<CellPosition>();
                foreach (CellPosition p in board.Positions())
                    if (board[p].exists && !board[p].chained && board[p].occupant.kind == OccupantKind.ColorBlock && board[p].occupant.color == color)
                    {
                        Occupant value = model; value.color = BlockColor.None;
                        if (value.kind == OccupantKind.Rocket) value.orientation = (p.r+p.c)%2 == 0 ? Orientation.H : Orientation.V;
                        board[p].occupant = value; converted.Add(p);
                        // 바뀐 색 블록은 아이템이 되면 색을 잃으므로, 바뀌는 순간 그 색으로 집계합니다.
                        goals.Record("Collect",color,events);
                        events.Add(new GameEvent(GameEventKind.SpecialCreated,p) { occupant = value });
                    }
                if (converted.Count == 0)
                {
                    queue.Enqueue(new Activation(a,first)); queue.Enqueue(new Activation(b,second));
                }
                else foreach (CellPosition p in converted) Hit(p,events,true);
            }
            else if (first.kind == OccupantKind.Meteor || second.kind == OccupantKind.Meteor)
            {
                bool both = first.kind == OccupantKind.Meteor && second.kind == OccupantKind.Meteor;
                List<CellPosition> chosen = new List<CellPosition>();
                for (int i = 0; i < (both ? 3 : 1); i++)
                {
                    CellPosition? target = PickMeteor(chosen);
                    if (target.HasValue) chosen.Add(target.Value);
                }
                List<CellPosition> targets = new List<CellPosition>();
                if (both) targets.AddRange(chosen);
                else if (chosen.Count > 0)
                {
                    Occupant model = first.kind == OccupantKind.Meteor ? second : first;
                    targets = Targets(chosen[0],model);
                }
                ApplyTargets(targets,b,events,consumed);
            }
            else
            {
                List<CellPosition> targets = new List<CellPosition>();
                bool rockets = first.kind == OccupantKind.Rocket && second.kind == OccupantKind.Rocket;
                bool bombs = first.kind == OccupantKind.Bomb && second.kind == OccupantKind.Bomb;
                if (bombs) AddSquare(targets,b,2);
                else foreach (CellPosition p in board.Positions())
                    if (rockets ? p.r == b.r || p.c == b.c : Math.Abs(p.r-b.r) <= 1 || Math.Abs(p.c-b.c) <= 1) targets.Add(p);
                ApplyTargets(targets,b,events,consumed);
            }
            Drain(events);
        }

        public void SpreadVine(List<GameEvent> events)
        {
            if (VineHitThisTurn) return;
            List<CellPosition> vines = new List<CellPosition>();
            foreach (CellPosition p in board.Positions())
                if (board[p].exists && board[p].occupant.kind == OccupantKind.Vine && VineNeighbors(p).Count > 0) vines.Add(p);
            if (vines.Count == 0) return;
            CellPosition origin = vines[effects.NextInt(vines.Count)];
            List<CellPosition> neighbors = VineNeighbors(origin);
            CellPosition target = neighbors[effects.NextInt(neighbors.Count)];
            board[target].occupant = new Occupant { kind = OccupantKind.Vine, hp = 1, color = BlockColor.None };
            events.Add(new GameEvent(GameEventKind.VineSpread,target) { occupant = board[target].occupant });
        }
        private List<CellPosition> VineNeighbors(CellPosition p)
        {
            List<CellPosition> neighbors = new List<CellPosition>();
            foreach (CellPosition q in BoardGenerator.Neighbors(p))
                if (board.Contains(q) && board[q].exists && board[q].ice == 0 && !board[q].chained && board[q].occupant.kind == OccupantKind.ColorBlock) neighbors.Add(q);
            neighbors.Sort(MatchFinder.ReadingOrder);
            return neighbors;
        }
    }
}
