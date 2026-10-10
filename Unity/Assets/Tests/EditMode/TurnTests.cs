using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using StardustAtelier.Core;
using StardustAtelier.Meta;

namespace StardustAtelier.Tests
{
    public sealed class TurnTests
    {
        private static Board Make(params string[] rows)
        {
            Board board = new Board(rows[0].Length,rows.Length);
            for (int r = 0; r < rows.Length; r++) for (int c = 0; c < rows[r].Length; c++)
            {
                char code = rows[r][c];
                if ("RBGYP".IndexOf(code) >= 0) board[r,c].occupant = Occupant.Block(BoardGenerator.ParseColor(code));
                else if (code == 'X' || code == 'V') board[r,c].occupant = new Occupant { kind = code == 'X' ? OccupantKind.Box : OccupantKind.Vine, hp = 1, color = BlockColor.None };
            }
            return board;
        }
        private static GameConfig Config() { return DataLoader.LoadConfig(Path.Combine(UnityEngine.Application.streamingAssetsPath,"Data")); }
        private static LevelData FirstLevel() { return DataLoader.LoadLevels(Path.Combine(UnityEngine.Application.streamingAssetsPath,"Data")).levels[0]; }
        private static string Fingerprint(Board board)
        {
            StringBuilder text = new StringBuilder();
            foreach (CellPosition p in board.Positions()) text.Append((int)board[p].occupant.kind).Append(',').Append((int)board[p].occupant.color).Append(',').Append(board[p].ice).Append(',').Append(board[p].chained).Append(';');
            return text.ToString();
        }
        // [실제 코드에 남길 주석] 명세 6장의 단일 타격·조합 보호와 마지막 턴 판정을 고정 보드로 검사합니다.
        [Test] public void ChainHitPreservesBlockAndIce()
        {
            Board board = Make("R"); board[0,0].chained = true; board[0,0].ice = 2;
            GoalTracker goals = new GoalTracker(new[] { new GoalData { type="Chain",count=1 },new GoalData { type="Collect",color="R",count=1 } });
            List<GameEvent> events = new List<GameEvent>();
            new Resolver(board,goals,1).Hit(new CellPosition(0,0),events,true);
            Assert.That(board[0,0].chained,Is.False); Assert.That(board[0,0].ice,Is.EqualTo(2));
            Assert.That(board[0,0].occupant.kind,Is.EqualTo(OccupantKind.ColorBlock));
            Assert.That(goals.Remaining(0),Is.Zero); Assert.That(goals.Remaining(1),Is.EqualTo(1));
        }
        [Test] public void TwoLayerIceCountsOneGoalOnlyWhenGone()
        {
            Board board = Make("R"); board[0,0].ice = 2;
            GoalTracker goals = new GoalTracker(new[] { new GoalData { type="Ice",count=1 } }); Resolver resolver = new Resolver(board,goals,1);
            List<GameEvent> events = new List<GameEvent>();
            resolver.Hit(new CellPosition(0,0),events,true); Assert.That(goals.Remaining(0),Is.EqualTo(1));
            resolver.Hit(new CellPosition(0,0),events,true); Assert.That(goals.Remaining(0),Is.Zero);
            Assert.That(events.FindAll(e=>e.kind==GameEventKind.GoalChanged).Count,Is.EqualTo(1));
        }
        [Test] public void PrismPrismHitsEveryCellExactlyOnceIncludingOrigins()
        {
            Board board = Make("RBG","BYR","GRY");
            foreach (CellPosition p in board.Positions()) board[p].ice = 2;
            board[0,0].occupant = new Occupant { kind=OccupantKind.Prism,color=BlockColor.None };
            board[0,1].occupant = board[0,0].occupant;
            GoalTracker goals = new GoalTracker(new[] { new GoalData { type="Ice",count=9 } });
            List<GameEvent> events = new List<GameEvent>();
            new Resolver(board,goals,1).ResolveCombo(new CellPosition(0,0),new CellPosition(0,1),events);
            foreach (CellPosition p in board.Positions()) Assert.That(board[p].ice,Is.EqualTo(1),p.ToString());
            Assert.That(events.FindAll(e=>e.kind==GameEventKind.CellHit).Count,Is.EqualTo(9));
        }
        // 서로 다른 두 발동(가로 로켓, 세로 로켓)이 같은 2겹 얼음을 각각 한 번씩 타격합니다(E15).
        [Test] public void DistinctRocketEffectsCanHitSameIceTwice()
        {
            Board board = Make("RBGYG","RYBYP","RBYPB"); board[1,2].ice = 2;
            board[1,0].occupant = new Occupant { kind=OccupantKind.Rocket,color=BlockColor.None,orientation=Orientation.H };
            board[0,2].occupant = new Occupant { kind=OccupantKind.Rocket,color=BlockColor.None,orientation=Orientation.V };
            GoalTracker goals = new GoalTracker(new[] { new GoalData { type="Ice",count=1 } });
            List<GameEvent> events = new List<GameEvent>(); Resolver resolver = new Resolver(board,goals,1);
            resolver.Activate(new CellPosition(1,0),events); resolver.Activate(new CellPosition(0,2),events);
            Assert.That(board[1,2].ice,Is.Zero); Assert.That(goals.Remaining(0),Is.Zero);
            Assert.That(events.FindAll(e=>e.kind==GameEventKind.IceDamaged && e.position.Equals(new CellPosition(1,2))).Count,Is.EqualTo(2));
        }
        // 2026-10-10 변경: 아이템은 색이 없어 매치를 끊고, 매치로 만든 로켓도 색이 없으며 같은 단계 타격을 받지 않습니다.
        [Test] public void ItemsBreakMatchesAndNewRocketIsColorless()
        {
            Board board = Make("BGBGB","GYGYG","RRRRB","YGYGY");
            List<GameEvent> events = new List<GameEvent>();
            GoalTracker goals = new GoalTracker(new[] { new GoalData { type="Collect",color="R",count=4 } });
            new Resolver(board,goals,1).ResolveMatches(MatchFinder.Find(board,false),null,new CellPosition(2,1),events);
            Assert.That(board[2,1].occupant.kind,Is.EqualTo(OccupantKind.Rocket));
            Assert.That(board[2,1].occupant.color,Is.EqualTo(BlockColor.None));
            Assert.That(goals.Complete,Is.True);
            Assert.That(events.FindLastIndex(e=>e.kind==GameEventKind.SpecialCreated),Is.GreaterThan(events.FindLastIndex(e=>e.kind==GameEventKind.CellHit)));
            // 같은 색 블록 사이에 놓인 아이템은 줄을 끊습니다.
            Board line = Make("RRRGB"); line[0,1].occupant = new Occupant { kind=OccupantKind.Bomb,color=BlockColor.None };
            Assert.That(MatchFinder.Find(line,false),Is.Empty);
        }
        [Test] public void ItemRemovalDoesNotCountColorGoal()
        {
            Board board = Make("GBG"); board[0,1].occupant = new Occupant { kind=OccupantKind.Bomb,color=BlockColor.None };
            GoalTracker goals = new GoalTracker(new[] { new GoalData { type="Collect",color="B",count=1 } });
            new Resolver(board,goals,1).Hit(new CellPosition(0,1),new List<GameEvent>(),false);
            Assert.That(goals.Remaining(0),Is.EqualTo(1));
        }
        [Test] public void NeighborBoxIsHitOnceAcrossSeparateMatchGroups()
        {
            Board board = Make("RRR","GXG","RRR");
            GoalTracker goals = new GoalTracker(new[] { new GoalData { type="Box",count=1 } }); List<GameEvent> events = new List<GameEvent>();
            new Resolver(board,goals,1).ResolveMatches(MatchFinder.Find(board,false),null,null,events);
            Assert.That(goals.Complete,Is.True);
            Assert.That(events.FindAll(e=>e.kind==GameEventKind.ObstacleDamaged).Count,Is.EqualTo(1));
        }
        // 조합 이벤트는 옮겨 온 아이템(A)과 도착 칸 아이템(B)의 종류를 함께 알려 줍니다.
        [Test] public void ComboEventCarriesBothItemKinds()
        {
            Board board = Make("RBGYP","BGYPR","GYPRB","YPRBG","PRBGY");
            board[2,2].occupant = new Occupant { kind=OccupantKind.Bomb,color=BlockColor.None };
            board[2,3].occupant = new Occupant { kind=OccupantKind.Rocket,color=BlockColor.None,orientation=Orientation.H };
            List<GameEvent> events = new List<GameEvent>();
            new Resolver(board,new GoalTracker(new[] { new GoalData { type="Collect",color="R",count=1 } }),1).ResolveCombo(new CellPosition(2,2),new CellPosition(2,3),events);
            GameEvent combo = events.Find(e=>e.kind==GameEventKind.SpecialActivated && e.value==2);
            Assert.That((OccupantKind)combo.index,Is.EqualTo(OccupantKind.Bomb));
            Assert.That(combo.occupant.kind,Is.EqualTo(OccupantKind.Rocket));
            Assert.That(combo.position,Is.EqualTo(new CellPosition(2,3)));
        }
        [Test] public void PrismRocketConvertsOnlyUnchainedMostCommonColor()
        {
            Board board = Make("RRR","RRG","BGY"); board[1,1].chained = true;
            board[0,0].occupant = new Occupant { kind=OccupantKind.Prism,color=BlockColor.None };
            board[0,1].occupant = new Occupant { kind=OccupantKind.Rocket,color=BlockColor.R };
            List<GameEvent> events = new List<GameEvent>();
            new Resolver(board,new GoalTracker(new[] { new GoalData { type="Collect",color="R",count=5 } }),1).ResolveCombo(new CellPosition(0,0),new CellPosition(0,1),events);
            List<GameEvent> created = events.FindAll(e=>e.kind==GameEventKind.SpecialCreated);
            Assert.That(created.Count,Is.EqualTo(2));
            foreach (GameEvent e in created) Assert.That(e.occupant.orientation,Is.EqualTo((e.position.r+e.position.c)%2==0 ? Orientation.H : Orientation.V));
            Assert.That(board[1,1].chained,Is.True);
        }
        [Test] public void HitVineStopsTurnSpread()
        {
            Board board = Make("RRRBG","GVGBG","BYBVG");
            Resolver resolver = new Resolver(board,new GoalTracker(new[] { new GoalData { type="Vine",count=2 } }),1);
            List<GameEvent> events = new List<GameEvent>(); resolver.BeginTurn();
            resolver.ResolveMatches(MatchFinder.Find(board,false),null,null,events); resolver.SpreadVine(events);
            Assert.That(resolver.VineHitThisTurn,Is.True); Assert.That(events.Exists(e=>e.kind==GameEventKind.VineSpread),Is.False);
        }
        [Test] public void InvalidSwapPreservesBoardGoalsMovesAndRandomStream()
        {
            GameSession first = new GameSession(FirstLevel(),Config(),63), second = new GameSession(FirstLevel(),Config(),63);
            string before = Fingerprint(first.Board); List<GameEvent> rejected = first.Swap(new CellPosition(0,0),new CellPosition(0,1));
            Assert.That(rejected.Exists(e=>e.kind==GameEventKind.SwapRejected),Is.True);
            Assert.That(Fingerprint(first.Board),Is.EqualTo(before)); Assert.That(first.MovesLeft,Is.EqualTo(20)); Assert.That(first.Turns,Is.Zero);
            first.Swap(new CellPosition(4,3),new CellPosition(3,3)); second.Swap(new CellPosition(4,3),new CellPosition(3,3));
            Assert.That(Fingerprint(first.Board),Is.EqualTo(Fingerprint(second.Board)));
            Assert.That(first.Goals.Amounts,Is.EqualTo(second.Goals.Amounts));
        }
        [Test] public void LastMoveClearWinsAndFinishesAllCascades()
        {
            LevelData level = FirstLevel(); level.moves = 1; level.goals[0].count = 1;
            GameSession session = new GameSession(level,Config(),63);
            session.Swap(new CellPosition(4,3),new CellPosition(3,3));
            Assert.That(session.MovesLeft,Is.Zero); Assert.That(session.Outcome,Is.EqualTo(SessionOutcome.Clear));
            Assert.That(MatchFinder.Find(session.Board,false),Is.Empty);
            Assert.That(session.Swap(new CellPosition(0,0),new CellPosition(0,1)),Is.Empty);
        }

