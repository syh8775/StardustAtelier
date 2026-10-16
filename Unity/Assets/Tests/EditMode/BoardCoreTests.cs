using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using StardustAtelier.Core;
using StardustAtelier.Meta;

namespace StardustAtelier.Tests
{
    public sealed class BoardCoreTests
    {
        // [실제 코드에 남길 주석] 고정 보드로 규칙을 검증하며 화면·프레임 시간에 의존하지 않습니다.
        private static Board Make(params string[] rows)
        {
            Board board=new Board(rows[0].Length,rows.Length);
            for(int r=0;r<rows.Length;r++) for(int c=0;c<rows[r].Length;c++)
            {
                char token=rows[r][c];
                if("RBGYP".IndexOf(token)>=0) board[r,c].occupant=Occupant.Block(BoardGenerator.ParseColor(token));
                if(token=='#') board[r,c].exists=false;
                if(token=='X') board[r,c].occupant=new Occupant { kind=OccupantKind.Box, hp=1, color=BlockColor.None };
                if(token=='O') board[r,c].occupant=new Occupant { kind=OccupantKind.Relic, color=BlockColor.None };
            }
            return board;
        }
        private static string Fingerprint(Board board)
        {
            StringBuilder text=new StringBuilder();
            foreach(CellPosition p in board.Positions())
            {
                Cell cell=board[p];
                text.Append(cell.exists).Append(',').Append(cell.ice).Append(',').Append(cell.chained).Append(',')
                    .Append((int)cell.occupant.kind).Append(',').Append((int)cell.occupant.color).Append(',')
                    .Append((int)cell.occupant.orientation).Append(',').Append(cell.occupant.hp).Append(';');
            }
            return text.ToString();
        }
        private static string DataDirectory { get { return Path.Combine(UnityEngine.Application.streamingAssetsPath,"Data"); } }

