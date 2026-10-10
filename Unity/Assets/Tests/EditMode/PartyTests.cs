using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using StardustAtelier.Core;
using StardustAtelier.Meta;

namespace StardustAtelier.Tests
{
    // [실제 코드에 남길 주석] 기획서 12장 파티 스킬과 10.3 마지막 기회를 Core만으로 검사합니다(17장 E25~E27, E33~E37).
    public sealed class PartyTests
    {
        private static string Folder { get { return Path.Combine(UnityEngine.Application.streamingAssetsPath,"Data"); } }
        private static GameConfig Config() { return DataLoader.LoadConfig(Folder); }
        private static LevelData Level(int id) { return DataLoader.LoadLevels(Folder).levels[id-1]; }
        private static PartyMember Member(int id)
        {
            CharacterCatalog catalog = DataLoader.LoadCharacters(Folder);
            PartyMember member = new PartyMember { level = 1, costReductionPerLevel = catalog.growth.costReductionPerLevel };
            foreach (CharacterData c in catalog.characters) if (c.id == id) member.character = c;
            foreach (SkillData s in catalog.skills) if (s.id == member.character.skillId) member.skill = s;
            return member;
        }
        // 목표를 크게 잡아 테스트 도중 클리어되지 않게 합니다.
        private static LevelData Endless(int id, int moves)
        {
            LevelData level = Level(id); level.moves = moves;
            level.goals = new[] { new GoalData { type="Collect",color="R",count=999 } };
            return level;
        }
        private static void AnySwap(GameSession session)
        {
            List<ValidMove> moves = MoveFinder.Find(session.Board,session.Level.features.squareMatch);
            Assert.That(moves.Count,Is.GreaterThan(0));
            session.Swap(moves[0].a,moves[0].b);
        }

