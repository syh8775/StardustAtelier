using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StardustAtelier.Core;
using StardustAtelier.View;

namespace StardustAtelier.PlayTests
{
    public sealed class PlaySurfaceTests
    {
        private StardustApp app;
        // [실제 코드에 남길 주석] 실제 씬의 레이캐스트와 포인터 이벤트를 거쳐 입력부터 표시까지 검사합니다.
        private string saveDirectory;
        [UnitySetUp] public IEnumerator SetUp()
        {
            // 실제 저장을 건드리지 않도록 테스트마다 빈 임시 폴더를 저장 폴더로 씁니다.
            saveDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(),"stardust-play-test-"+Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(saveDirectory);
            StardustApp.SaveDirectoryOverride = saveDirectory;
            // 안내형 튜토리얼(레벨 1·2·4)은 지정 입력만 받으므로, 다른 검사들은 이미 본 상태로 시작합니다.
            StardustAtelier.Meta.SaveData seen = new StardustAtelier.Meta.SaveData();
            seen.tutorialsSeen.AddRange(new[] { 1,2,4 });
            StardustAtelier.Meta.SaveService.Save(saveDirectory,seen);
            SceneManager.LoadScene("Stardust");
            yield return null;
            app = UnityEngine.Object.FindFirstObjectByType<StardustApp>();
            app.StartLevel(0,63); app.SkipIntro();
            yield return Ready();
        }
        [TearDown] public void TearDown()
        {
            StardustApp.SaveDirectoryOverride = null;
            if (System.IO.Directory.Exists(saveDirectory)) System.IO.Directory.Delete(saveDirectory,true);
        }
        // 저장 파일에 적힌 진행(클리어·별·보유 캐릭터)이 앱을 다시 켜면 그대로 돌아옵니다(14.3).
        [UnityTest] public IEnumerator SavedProgressIsRestoredOnStart()
        {
            StardustAtelier.Meta.SaveData data = new StardustAtelier.Meta.SaveData();
            StardustAtelier.Meta.LevelSave level = StardustAtelier.Meta.SaveService.Level(data,1); level.cleared = true; level.bestStars = 2;
            data.characters["105"] = new StardustAtelier.Meta.CharacterSave();
            data.itemsSeen.Add("Rocket");
            StardustAtelier.Meta.SaveService.Save(saveDirectory,data);
            SceneManager.LoadScene("Stardust");
            yield return null; yield return null;
            app = UnityEngine.Object.FindFirstObjectByType<StardustApp>();
            Assert.That(app.Unlocked,Is.EqualTo(1)); Assert.That(app.BestStars(0),Is.EqualTo(2));
            Assert.That(app.Owned,Does.Contain(105)); Assert.That(app.SaveRecovered,Is.False);
            Assert.That(app.map.nodes[1].button.interactable,Is.True);
        }
        // 맵 [캐릭터] → 캐릭터 화면 → [레벨업]: 골드 차감·레벨 상승·즉시 저장, 골드가 모자라면 버튼이 꺼집니다(12.6).
        [UnityTest] public IEnumerator CharacterScreenLevelUpSpendsGoldAndSaves()
        {
            StardustAtelier.Meta.SaveData data = new StardustAtelier.Meta.SaveData { gold = 100 };
            StardustAtelier.Meta.SaveService.Level(data,1).cleared = true;
            data.characters["105"] = new StardustAtelier.Meta.CharacterSave();
            StardustAtelier.Meta.SaveService.Save(saveDirectory,data);
            SceneManager.LoadScene("Stardust");
            yield return null; yield return null;
            app = UnityEngine.Object.FindFirstObjectByType<StardustApp>();
            // 앱을 켜면 타이틀 화면이 먼저 나오고, 화면을 누르면 레벨 맵으로 갑니다(13.1).
            Assert.That(app.titleScreen.activeSelf,Is.True); Assert.That(app.map.gameObject.activeSelf,Is.False);
            Click(app.titleButton); yield return null;
            Assert.That(app.titleScreen.activeSelf,Is.False); Assert.That(app.map.gameObject.activeSelf,Is.True);
            Click(app.map.charactersButton); yield return null;
            Assert.That(app.characters.gameObject.activeSelf,Is.True); Assert.That(app.map.gameObject.activeSelf,Is.False);
            Assert.That(app.characters.SelectedId,Is.EqualTo(105)); Assert.That(app.characters.levelText.text,Is.EqualTo("Lv 1"));
            Click(app.characters.levelUp); yield return null;   // 1 → 2, 30 골드
            Click(app.characters.levelUp); yield return null;   // 2 → 3, 60 골드
            Assert.That(app.CharacterLevel(105),Is.EqualTo(3)); Assert.That(app.Gold,Is.EqualTo(10));
            Assert.That(app.characters.levelText.text,Is.EqualTo("Lv 3")); Assert.That(app.characters.levelUp.interactable,Is.False);
            bool recovered;
            StardustAtelier.Meta.SaveData saved = StardustAtelier.Meta.SaveService.Load(saveDirectory,out recovered);
            Assert.That(saved.characters["105"].level,Is.EqualTo(3)); Assert.That(saved.gold,Is.EqualTo(10));
            Click(app.characters.back); yield return null;
            Assert.That(app.map.gameObject.activeSelf,Is.True); Assert.That(app.map.goldText.text,Is.EqualTo("10"));
        }
        // 맵 [설정] → 배경음 음량을 줄이면 소리에 반영되고 저장됩니다(13.2). 100이 기본 음량입니다.
        [UnityTest] public IEnumerator VolumeSliderChangesAndSaves()
        {
            app.ShowMap(); yield return null;
            Click(app.map.settingsButton); yield return null;
            Assert.That(app.modal.activeSelf,Is.True); Assert.That(app.popupTitle.text,Is.EqualTo("설정"));
            Assert.That(app.BgmVolume,Is.EqualTo(100)); Assert.That(app.bgmSlider.value,Is.EqualTo(100));
            Assert.That(app.bgmSlider.maxValue,Is.EqualTo(100));
            app.bgmSlider.value = 30; yield return null; yield return null;
            Assert.That(app.BgmVolume,Is.EqualTo(30)); Assert.That(app.bgmValue.text,Is.EqualTo("30"));
            bool recovered;
            StardustAtelier.Meta.SaveData saved = StardustAtelier.Meta.SaveService.Load(saveDirectory,out recovered);
            Assert.That(saved.settings.bgmVolume,Is.EqualTo(30)); Assert.That(saved.settings.sfxVolume,Is.EqualTo(100));
            app.sfxSlider.value = 0; yield return null; yield return null;
            saved = StardustAtelier.Meta.SaveService.Load(saveDirectory,out recovered);
            Assert.That(saved.settings.sfxVolume,Is.Zero); Assert.That(saved.settings.sfx,Is.False);
            Click(app.primary); yield return null;
            Assert.That(app.modal.activeSelf,Is.False);
        }
        // 실패 보정(10.5): 같은 레벨을 연속 3회 실패하면 켜지고, 고비 레벨은 켜지지 않습니다.
        [UnityTest] public IEnumerator AssistTurnsOnAfterThreeFailsExceptHardLevels()
        {
            StardustAtelier.Meta.SaveData data = new StardustAtelier.Meta.SaveData();
            for (int id = 1; id <= 9; id++) { StardustAtelier.Meta.LevelSave cleared = StardustAtelier.Meta.SaveService.Level(data,id); cleared.cleared = true; cleared.bestStars = 1; }
            StardustAtelier.Meta.SaveService.Level(data,2).consecutiveFails = 2;
            StardustAtelier.Meta.SaveService.Level(data,3).consecutiveFails = 3;
            StardustAtelier.Meta.SaveService.Level(data,10).consecutiveFails = 5;   // 레벨 10은 고비
            StardustAtelier.Meta.SaveService.Save(saveDirectory,data);
            SceneManager.LoadScene("Stardust");
            yield return null; yield return null;
            app = UnityEngine.Object.FindFirstObjectByType<StardustApp>();
            app.StartLevel(1,7); Assert.That(app.Session.Assisted,Is.False);
            app.StartLevel(2,7); Assert.That(app.Session.Assisted,Is.True);
            app.StartLevel(9,7); Assert.That(app.Session.Level.hard,Is.True); Assert.That(app.Session.Assisted,Is.False);
            app.SkipIntro(); yield return Ready();
        }
        // 저장을 비우고 씬을 다시 불러 처음 플레이하는 상태로 만듭니다.
        private IEnumerator FreshStart()
        {
            foreach (string file in System.IO.Directory.GetFiles(saveDirectory)) System.IO.File.Delete(file);
            SceneManager.LoadScene("Stardust");
            yield return null; yield return null;
            app = UnityEngine.Object.FindFirstObjectByType<StardustApp>();
        }
        // 안내형 튜토리얼(13.3): 처음 플레이하는 레벨 1은 손가락이 가리키는 교환만 받습니다.
        [UnityTest] public IEnumerator GuidedTutorialAcceptsOnlyTheShownSwap()
        {
            yield return FreshStart();
            app.StartLevel(0,63); app.SkipIntro(); yield return Ready();
            Assert.That(app.GuideActive,Is.True); Assert.That(app.guide.Visible,Is.True);
            int[][] swap = app.Session.Level.tutorial.swap;
            CellPosition a = new CellPosition(swap[0][0],swap[0][1]), b = new CellPosition(swap[1][0],swap[1][1]);
            // 다른 유효한 수는 무시합니다.
            foreach (ValidMove move in MoveFinder.Find(app.Session.Board,false))
            {
                bool shown = (move.a.Equals(a) && move.b.Equals(b)) || (move.a.Equals(b) && move.b.Equals(a));
                if (shown) continue;
                DragSwap(move.a,move.b); yield return null;
                Assert.That(app.Session.Turns,Is.EqualTo(0)); Assert.That(app.Busy,Is.False);
                break;
            }
            DragSwap(a,b);
            Assert.That(app.Session.Turns,Is.EqualTo(1));
            Assert.That(app.guide.Visible,Is.False);
            yield return Ready();
            Assert.That(app.GuideActive,Is.False);
            // 지정 입력을 마치면 본 것으로 저장하고, 다시 시작해도 안내하지 않습니다.
            bool recovered; Assert.That(StardustAtelier.Meta.SaveService.Load(saveDirectory,out recovered).tutorialsSeen,Does.Contain(1));
            app.StartLevel(0,63); app.SkipIntro(); yield return Ready();
            Assert.That(app.GuideActive,Is.False); Assert.That(app.guide.Visible,Is.False);
        }
        // 스킬 안내(레벨 2): 보드와 [취소]는 받지 않고, 세이지 초상화 → [발동]만 받습니다.
        [UnityTest] public IEnumerator GuidedSkillAcceptsOnlyPortraitThenUse()
        {
            yield return FreshStart();
            app.StartLevel(1,5,new[] { 105,0,0 }); app.SkipIntro(); yield return Ready();
            Assert.That(app.GuideActive,Is.True); Assert.That(app.guide.Visible,Is.True);
            ValidMove move = MoveFinder.Find(app.Session.Board,false)[0];
            DragSwap(move.a,move.b); yield return null;
            Assert.That(app.Session.Turns,Is.EqualTo(0));
            Click(app.hud.party.slots[0].button); yield return null;
            Assert.That(app.PreviewSlot,Is.EqualTo(0));
            // 미리보기 중에 보드를 누르거나 [취소]를 눌러도 미리보기가 유지됩니다.
            Tap(move.a); Click(app.hud.skillCancel);
            Assert.That(app.PreviewSlot,Is.EqualTo(0));
            Click(app.hud.skillUse);
            Assert.That(app.guide.Visible,Is.False);
            yield return Ready();
            Assert.That(app.GuideActive,Is.False);
            Assert.That(app.LastEvents.Exists(e=>e.kind==GameEventKind.SkillUsed),Is.True);
        }
        // 테스트용 도구: 레벨 바로 가기는 잠긴 레벨의 진입 팝업을 열고, 진행 초기화는 처음 상태로 되돌립니다.
        [UnityTest] public IEnumerator DevToolsJumpLevelAndResetProgress()
        {
            app.JumpLevel(4); yield return null;
            Assert.That(app.modal.activeSelf,Is.True); Assert.That(app.popupTitle.text,Is.EqualTo("레벨 5"));
            Click(app.primary); app.SkipIntro(); yield return Ready();
            Assert.That(app.Session.Level.id,Is.EqualTo(5));
            app.ResetProgress(); yield return null;
            Assert.That(app.map.gameObject.activeSelf,Is.True);
            bool recovered;
            StardustAtelier.Meta.SaveData data = StardustAtelier.Meta.SaveService.Load(saveDirectory,out recovered);
            Assert.That(data.tutorialsSeen,Is.Empty); Assert.That(data.gold,Is.EqualTo(0));
            // 초기화 뒤에는 잠금도 처음 상태입니다: 레벨 2 진입 팝업이 열리지 않습니다.
            app.OpenEntry(1); Assert.That(app.modal.activeSelf,Is.False);
        }
        private IEnumerator Ready()
        {
            float deadline = Time.realtimeSinceStartup+12;
            while (app.Busy && Time.realtimeSinceStartup < deadline)
            {
                // 아이템 첫 생성 설명 팝업은 [확인]으로 닫고 계속합니다.
                if (app.ItemTutorialOpen) app.primary.onClick.Invoke();
                yield return null;
            }
            Assert.That(app.Busy,Is.False,"Animation did not finish.");
            yield return null;
            Canvas.ForceUpdateCanvases();
        }
        private PointerEventData Data(GameObject target)
        {
            Canvas.ForceUpdateCanvases();
            PointerEventData data = new PointerEventData(EventSystem.current);
            data.position = RectTransformUtility.WorldToScreenPoint(null,target.transform.position);
            List<RaycastResult> hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data,hits);
            Assert.That(hits.Count,Is.GreaterThan(0),target.name+" has no raycast hit.");
            Assert.That(hits[0].gameObject,Is.EqualTo(target),target.name+" is covered.");
            data.pointerCurrentRaycast = hits[0]; data.pointerPressRaycast = hits[0];
            return data;
        }
        private void Tap(CellPosition p)
        {
            GameObject target = app.board.At(p).gameObject; PointerEventData data = Data(target);
            ExecuteEvents.Execute(target,data,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target,data,ExecuteEvents.pointerUpHandler);
        }
        private void Click(UnityEngine.UI.Button button)
        {
            PointerEventData data = Data(button.gameObject);
            ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
        }
        private void AssertSynchronized()
        {
            foreach (CellPosition p in app.Session.Board.Positions())
            {
                Cell core = app.Session.Board[p], shown = app.board.ViewBoard[p];
                Assert.That(shown.occupant.kind,Is.EqualTo(core.occupant.kind),p.ToString());
                Assert.That(shown.occupant.color,Is.EqualTo(core.occupant.color),p.ToString());
                Assert.That(shown.ice,Is.EqualTo(core.ice),p.ToString());
                Assert.That(shown.chained,Is.EqualTo(core.chained),p.ToString());
            }
            Assert.That(app.hud.moves.text,Is.EqualTo(app.Session.MovesLeft.ToString()));
        }