        [Test] public void Pcg32MatchesPublishedReferenceVector()
        {
            Pcg32 random=new Pcg32(42,54);
            uint[] expected={0xa15c02b7u,0x7b47f409u,0xba1d3330u,0x83d2f293u,0xbfa4784bu,0xcbed606eu};
            foreach(uint value in expected) Assert.That(random.NextUInt(),Is.EqualTo(value));
        }
        [Test] public void PcgCopyAndStreamsRemainIndependent()
        {
            Pcg32 first=new Pcg32(91,1), copy=first, refill=new Pcg32(91,2);
            Assert.That(first.NextUInt(),Is.EqualTo(copy.NextUInt()));
            Assert.That(first.NextUInt(),Is.Not.EqualTo(refill.NextUInt()));
            Assert.Throws<ArgumentOutOfRangeException>(()=>copy.NextInt(0));
            for(int i=0;i<1000;i++) Assert.That(copy.NextInt(5),Is.InRange(0,4));
        }
        [Test] public void BoardCloneDoesNotShareCells()
        {
            Board board=Make("RB","GY"), clone=board.Clone();
            clone[0,0].ice=2; clone[0,0].occupant=Occupant.Empty;
            Assert.That(board[0,0].ice,Is.Zero);
            Assert.That(board[0,0].occupant.kind,Is.EqualTo(OccupantKind.ColorBlock));
        }
        [Test] public void SwapMovesOnlyOccupants()
        {
            Board board=Make("RB"); board[0,0].ice=2; board[0,1].ice=1;
            board.Swap(new CellPosition(0,0),new CellPosition(0,1));
            Assert.That(board[0,0].occupant.color,Is.EqualTo(BlockColor.B));
            Assert.That(board[0,0].ice,Is.EqualTo(2));
            Assert.That(board[0,1].ice,Is.EqualTo(1));
        }
        [Test] public void TouchingLinesStaySeparateUntilSquareUnlock()
        {
            Board board=Make("RRR","RRR");
            Assert.That(MatchFinder.Find(board,false).Count,Is.EqualTo(2));
            List<MatchGroup> unlocked=MatchFinder.Find(board,true);
            Assert.That(unlocked.Count,Is.EqualTo(1));
            Assert.That(unlocked[0].GetSpecial().kind,Is.EqualTo(OccupantKind.Meteor));
        }
        [Test] public void FiveLineTakesPriorityOverCross()
        {
            Board board=Make("--R--","RRRRR","--R--");
            List<MatchGroup> groups=MatchFinder.Find(board,false);
            Assert.That(groups.Count,Is.EqualTo(1));
            Assert.That(groups[0].GetSpecial().kind,Is.EqualTo(OccupantKind.Prism));
        }
        [Test] public void CrossTakesPriorityOverFourLine()
        {
            Board board=Make("-R--","RRRR","-R--");
            Assert.That(MatchFinder.Find(board,false)[0].GetSpecial().kind,Is.EqualTo(OccupantKind.Bomb));
        }
        [Test] public void ChainMatchesButCannotSwapOrHostSpecial()
        {
            Board board=Make("R","R","R","R");
            board[0,0].chained=true; board[3,0].chained=true;
            MatchGroup group=MatchFinder.Find(board,false)[0];
            Assert.That(group.GetSpecial().orientation,Is.EqualTo(Orientation.V));
            Assert.That(group.GetCreationPosition(board),Is.EqualTo(new CellPosition(2,0)));
            Assert.That(board[0,0].CanMatch,Is.True);
            Assert.That(board[0,0].CanSwap,Is.False);
            foreach(CellPosition p in board.Positions()) board[p].chained=true;
            Assert.That(group.GetCreationPosition(board),Is.Null);
        }
        [Test] public void HoleRelicAndPrismBreakColorRuns()
        {
            Board board=Make("RR#RR","RRORR","RRBRR");
            board[2,2].occupant=new Occupant {kind=OccupantKind.Prism,color=BlockColor.None};
            Assert.That(MatchFinder.Find(board,false).Count,Is.EqualTo(4)); // 네 개의 세로 3개 줄만 남습니다.
            foreach(MatchGroup group in MatchFinder.Find(board,false)) Assert.That(group.patterns[0].shape,Is.EqualTo(MatchShape.Vertical));
        }
        // 2026-10-09 변경(E02): 로켓을 색 블록과 바꾸면 매치가 없어도 유효합니다.
        [Test] public void RocketColorSwapIsValidWithoutMatch()
        {
            Board board=Make("RB","GY");
            board[0,0].occupant=new Occupant {kind=OccupantKind.Rocket,color=BlockColor.R,orientation=Orientation.H};
            Assert.That(MoveFinder.IsValid(board,new CellPosition(0,0),new CellPosition(0,1),false),Is.True);
            Assert.That(MoveFinder.IsValid(board,new CellPosition(0,1),new CellPosition(0,0),false),Is.True);
            board[0,1].occupant=new Occupant {kind=OccupantKind.Bomb,color=BlockColor.B};
            Assert.That(MoveFinder.IsValid(board,new CellPosition(0,0),new CellPosition(0,1),false),Is.True);
        }
        [Test] public void PrismColorSwapIsValidWithoutMatch()
        {
            Board board=Make("RB","GY");
            board[0,0].occupant=new Occupant {kind=OccupantKind.Prism,color=BlockColor.None};
            Assert.That(MoveFinder.IsValid(board,new CellPosition(0,0),new CellPosition(0,1),false),Is.True);
        }
        [Test] public void MoveSearchLeavesBoardUntouched()
        {
            Board board=Make("RBR","GRG","YGY");
            string before=Fingerprint(board);
            List<ValidMove> moves=MoveFinder.Find(board,false);
            Assert.That(moves.Count,Is.GreaterThan(0));
            Assert.That(moves[0].a,Is.EqualTo(new CellPosition(0,1)));
            Assert.That(moves[0].b,Is.EqualTo(new CellPosition(1,1)));
            Assert.That(Fingerprint(board),Is.EqualTo(before));
        }
        [Test] public void UnrelatedExistingMatchDoesNotValidateSwap()
        {
            Board board=Make("RRR","BGB","GYG");
            Assert.That(MoveFinder.IsValid(board,new CellPosition(1,0),new CellPosition(1,1),false),Is.False);
        }
        [Test] public void GravityPassesThroughHolesAndPreservesFloor()
        {
            Board board=Make("R","#","B","-","-"); board[4,0].ice=2;
            List<GravityEvent> events=new List<GravityEvent>(); Gravity.Collapse(board,events);
            Assert.That(board[4,0].occupant.color,Is.EqualTo(BlockColor.B));
            Assert.That(board[3,0].occupant.color,Is.EqualTo(BlockColor.R));
            Assert.That(board[4,0].ice,Is.EqualTo(2));
            Assert.That(board[1,0].occupant.kind,Is.EqualTo(OccupantKind.Empty));
        }
        [Test] public void FixedOccupantStopsRefillBelowIt()
        {
            Board board=Make("-","R","X","B","-");
            Pcg32 refill=new Pcg32(7,2);
            Gravity.Apply(board,new[]{"R","B","G"},ref refill,false,new RuleConfig(),false);
            Assert.That(board[0,0].occupant.kind,Is.EqualTo(OccupantKind.ColorBlock));
            Assert.That(board[2,0].occupant.kind,Is.EqualTo(OccupantKind.Box));
            Assert.That(board[3,0].occupant.kind,Is.EqualTo(OccupantKind.Empty));
            Assert.That(board[4,0].occupant.color,Is.EqualTo(BlockColor.B));
        }
        [Test] public void ChainedColorSplitsGravitySegments()
        {
            Board board=Make("R","G","B","-"); board[1,0].chained=true;
            List<GravityEvent> events=new List<GravityEvent>(); Gravity.Collapse(board,events);
            Assert.That(board[0,0].occupant.color,Is.EqualTo(BlockColor.R));
            Assert.That(board[1,0].occupant.color,Is.EqualTo(BlockColor.G));
            Assert.That(board[3,0].occupant.color,Is.EqualTo(BlockColor.B));
        }
        // 얼음에 갇힌 블록은 옮길 수도 떨어질 수도 없고, 매치에는 포함됩니다. 얼음이 다 깨지면 다시 움직입니다.
        [Test] public void FrozenBlockCannotSwapOrFallUntilIceBreaks()
        {
            Board board=Make("R","G","B","-"); board[1,0].ice=1;
            Assert.That(board[1,0].CanSwap,Is.False); Assert.That(board[1,0].CanMatch,Is.True);
            Assert.That(MoveFinder.IsValid(board,new CellPosition(0,0),new CellPosition(1,0),false),Is.False);
            List<GravityEvent> events=new List<GravityEvent>(); Gravity.Collapse(board,events);
            Assert.That(board[1,0].occupant.color,Is.EqualTo(BlockColor.G));
            Assert.That(board[3,0].occupant.color,Is.EqualTo(BlockColor.B));
            board[1,0].ice=0; Gravity.Collapse(board,events);
            Assert.That(board[1,0].CanSwap,Is.True);
            Assert.That(board[2,0].occupant.color,Is.EqualTo(BlockColor.G));
        }
        // 얼음 속 아이템은 이동·발동 불가이며, 유물은 기존대로 얼지 않습니다.
        [Test] public void FrozenSpecialCannotActivateAndRelicNeverFreezes()
        {
            Board board=Make("R","O","-");
            board[0,0].occupant=new Occupant { kind=OccupantKind.Bomb,color=BlockColor.None }; board[0,0].ice=1; board[1,0].ice=2;
            Assert.That(board[0,0].CanSwap,Is.False);
            Assert.That(MoveFinder.CanActivate(board,new CellPosition(0,0)),Is.False);
            Assert.That(board[1,0].CanSwap,Is.True);
            List<GravityEvent> events=new List<GravityEvent>(); Gravity.Collapse(board,events);
            Assert.That(board[2,0].occupant.kind,Is.EqualTo(OccupantKind.Relic));
        }
        // 유물은 블록과 자리를 바꿀 수 있습니다. 바꾼 블록이 매치를 만들 때만 유효한 수입니다.
        [Test] public void RelicSwapsOnlyWhenTheBlockMakesAMatch()
        {
            Board board=Make("RRBG","GYOR","BGRY");
            Assert.That(MoveFinder.IsValid(board,new CellPosition(1,2),new CellPosition(1,3),false),Is.False);
            board=Make("RROG","GYRB","BGYY");
            Assert.That(MoveFinder.IsValid(board,new CellPosition(0,2),new CellPosition(1,2),false),Is.True);
        }
        [Test] public void StackedRelicsCollectBeforeRefill()
        {
            Board board=Make("R","O","O","#");
            Pcg32 refill=new Pcg32(5,2);
            List<GravityEvent> events=Gravity.Apply(board,new[]{"R","B","G"},ref refill,false,new RuleConfig(),false);
            Assert.That(events.FindAll(e=>e.kind==GravityEventKind.RelicCollected).Count,Is.EqualTo(2));
            Assert.That(events.FindAll(e=>e.kind==GravityEventKind.Spawned).Count,Is.EqualTo(2));
            Assert.That(board[2,0].occupant.color,Is.EqualTo(BlockColor.R));
        }
        [Test] public void RefillUsesExactlyOneDrawNormallyAndTwoWithAssist()
        {
            RuleConfig rules=new RuleConfig {assistRefillRate=0.25f};
            Pcg32 actual=new Pcg32(5,2), expected=actual;
            Gravity.Apply(Make("-"),new[]{"R","B","G"},ref actual,false,rules,false);
            expected.NextInt(3);
            Assert.That(actual.NextUInt(),Is.EqualTo(expected.NextUInt()));
            actual=new Pcg32(5,2); expected=actual;
            Gravity.Apply(Make("-"),new[]{"R","B","G"},ref actual,true,rules,true);
            expected.NextInt(10000); expected.NextInt(3);
            Assert.That(actual.NextUInt(),Is.EqualTo(expected.NextUInt()));
        }
        [Test] public void AllThirtyLevelsPassSpecificationValidation()
        {
            LevelCatalog levels=DataLoader.LoadLevels(DataDirectory);
            CharacterCatalog characters=DataLoader.LoadCharacters(DataDirectory);
            GameConfig config=DataLoader.LoadConfig(DataDirectory);
            Assert.That(levels.levels.Length,Is.EqualTo(30));
            Assert.That(characters.characters.Length,Is.EqualTo(5));
            foreach(LevelData level in levels.levels)
                Assert.That(LevelValidator.Validate(level,characters,config.rules),Is.Empty,"Level "+level.id);
        }
        [Test] public void AllThirtyLevelsGenerateSameBoardForSameSeed()
        {
            LevelCatalog levels=DataLoader.LoadLevels(DataDirectory); RuleConfig rules=DataLoader.LoadConfig(DataDirectory).rules;
            foreach(LevelData level in levels.levels)
            {
                Pcg32 first=new Pcg32(63,1), firstShuffle=new Pcg32(63,4), second=first, secondShuffle=firstShuffle;
                Board a=BoardGenerator.Generate(level,rules,ref first,ref firstShuffle).board;
                Board b=BoardGenerator.Generate(level,rules,ref second,ref secondShuffle).board;
                Assert.That(Fingerprint(a),Is.EqualTo(Fingerprint(b)),"Level "+level.id);
                Assert.That(first.NextUInt(),Is.EqualTo(second.NextUInt()));
            }
        }
        [Test] public void ValidatorRejectsForbiddenIceBoxCombination()
        {
            LevelCatalog catalog=DataLoader.LoadLevels(DataDirectory);
            LevelData level=catalog.levels[1]; string original=level.layers.occupant[0]; string ice=level.layers.ice[0];
            level.layers.occupant[0]="X"+original.Substring(1); level.layers.ice[0]="1"+ice.Substring(1);
            Assert.That(LevelValidator.Validate(level,DataLoader.LoadCharacters(DataDirectory),DataLoader.LoadConfig(DataDirectory).rules),Is.Not.Empty);
        }
    }
}