        // 단독 클릭의 효과·이동·잠금·종료 판정을 기존 턴과 같은 경로로 검사합니다.
        [TestCase(OccupantKind.Rocket)]
        [TestCase(OccupantKind.Bomb)]
        [TestCase(OccupantKind.Meteor)]
        [TestCase(OccupantKind.Prism)]
        public void SingleActivationSpendsOneMoveWithoutSwap(OccupantKind kind)
        {
            GameSession session = new GameSession(FirstLevel(),Config(),63);
            CellPosition p = new CellPosition(0,0);
            session.Board[p].occupant = new Occupant { kind=kind,color=kind==OccupantKind.Prism ? BlockColor.None : BlockColor.B,orientation=Orientation.H };
            Assert.That(MatchFinder.Find(session.Board,false),Is.Empty);
            List<GameEvent> events = session.Activate(p);
            Assert.That(session.MovesLeft,Is.EqualTo(19)); Assert.That(session.Turns,Is.EqualTo(1));
            Assert.That(events.Exists(e=>e.kind==GameEventKind.SpecialActivated && e.position.Equals(p) && e.occupant.kind==kind),Is.True);
            Assert.That(events.Exists(e=>e.kind==GameEventKind.Swapped),Is.False);
            Assert.That(MatchFinder.Find(session.Board,false),Is.Empty);
            Assert.That(events.FindAll(e=>e.kind==GameEventKind.TurnEnded).Count,Is.EqualTo(1));
        }
        // 이동 발동: 특수 블록이 도착한 칸이 기준점이고, 이동 1회와 턴 종료 판정을 그대로 씁니다.
        [TestCase(OccupantKind.Rocket)]
        [TestCase(OccupantKind.Bomb)]
        [TestCase(OccupantKind.Meteor)]
        public void MovedSpecialActivatesAtDestination(OccupantKind kind)
        {
            GameSession session = new GameSession(FirstLevel(),Config(),63);
            CellPosition a = new CellPosition(0,0), b = new CellPosition(0,1);
            session.Board[a].occupant = new Occupant { kind=kind,color=BlockColor.B,orientation=Orientation.V };
            List<GameEvent> events = session.Swap(a,b);
            Assert.That(events.Exists(e=>e.kind==GameEventKind.SwapRejected),Is.False);
            Assert.That(session.MovesLeft,Is.EqualTo(19)); Assert.That(session.Turns,Is.EqualTo(1));
            Assert.That(events.Exists(e=>e.kind==GameEventKind.SpecialActivated && e.position.Equals(b) && e.occupant.kind==kind),Is.True);
            Assert.That(MatchFinder.Find(session.Board,false),Is.Empty);
            Assert.That(events.FindAll(e=>e.kind==GameEventKind.TurnEnded).Count,Is.EqualTo(1));
        }
        [Test] public void SingleRocketChainsBombAndHitsItsOwnIceOnce()
        {
            Board board = Make("RBG","GYB","BGR"); CellPosition p = new CellPosition(1,0);
            board[p].occupant = new Occupant { kind=OccupantKind.Rocket,color=BlockColor.G,orientation=Orientation.H };
            board[p].ice = 2;
            board[1,2].occupant = new Occupant { kind=OccupantKind.Bomb,color=BlockColor.B };
            List<GameEvent> events = new List<GameEvent>();
            new Resolver(board,new GoalTracker(new[] { new GoalData { type="Collect",color="B",count=3 } }),1).Activate(p,events);
            Assert.That(events.FindAll(e=>e.kind==GameEventKind.SpecialActivated).Count,Is.EqualTo(2));
            Assert.That(board[p].ice,Is.EqualTo(1));
            Assert.That(board[0,2].occupant.kind,Is.EqualTo(OccupantKind.Empty));
        }
        [Test] public void SinglePrismClearsMostCommonColor()
        {
            Board board = Make("RBR","GBR","YPR"); CellPosition p = new CellPosition(1,0);
            board[p].occupant = new Occupant { kind=OccupantKind.Prism,color=BlockColor.None };
            GoalTracker goals = new GoalTracker(new[] { new GoalData { type="Collect",color="R",count=4 } });
            List<GameEvent> events = new List<GameEvent>(); new Resolver(board,goals,1).Activate(p,events);
            Assert.That(goals.Complete,Is.True); Assert.That(board[0,1].occupant.color,Is.EqualTo(BlockColor.B));
        }
        [Test] public void NonSpecialChainedMissingAndOutsideClicksDoNotSpendOrChangeState()
        {
            GameSession session = new GameSession(FirstLevel(),Config(),63);
            CellPosition p = new CellPosition(0,0);
            Assert.That(session.Activate(p),Is.Empty);
            session.Board[p].occupant = new Occupant { kind=OccupantKind.Rocket,color=BlockColor.B };
            session.Board[p].chained = true;
            string before = Fingerprint(session.Board);
            Assert.That(session.Activate(p),Is.Empty); Assert.That(Fingerprint(session.Board),Is.EqualTo(before));
            session.Board[p].chained = false; session.Board[p].exists = false;
            Assert.That(session.Activate(p),Is.Empty); Assert.That(session.Activate(new CellPosition(-1,0)),Is.Empty);
            Assert.That(session.MovesLeft,Is.EqualTo(20)); Assert.That(session.Turns,Is.Zero);
        }
        [Test] public void LastClickClearFinishesCascadeAndRejectsFurtherClicks()
        {
            LevelData level = FirstLevel(); level.moves=1; level.goals[0].count=1;
            GameSession session = new GameSession(level,Config(),63); CellPosition p = new CellPosition(0,0);
            session.Board[p].occupant = new Occupant { kind=OccupantKind.Rocket,color=BlockColor.R,orientation=Orientation.H };
            session.Activate(p);
            Assert.That(session.MovesLeft,Is.Zero); Assert.That(session.Outcome,Is.EqualTo(SessionOutcome.Clear));
            Assert.That(MatchFinder.Find(session.Board,false),Is.Empty); Assert.That(session.Activate(p),Is.Empty);
        }
        [Test] public void SpecialClickCountsAsActionEvenWithoutAnyPossibleSwap()
        {
            Board board = Make("B"); board[0,0].occupant = new Occupant { kind=OccupantKind.Bomb,color=BlockColor.B };
            Assert.That(MoveFinder.Find(board,false),Is.Empty); Assert.That(MoveFinder.HasAction(board,false),Is.True);
            board[0,0].chained=true; Assert.That(MoveFinder.HasAction(board,false),Is.False);
        }
        [Test] public void AllPlayableLevelsFinishRandomTurnsWithoutUnresolvedMatches()
        {
            LevelCatalog data = DataLoader.LoadLevels(Path.Combine(UnityEngine.Application.streamingAssetsPath,"Data")); GameConfig config = Config();
            foreach (LevelData level in data.levels)
            {
                for (ulong seed = 1; seed <= 10; seed++)
                {
                    GameSession session = new GameSession(level,config,seed); Pcg32 bot = new Pcg32(seed,77);
                    for (int turn = 0; turn < level.moves && session.Outcome == SessionOutcome.Idle; turn++)
                    {
                        List<ValidMove> moves = MoveFinder.Find(session.Board,level.features.squareMatch);
                        // 스왑할 수가 없고 누를 아이템만 있으면 아이템을 발동합니다(유효한 행동, 9.3).
                        CellPosition? item = MoveFinder.FindActivation(session.Board);
                        Assert.That(moves.Count > 0 || item.HasValue,Is.True,"Level "+level.id+", seed "+seed);
                        if (moves.Count == 0) { session.Activate(item.Value); continue; }
                        ValidMove move = moves[bot.NextInt(moves.Count)]; session.Swap(move.a,move.b);
                        Assert.That(MatchFinder.Find(session.Board,level.features.squareMatch),Is.Empty);
                    }
                    Assert.That(session.Outcome,Is.Not.EqualTo(SessionOutcome.Idle));
                    Assert.That(session.Goals.Complete,Is.EqualTo(session.Outcome == SessionOutcome.Clear));
                }
            }
        }
    }
}