        [Test] public void RequiredGaugeUsesBaseCostAndLevel()
        {
            PartyMember sage = Member(105);
            Assert.That(sage.RequiredUnits(12),Is.EqualTo(15*12));
            sage.level = 21; // 15 × (1 − 0.015 × 20) = 10.5 → 반올림 11
            Assert.That(sage.RequiredUnits(12),Is.EqualTo(11*12));
        }
        [Test] public void LevelTwoSageStartsFullAndSkillLocksUntilNextSwap()
        {
            GameSession session = new GameSession(Endless(2,20),Config(),5,new[] { Member(105),null,null });
            Assert.That(session.GetSkillState(0),Is.EqualTo(SkillState.Ready));
            List<CellPosition> targets = session.SkillTargetsFor(0);
            Assert.That(targets.Count,Is.EqualTo(6));
            List<GameEvent> events = session.UseSkill(0);
            Assert.That(events.Exists(e=>e.kind==GameEventKind.SkillUsed),Is.True);
            Assert.That(session.MovesLeft,Is.EqualTo(20)); Assert.That(session.SkillLocked,Is.True);
            session.Gauge[0] = session.Required(0);
            Assert.That(session.GetSkillState(0),Is.EqualTo(SkillState.Locked));
            Assert.That(session.UseSkill(0),Is.Empty);
            AnySwap(session);
            Assert.That(session.SkillLocked,Is.False); Assert.That(session.GetSkillState(0),Is.EqualTo(SkillState.Ready));
        }
        [Test] public void ReopeningPreviewGivesSameTargets()
        {
            GameSession session = new GameSession(Endless(2,20),Config(),9,new[] { Member(105),null,null });
            Assert.That(session.SkillTargetsFor(0),Is.EqualTo(session.SkillTargetsFor(0)));
        }
        [Test] public void ChargeSplitsBetweenSameAttributeAndCapsAtRequired()
        {
            PartyMember second = Member(106); second.character.attr = "G"; // 같은 속성 2명(테스트 데이터, E37)
            GameSession session = new GameSession(Endless(9,22),Config(),3,new[] { Member(105),second });
            List<GameEvent> events = new List<GameEvent>();
            session.Goals.Record("Collect",BlockColor.G,events);
            Assert.That(session.Gauge[0],Is.EqualTo(6)); Assert.That(session.Gauge[1],Is.EqualTo(6));
            for (int i = 0; i < 100; i++) session.Goals.Record("Collect",BlockColor.G,events);
            Assert.That(session.Gauge[0],Is.EqualTo(session.Required(0))); // E36: 넘치는 충전은 버림
        }
        [Test] public void SkillTurnChargesOthersHalfAndNotTheCaster()
        {
            // 슬롯0: 맨 아래 줄을 치는 숲 속성, 슬롯1: 숲 속성. 맨 아래 줄을 초록 블록으로 채웁니다(E34).
            PartyMember caster = Member(106); caster.character.attr = "G";
            GameSession session = new GameSession(Endless(9,22),Config(),3,new[] { caster,Member(105) });
            int bottom = session.Board.Height-1;
            for (int c = 0; c < session.Board.Width; c++)
            { Cell cell = session.Board[bottom,c]; cell.ice = 0; cell.chained = false; cell.occupant = Occupant.Block(BlockColor.G); }
            session.Gauge[0] = session.Required(0);
            session.UseSkill(0);
            Assert.That(session.Gauge[0],Is.Zero);
            Assert.That(session.Gauge[1],Is.GreaterThanOrEqualTo(session.Board.Width*3));
            Assert.That(session.Gauge[1]%3,Is.Zero);
        }
        [Test] public void LastChanceWithMorAddsMoveThenFailsWhenLocked()
        {
            GameSession session = new GameSession(Endless(29,1),Config(),4,new[] { Member(109),null,null });
            session.Gauge[0] = session.Required(0);
            AnySwap(session);
            Assert.That(session.MovesLeft,Is.Zero); Assert.That(session.LastChance,Is.True);
            Assert.That(session.Outcome,Is.EqualTo(SessionOutcome.Idle));
            session.UseSkill(0); // E26: 모르 → 이동 1, 대기, 잠금
            Assert.That(session.MovesLeft,Is.EqualTo(1)); Assert.That(session.LastChance,Is.False);
            Assert.That(session.SkillLocked,Is.True);
            AnySwap(session);
            Assert.That(session.Outcome,Is.EqualTo(SessionOutcome.Fail));
        }
        [Test] public void FullButLockedAfterSkillTurnFails()
        {
            // E25: 마지막 기회에서 스킬을 쓰면 잠금이 걸려, 이동이 여전히 0이면 다른 가득 찬 스킬이 있어도 실패입니다.
            GameSession session = new GameSession(Endless(9,1),Config(),6,new[] { Member(106),Member(105) });
            session.Gauge[0] = session.Required(0); session.Gauge[1] = session.Required(1);
            AnySwap(session);
            Assert.That(session.LastChance,Is.True);
            session.UseSkill(0);
            Assert.That(session.Outcome,Is.EqualTo(SessionOutcome.Fail));
        }
        [Test] public void EndButtonFailsImmediately()
        {
            GameSession session = new GameSession(Endless(29,1),Config(),4,new[] { Member(109),null,null });
            session.Gauge[0] = session.Required(0);
            AnySwap(session);
            session.EndLastChance(); // E27
            Assert.That(session.Outcome,Is.EqualTo(SessionOutcome.Fail));
        }
        [Test] public void ConvertSkillWithoutNormalBlocksHasNoTarget()
        {
            GameSession session = new GameSession(Endless(16,22),Config(),2,new[] { Member(107),null,null });
            session.Gauge[0] = session.Required(0);
            foreach (CellPosition p in session.Board.Positions())
                if (session.Board[p].occupant.kind == OccupantKind.ColorBlock) session.Board[p].occupant = new Occupant { kind=OccupantKind.Box,hp=1,color=BlockColor.None };
            Assert.That(session.GetSkillState(0),Is.EqualTo(SkillState.NoTarget)); // E35
        }
        [Test] public void FioConvertsBlockToColorlessBombAndCountsItsColor()
        {
            LevelData level = Endless(16,22);
            GameSession session = new GameSession(level,Config(),2,new[] { Member(107),null,null });
            session.Gauge[0] = session.Required(0);
            CellPosition target = session.SkillTargetsFor(0)[0];
            BlockColor color = session.Board[target].occupant.color;
            List<GameEvent> events = session.UseSkill(0);
            Assert.That(events.Exists(e=>e.kind==GameEventKind.SpecialCreated && e.occupant.kind==OccupantKind.Bomb && e.occupant.color==BlockColor.None),Is.True);
            if (color == BlockColor.R) Assert.That(session.Goals.Remaining(0),Is.LessThan(999));
        }
    }
}