        private void PlaceSpecial(CellPosition p, OccupantKind kind, bool chained = false)
        {
            app.Session.Board[p].occupant = new Occupant { kind=kind,color=kind==OccupantKind.Prism ? BlockColor.None : BlockColor.B,orientation=Orientation.H };
            app.Session.Board[p].chained=chained;
            app.board.Configure(app.Session.Board,app.Config.layout.boardAreaPx);
        }
        private void DragSwap(CellPosition a, CellPosition b)
        {
            GameObject target = app.board.At(a).gameObject; PointerEventData data = Data(target);
            ExecuteEvents.Execute(target,data,ExecuteEvents.pointerDownHandler);
            data.position = RectTransformUtility.WorldToScreenPoint(null,app.board.At(b).transform.position);
            ExecuteEvents.Execute(target,data,ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(target,data,ExecuteEvents.pointerUpHandler);
        }
        [UnityTest] public IEnumerator OneClickActivatesEverySpecialAndSynchronizesReplay()
        {
            foreach (OccupantKind kind in new[] { OccupantKind.Rocket,OccupantKind.Bomb,OccupantKind.Meteor,OccupantKind.Prism })
            {
                app.StartLevel(0,63); app.SkipIntro(); yield return Ready();
                CellPosition p = new CellPosition(0,0); PlaceSpecial(p,kind);
                Tap(p);
                Assert.That(app.Session.MovesLeft,Is.EqualTo(19)); Assert.That(app.Session.Turns,Is.EqualTo(1));
                Assert.That(app.LastEvents.Exists(e=>e.kind==GameEventKind.SpecialActivated && e.occupant.kind==kind),Is.True);
                Assert.That(app.LastEvents.Exists(e=>e.kind==GameEventKind.Swapped),Is.False);
                yield return Ready(); AssertSynchronized();
            }
        }
        [UnityTest] public IEnumerator SelectedNormalDoesNotDelaySpecialClick()
        {
            CellPosition p = new CellPosition(0,1); PlaceSpecial(p,OccupantKind.Bomb);
            Tap(new CellPosition(0,0)); Tap(p); yield return Ready();
            Assert.That(app.Session.Turns,Is.EqualTo(1));
            Assert.That(app.LastEvents.Exists(e=>e.kind==GameEventKind.Swapped),Is.False); AssertSynchronized();
        }
        [UnityTest] public IEnumerator ChainedSpecialClickCannotConsumeMove()
        {
            CellPosition p = new CellPosition(0,0); PlaceSpecial(p,OccupantKind.Rocket,true);
            Tap(p); yield return null;
            Assert.That(app.Session.MovesLeft,Is.EqualTo(20)); Assert.That(app.Session.Turns,Is.Zero);
            Assert.That(app.Session.Board[p].chained,Is.True); AssertSynchronized();
        }
        [UnityTest] public IEnumerator ExtraSpecialClickWhileBusyDoesNotSpendSecondMove()
        {
            CellPosition first = new CellPosition(0,0), second = new CellPosition(6,6);
            PlaceSpecial(first,OccupantKind.Rocket); PlaceSpecial(second,OccupantKind.Bomb);
            Tap(first); Assert.That(app.Busy,Is.True);
            Tap(second); yield return Ready();
            Assert.That(app.Session.Turns,Is.EqualTo(1)); Assert.That(app.Session.MovesLeft,Is.EqualTo(19)); AssertSynchronized();
        }
        [UnityTest] public IEnumerator DragStillCombinesTwoSpecialsWithoutExtraClickActivation()
        {
            CellPosition first = new CellPosition(0,0), second = new CellPosition(0,1);
            PlaceSpecial(first,OccupantKind.Rocket); PlaceSpecial(second,OccupantKind.Bomb);
            DragSwap(first,second); yield return Ready();
            Assert.That(app.Session.Turns,Is.EqualTo(1)); Assert.That(app.Session.MovesLeft,Is.EqualTo(19));
            Assert.That(app.LastEvents.Exists(e=>e.kind==GameEventKind.SpecialActivated && e.value==2),Is.True); AssertSynchronized();
        }
        [UnityTest] public IEnumerator FirstBoardIsVisibleAndInteractive()
        {
            Assert.That(app.board.cells.Length,Is.EqualTo(81));
            int visible = 0;
            foreach (CellView cell in app.board.cells) if (cell.gameObject.activeInHierarchy) visible++;
            Assert.That(visible,Is.EqualTo(49));
            Assert.That(app.hud.goalTexts[0].text,Is.EqualTo("12"));
            Assert.That(app.CanInput,Is.True); AssertSynchronized();
            yield return null;
        }
        [UnityTest] public IEnumerator ClickSwapUpdatesBoardMovesAndGoalAfterReplay()
        {
            Tap(new CellPosition(4,3)); Tap(new CellPosition(3,3)); yield return Ready();
            Assert.That(app.Session.MovesLeft,Is.EqualTo(19));
            Assert.That(app.Session.Goals.Remaining(0),Is.LessThan(12));
            Assert.That(app.hud.goalTexts[0].text,Is.EqualTo(app.Session.Goals.Remaining(0).ToString()));
            AssertSynchronized();
        }
        [UnityTest] public IEnumerator InvalidClickSwapReturnsWithoutSpendingMove()
        {
            Tap(new CellPosition(0,0)); Tap(new CellPosition(0,1)); yield return Ready();
            Assert.That(app.Session.MovesLeft,Is.EqualTo(20)); Assert.That(app.Session.Goals.Remaining(0),Is.EqualTo(12));
            AssertSynchronized();
        }
        [UnityTest] public IEnumerator DragAcceptsAtThresholdBeforePointerUp()
        {
            CellView first = app.board.At(new CellPosition(4,3)), second = app.board.At(new CellPosition(3,3));
            PointerEventData data = Data(first.gameObject);
            ExecuteEvents.Execute(first.gameObject,data,ExecuteEvents.pointerDownHandler);
            data.position = RectTransformUtility.WorldToScreenPoint(null,second.transform.position);
            ExecuteEvents.Execute(first.gameObject,data,ExecuteEvents.dragHandler);
            Assert.That(app.Session.MovesLeft,Is.EqualTo(19),"Drag should execute before release.");
            ExecuteEvents.Execute(first.gameObject,data,ExecuteEvents.pointerUpHandler);
            yield return Ready(); Assert.That(app.Session.Turns,Is.EqualTo(1)); AssertSynchronized();
        }
        [UnityTest] public IEnumerator DragStartedDuringResolutionNeverReappears()
        {
            Tap(new CellPosition(4,3)); Tap(new CellPosition(3,3));
            Assert.That(app.Busy,Is.True);
            CellView first = app.board.At(new CellPosition(0,0)), second = app.board.At(new CellPosition(0,1));
            PointerEventData data = Data(first.gameObject);
            ExecuteEvents.Execute(first.gameObject,data,ExecuteEvents.pointerDownHandler);
            yield return Ready();
            data.position = RectTransformUtility.WorldToScreenPoint(null,second.transform.position);
            ExecuteEvents.Execute(first.gameObject,data,ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(first.gameObject,data,ExecuteEvents.pointerUpHandler);
            Assert.That(app.Session.Turns,Is.EqualTo(1)); Assert.That(app.Session.MovesLeft,Is.EqualTo(19));
        }
        [UnityTest] public IEnumerator PauseAndContinueButtonsPreserveState()
        {
            Click(app.hud.pause); Assert.That(app.modal.activeSelf,Is.True); Assert.That(app.CanInput,Is.False);
            Assert.That(app.popupTitle.text,Is.EqualTo("일시정지"));
            yield return null;
            Click(app.primary); Assert.That(app.modal.activeSelf,Is.False);
            Assert.That(app.CanInput,Is.True); Assert.That(app.Session.MovesLeft,Is.EqualTo(20));
            yield return null;
        }
        [UnityTest] public IEnumerator RestartRequiresConfirmationAndRestoresRealData()
        {
            Tap(new CellPosition(4,3)); Tap(new CellPosition(3,3)); yield return Ready();
            // 기획서 13.2: 다시 시작은 일시정지 팝업 안에 있고, 이동을 썼으면 확인을 거칩니다.
            Click(app.hud.pause); yield return null;
            Click(app.secondary); Assert.That(app.popupTitle.text,Does.Contain("시작할까요"));
            yield return null;
            Click(app.primary); app.SkipIntro(); yield return Ready();
            Assert.That(app.Session.MovesLeft,Is.EqualTo(20)); Assert.That(app.Session.Goals.Remaining(0),Is.EqualTo(12));
            Assert.That(app.Session.Turns,Is.Zero); AssertSynchronized();
        }
        [UnityTest] public IEnumerator MapNodeOpensEntryPopupAndStartsLevel()
        {
            app.ShowMap(); yield return null;
            Assert.That(app.gameScreen.activeSelf,Is.False); Assert.That(app.map.nodes[1].button.interactable,Is.False);
            Click(app.map.nodes[0].button); yield return null;
            Assert.That(app.modal.activeSelf,Is.True); Assert.That(app.popupTitle.text,Does.StartWith("레벨 1"));
            Click(app.primary); app.SkipIntro(); yield return Ready();
            Assert.That(app.gameScreen.activeSelf,Is.True); Assert.That(app.Session.Level.id,Is.EqualTo(1)); AssertSynchronized();
        }
        // 레벨 2: 가득 찬 세이지 초상화 클릭 → 미리보기 → [발동]. 이동은 그대로, 게이지 0, 파티 잠금(12.4).
        [UnityTest] public IEnumerator PortraitPreviewAndUseSkillThroughRealButtons()
        {
            app.StartLevel(1,5,new[] { 105,0,0 }); app.SkipIntro(); yield return Ready();
            Assert.That(app.hud.party.gameObject.activeSelf,Is.True);
            Click(app.hud.party.slots[0].button);
            Assert.That(app.PreviewSlot,Is.EqualTo(0)); Assert.That(app.CanInput,Is.False);
            yield return null;
            Click(app.hud.skillUse); yield return Ready();
            Assert.That(app.Session.MovesLeft,Is.EqualTo(20)); Assert.That(app.Session.Gauge[0],Is.Zero);
            Assert.That(app.Session.SkillLocked,Is.True);
            Assert.That(app.LastEvents.Exists(e=>e.kind==GameEventKind.SkillUsed),Is.True); AssertSynchronized();
        }
        // 아이템이 처음 생긴 턴이 끝나면 설명 팝업이 한 번 뜹니다(레벨 4 안내 스왑 = 가로 4개 → 로켓).
        [UnityTest] public IEnumerator FirstRocketShowsItemExplanationOnce()
        {
            app.StartLevel(3,63); app.SkipIntro(); yield return Ready();
            DragSwap(new CellPosition(2,3),new CellPosition(3,3));
            float deadline = Time.realtimeSinceStartup+12;
            while (!app.ItemTutorialOpen && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(app.ItemTutorialOpen,Is.True); Assert.That(app.popupTitle.text,Is.EqualTo("새로운 아이템!"));
            Assert.That(app.Busy,Is.True);
            yield return null;
            Click(app.primary); yield return Ready();
            Assert.That(app.ItemTutorialOpen,Is.False); Assert.That(app.modal.activeSelf,Is.False); Assert.That(app.CanInput,Is.True);
        }
        [UnityTest] public IEnumerator DraggedRocketActivatesWithoutMatch()
        {
            CellPosition a = new CellPosition(0,0), b = new CellPosition(0,1); PlaceSpecial(a,OccupantKind.Rocket);
            DragSwap(a,b); yield return Ready();
            Assert.That(app.Session.MovesLeft,Is.EqualTo(19));
            Assert.That(app.LastEvents.Exists(e=>e.kind==GameEventKind.SpecialActivated && e.position.Equals(b)),Is.True); AssertSynchronized();
        }
        [UnityTest] public IEnumerator RealMovesReachClearAndNextLevelButtonOpensLevelTwo()
        {
            for (int turn = 0; turn < 20 && app.Session.Outcome == SessionOutcome.Idle; turn++)
            {
                List<ValidMove> moves = MoveFinder.Find(app.Session.Board,false);
                int bestScore = int.MinValue; ValidMove best = moves[0];
                foreach (ValidMove move in moves)
                {
                    Board copy = app.Session.Board.Clone(); copy.Swap(move.a,move.b);
                    int score = 0;
                    foreach (MatchGroup group in MatchFinder.Find(copy,false))
                    {
                        if (group.color == BlockColor.R) score += group.cells.Count*10;
                        if (group.GetSpecial().kind != OccupantKind.Empty) score += 3;
                    }
                    if (score > bestScore) { bestScore = score; best = move; }
                }
                DragSwap(best.a,best.b); yield return Ready();
            }
            Assert.That(app.Session.Outcome,Is.EqualTo(SessionOutcome.Clear));
            Assert.That(app.modal.activeSelf,Is.True); Assert.That(app.popupTitle.text,Is.EqualTo("클리어!"));
            Assert.That(app.primaryText.text,Is.EqualTo("다음 레벨"));
            // 결과 팝업을 띄울 때 진행이 저장됩니다: 레벨 1 클리어, 세이지 보유, 시도 1회(14.3).
            bool recovered;
            StardustAtelier.Meta.SaveData saved = StardustAtelier.Meta.SaveService.Load(saveDirectory,out recovered);
            Assert.That(recovered,Is.False); Assert.That(saved.levels["1"].cleared,Is.True);
            Assert.That(saved.levels["1"].bestStars,Is.EqualTo(app.Session.Stars)); Assert.That(saved.levels["1"].attempts,Is.EqualTo(1));
            Assert.That(saved.characters.ContainsKey("105"),Is.True);
            // 첫 클리어 골드 = 100 + 50 × (별 − 1). 결과 팝업에 보여주고 저장합니다(10.4).
            int gold = 100+50*(app.Session.Stars-1);
            Assert.That(saved.gold,Is.EqualTo(gold)); Assert.That(app.Gold,Is.EqualTo(gold));
            Assert.That(app.popupGoldRow.activeSelf,Is.True); Assert.That(app.popupGoldText.text,Is.EqualTo("+"+gold));
            // 다음 레벨은 레벨 진입 팝업을 거쳐 [시작]으로 들어갑니다(13.1).
            Click(app.primary); yield return null;
            // 레벨 1 첫 클리어로 세이지가 합류하면 합류 팝업을 먼저 닫습니다(13.1).
            Assert.That(app.popupTitle.text,Is.EqualTo("세이지를 만났어요!"));
            Click(app.primary); yield return null;
            Assert.That(app.popupTitle.text,Does.StartWith("레벨 2"));
            Click(app.primary); app.SkipIntro(); yield return Ready();
            Assert.That(app.Session.Level.id,Is.EqualTo(2)); Assert.That(app.Session.MovesLeft,Is.EqualTo(20));
        }
    }
}
