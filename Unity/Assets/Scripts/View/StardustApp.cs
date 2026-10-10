using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using StardustAtelier.Core;
using StardustAtelier.Meta;

namespace StardustAtelier.View
{
    public sealed class StardustApp : MonoBehaviour
    {
        public BoardView board;
        public HudView hud;
        public MapView map;
        public CellArt art;
        // 화면: 레벨 맵과 인게임. 나머지는 팝업입니다(기획서 13.1).
        public GameObject gameScreen;
        // 팝업 하나를 여러 용도(진입·일시정지·확인·결과·튜토리얼·합류)로 다시 씁니다.
        public GameObject modal, popupStarRow, popupGoalRow, popupPicture, popupPartyRow;
        public RectTransform popupCard;
        public UnityEngine.UI.Image popupPictureImage;
        public UnityEngine.UI.Image[] popupStars, popupGoalImages, popupRecommended;
        public TMP_Text[] popupGoalTexts;
        public TMP_Text popupTitle, popupBody;
        public UnityEngine.UI.Button primary, secondary, tertiary;
        public TMP_Text primaryText, secondaryText, tertiaryText;
        // 레벨 진입 팝업의 파티 슬롯과 캐릭터 고르기 패널(12.2, 13.2).
        public PartySlotView[] popupPartySlots;
        public GameObject picker;
        public UnityEngine.UI.Button[] pickerButtons;
        public UnityEngine.UI.Image[] pickerFaces;
        public UnityEngine.UI.Button pickerClear, pickerClose;
        // 목표 안내 배너(7.3, 1.2초, 탭으로 건너뛰기)와 클리어 배너(0.8초).
        public GameObject introBanner, clearBanner;
        public UnityEngine.UI.Button introSkip;
        public TMP_Text introTitle, introMoves;
        public UnityEngine.UI.Image[] introGoalImages;
        public TMP_Text[] introGoalTexts;

        public GameSession Session { get; private set; }
        public GameConfig Config { get; private set; }
        public bool Busy { get; private set; }
        public bool Intro { get; private set; }
        // 스킬 미리보기 중인 슬롯. -1이면 미리보기 아님.
        public int PreviewSlot { get; private set; }
        // 보드 입력은 대기 상태에서만 받습니다. 마지막 기회·스킬 미리보기에서는 스왑을 받지 않습니다.
        public bool CanInput
        {
            get
            {
                return !Busy && !modal.activeSelf && gameScreen.activeSelf && Session != null &&
                    Session.Outcome == SessionOutcome.Idle && !Session.LastChance && PreviewSlot < 0;
            }
        }
        public List<GameEvent> LastEvents { get; private set; }
        private List<LevelData> levels;
        private CharacterCatalog catalog;
        private CellPosition? selection;
        private bool skipIntro, hintVisible;
        private float hintTime;
        // 진행 저장(14.3)이 붙기 전까지는 실행 중에만 기억합니다. unlocked는 열린 마지막 레벨의 번호 순서(0부터)입니다.
        private int unlocked, current;
        private int[] bestStars;
        private readonly HashSet<int> tutorialsSeen = new HashSet<int>();
        // 보유 캐릭터와 마지막으로 쓴 파티(0 = 빈 슬롯). 레벨 N의 캐릭터는 레벨 N−1 첫 클리어 때 지급합니다(12.1).
        private readonly List<int> owned = new List<int>();
        private int[] lastParty = new int[0];
        private int[] entryParty = new int[0];
        private int pickerSlot = -1, pendingJoin, entryIndex;

        // 기획서 13.4 튜토리얼 문구. 레벨 데이터는 textKey만 가지고 있습니다.
        private static readonly Dictionary<string,string> tutorialTexts = new Dictionary<string,string>
        {
            { "tut_match", "같은 색 3개를 줄지어 보세요" },
            { "tut_rocket", "4개를 맞추면 로켓이 생겨요" },
            { "tut_skill", "블록을 모으면 캐릭터 게이지가 차요.\n가득 찬 캐릭터를 눌러 스킬을 쓰세요" },
            { "tut_ice", "얼음 위의 블록을 없애면 얼음이 깨져요" },
            { "tut_box", "상자는 옆에서 매치해 깨요.\n상자가 있으면 위에서 블록이 내려오지 않아요" },
            { "tut_relic", "유물을 맨 아래까지 내리면 수집해요" },
            { "tut_square", "이제 2×2로도 맞출 수 있어요.\n별똥별이 목표를 찾아가요" },
            { "tut_vine", "덩굴은 옆에서 매치해 없애요.\n한 턴 동안 건드리지 않으면 번져요" },
            { "tut_chain", "사슬에 묶인 블록은 움직일 수 없어요.\n매치에 포함하면 풀려요" },
        };
        private static readonly Dictionary<string,string> attrNames = new Dictionary<string,string>
        { { "R", "불" }, { "B", "물" }, { "G", "숲" }, { "Y", "빛" }, { "P", "어둠" } };

        private void Start()
        {
            Application.runInBackground = true; Application.targetFrameRate = 60;
            Load();
            hud.pause.onClick.AddListener(Pause);
            hud.skillUse.onClick.AddListener(UseSkillNow);
            hud.skillCancel.onClick.AddListener(CancelSkill);
            hud.endButton.onClick.AddListener(EndLastChance);
            hud.party.Bind(this);
            introSkip.onClick.AddListener(SkipIntro);
            for (int i = 0; i < popupPartySlots.Length; i++) { int slot = i; popupPartySlots[i].button.onClick.AddListener(() => OpenPicker(slot)); }
            for (int i = 0; i < pickerButtons.Length; i++) { int index = i; pickerButtons[i].onClick.AddListener(() => Pick(index)); }
            pickerClear.onClick.AddListener(() => Pick(-1));
            pickerClose.onClick.AddListener(() => picker.SetActive(false));
            map.Bind(this);
            ShowMap();
        }
        public void Load()
        {
            if (Config != null && levels != null && levels.Count > 0) return;
            string directory = Path.Combine(Application.streamingAssetsPath,"Data");
            Config = DataLoader.LoadConfig(directory);
            levels = new List<LevelData>(DataLoader.LoadLevels(directory).levels);
            catalog = DataLoader.LoadCharacters(directory);
            bestStars = new int[levels.Count];
            PreviewSlot = -1;
        }
        public IList<LevelData> Levels { get { Load(); return levels; } }
        public int Unlocked { get { return unlocked; } }
        public int BestStars(int index) { return bestStars[index]; }
        public bool Playable(int index) { return index >= 0 && index < levels.Count; }
        public IList<int> Owned { get { return owned; } }

        // 씬 생성기가 Editor에서 첫 화면을 미리 그릴 때만 씁니다.
        public void Preview()
        {
            Load();
            Session = new GameSession(levels[0],Config,63);
            gameScreen.SetActive(true); map.gameObject.SetActive(false);
            hud.Configure(Session.Level,Config); hud.party.Configure(Session,art);
            board.Configure(Session.Board,Config.layout.boardAreaPx);
            hud.ShowTip(tutorialTexts["tut_match"]);
            ShowHint();
        }
        // party가 없으면(다시 시작 등) 기본 파티(마지막으로 쓴 파티)로 시작합니다.
        public void StartLevel(int index, ulong? seed = null, int[] party = null)
        {
            Load(); StopAllCoroutines(); current = index;
            LevelData level = levels[index];
            int[] ids = party != null && party.Length == level.partySlots ? party : DefaultParty(level);
            Session = new GameSession(level,Config,seed ?? unchecked((ulong)DateTime.UtcNow.Ticks),BuildParty(ids));
            lastParty = (int[])ids.Clone();
            selection = null; LastEvents = new List<GameEvent>(); PreviewSlot = -1;
            modal.SetActive(false); picker.SetActive(false); clearBanner.SetActive(false);
            map.gameObject.SetActive(false); gameScreen.SetActive(true);
            hud.Configure(Session.Level,Config); hud.party.Configure(Session,art); hud.party.Highlight(-1);
            board.Configure(Session.Board,Config.layout.boardAreaPx);
            Busy = true; Intro = true; skipIntro = false; hintTime = 0; hintVisible = false; hud.Busy(true);
            StartCoroutine(BeginLevel());
        }
        private PartyMember[] BuildParty(int[] ids)
        {
            PartyMember[] party = new PartyMember[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] == 0) continue;
                // 성장(12.6)이 붙기 전까지 모든 캐릭터는 Lv 1입니다.
                PartyMember member = new PartyMember { level = 1, costReductionPerLevel = catalog.growth.costReductionPerLevel };
                foreach (CharacterData c in catalog.characters) if (c.id == ids[i]) member.character = c;
                foreach (SkillData s in catalog.skills) if (member.character != null && s.id == member.character.skillId) member.skill = s;
                if (member.character != null && member.skill != null) party[i] = member;
            }
            return party;
        }
        // 기본 파티(12.2): 마지막으로 쓴 파티. 없거나 슬롯이 늘었으면 빈 슬롯을
        // 추천 속성 캐릭터 → 남은 보유 캐릭터(ID 오름차순)로 채웁니다.
        private int[] DefaultParty(LevelData level)
        {
            int[] result = new int[level.partySlots];
            bool grew = lastParty.Length < result.Length;
            for (int i = 0; i < result.Length && i < lastParty.Length; i++)
                if (owned.Contains(lastParty[i]) && Array.IndexOf(result,lastParty[i]) < 0) result[i] = lastParty[i];
            if (lastParty.Length > 0 && !grew) return result;
            List<int> sorted = new List<int>(owned); sorted.Sort();
            for (int i = 0; i < result.Length; i++)
            {
                if (result[i] != 0) continue;
                int choice = 0;
                if (level.recommended != null)
                    foreach (string attr in level.recommended)
                    {
                        foreach (int id in sorted) if (Array.IndexOf(result,id) < 0 && Character(id).attr == attr) { choice = id; break; }
                        if (choice != 0) break;
                    }
                if (choice == 0) foreach (int id in sorted) if (Array.IndexOf(result,id) < 0) { choice = id; break; }
                result[i] = choice;
            }
            return result;
        }
        private CharacterData Character(int id)
        {
            foreach (CharacterData c in catalog.characters) if (c.id == id) return c;
            return null;
        }
        private IEnumerator BeginLevel()
        {
            TutorialData tutorial = Session.Level.tutorial;
            // 설명형 튜토리얼은 목표 안내 배너 대신 설명 팝업을 먼저 띄웁니다(13.4).
            if (tutorial != null && tutorial.type == "Popup" && !IsItemTutorial(tutorial) && !tutorialsSeen.Contains(Session.Level.id) && !skipIntro)
            {
                if (tutorial.textKey == "tut_square") itemsExplained.Add(OccupantKind.Meteor);
                bool closed = false;
                bool obstacle = tutorial.textKey == "tut_ice" || tutorial.textKey == "tut_box" || tutorial.textKey == "tut_relic" ||
                    tutorial.textKey == "tut_vine" || tutorial.textKey == "tut_chain";
                Popup(obstacle ? "새로운 장애물!" : "새로운 블록!",TutorialText(tutorial),"확인",() => { modal.SetActive(false); closed = true; },null,null,null,null);
                ShowPicture(TutorialPicture(tutorial.textKey));
                while (!closed && !skipIntro && modal.activeSelf) yield return null;
                modal.SetActive(false); tutorialsSeen.Add(Session.Level.id);
            }
            ShowIntroBanner();
            float elapsed = 0;
            while (elapsed < Config.timing.introBannerSec && !skipIntro) { elapsed += Time.unscaledDeltaTime; yield return null; }
            introBanner.SetActive(false);
            Intro = false; Busy = false; hud.Busy(false); hud.party.Refresh();
            if (IsGuidedTurn()) { hud.ShowTip(TutorialText(tutorial)); ShowHint(); }
            // 레벨 8 안내형 스킬 튜토리얼: 가득 찬 세이지 초상화를 눌러 보게 합니다.
            if (IsGuidedSkill()) hud.ShowTip(TutorialText(tutorial));
        }
        private Sprite TutorialPicture(string key)
        {
            switch (key)
            {
                case "tut_ice": return art.ice2;
                case "tut_prism": return art.prism;
                case "tut_box": return art.box;
                case "tut_relic": return art.relic;
                case "tut_square": return art.meteor;
                case "tut_vine": return art.vine;
                case "tut_chain": return art.chain;
                default: return art.bomb;
            }
        }
        private void ShowIntroBanner()
        {
            LevelData level = Session.Level;
            introTitle.text = "레벨 "+level.id;
            introMoves.text = level.moves+"번 안에 모아요";
            for (int i = 0; i < introGoalImages.Length; i++)
            {
                bool visible = i < level.goals.Length;
                introGoalImages[i].transform.parent.gameObject.SetActive(visible);
                if (!visible) continue;
                introGoalImages[i].sprite = art.Goal(level.goals[i]);
                HudView.CenterSlot(introGoalImages[i].transform.parent,i,level.goals.Length,240);
                introGoalTexts[i].text = level.goals[i].count.ToString();
            }
            introBanner.SetActive(true);
        }
        public void SkipIntro() { skipIntro = true; }
        private bool IsGuidedTurn()
        {
            TutorialData tutorial = Session.Level.tutorial;
            return Session.Turns == 0 && tutorial != null && tutorial.type == "Guided";
        }
        private bool IsGuidedSkill()
        {
            TutorialData tutorial = Session.Level.tutorial;
            return tutorial != null && tutorial.type == "GuidedSkill" && !tutorialsSeen.Contains(Session.Level.id) && Session.AnySkillReady() && !Session.SkillLocked;
        }
        private static string TutorialText(TutorialData tutorial)
        {
            string text;
            return tutorial.textKey != null && tutorialTexts.TryGetValue(tutorial.textKey,out text) ? text : "";
        }
        private void Update()
        {
            if (Config == null || Session == null) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                // 뒤로 가기(13.3): 팝업이 있으면 닫고, 스킬 미리보기면 취소, 인게임 대기·마지막 기회면 일시정지를 엽니다.
                if (picker.activeSelf) picker.SetActive(false);
                else if (modal.activeSelf) modal.SetActive(false);
                else if (PreviewSlot >= 0) CancelSkill();
                else if (gameScreen.activeSelf) Pause();
            }
            if (!CanInput || selection.HasValue) return;
            hintTime += Time.unscaledDeltaTime;
            if (!hintVisible && hintTime >= Config.timing.hintDelaySec) ShowHint();
        }
        public void Touch()
        {
            hintTime = 0; hintVisible = false; board.ClearMarks();
            if (selection.HasValue) board.Mark(selection.Value,SelectColor);
        }
        private static readonly Color SelectColor = new Color(1,0.86f,0.45f,1);
        private static readonly Color HintColor = new Color(1,1,1,0.85f);
        public void Tap(CellPosition p)
        {
            if (!CanInput) return; Touch();
            if (!Session.Board.Contains(p) || !Session.Board[p].CanSwap)
            { selection = null; board.ClearMarks(); return; }
            // 특수 블록 클릭은 선택 여부와 관계없이 즉시 단독 발동합니다. 조합은 드래그로 교환합니다.
            if (MoveFinder.CanActivate(Session.Board,p))
            {
                selection = null; board.ClearMarks(); BeginTurn();
                StartCoroutine(ActivateTurn(p)); return;
            }
            if (selection.HasValue)
            {
                CellPosition a = selection.Value;
                if (a.Equals(p)) { selection = null; board.ClearMarks(); return; }
                if (Math.Abs(a.r-p.r)+Math.Abs(a.c-p.c) == 1) { selection = null; TrySwap(a,p); return; }
            }
            selection = p; board.ClearMarks(); board.Mark(p,SelectColor);
        }
        public void TrySwap(CellPosition a, CellPosition b)
        {
            if (!CanInput) return; Touch(); selection = null; board.ClearMarks();
            if (!Session.Board.Contains(a) || !Session.Board[a].CanSwap) return;
            if (!Session.Board.Contains(b) || !Session.Board[b].CanSwap)
            { StartCoroutine(board.Shake(a,Config.input.shakeSec)); return; }
            BeginTurn();
            StartCoroutine(SwapTurn(a,b));
        }
        private void BeginTurn() { Busy = true; hud.Busy(true); hud.party.Refresh(); }
        private IEnumerator SwapTurn(CellPosition a, CellPosition b)
        {
            LastEvents = Session.Swap(a,b);
            yield return ReplayTurn();
        }
        private IEnumerator ActivateTurn(CellPosition p)
        {
            LastEvents = Session.Activate(p);
            yield return ReplayTurn();
        }

        // ---- 파티 스킬(12.3~12.4)과 마지막 기회(10.3) ----
        public void TapPortrait(int slot)
        {
            if (Busy || modal.activeSelf || Session == null || Session.Outcome != SessionOutcome.Idle) return;
            // 미리보기 중인 같은 초상화를 다시 누르면 발동합니다.
            if (PreviewSlot == slot) { UseSkillNow(); return; }
            SkillState state = Session.GetSkillState(slot);
            if (state == SkillState.Locked) { hud.Toast("다음 스왑 뒤에 쓸 수 있어요",Config.timing.toastSec); return; }
            if (state == SkillState.NoTarget) { hud.Toast("대상이 없어요",Config.timing.toastSec); return; }
            // 아직 차는 중이면 무엇을 모아야 하는지와 현재 양을 알려줍니다(13.2의 게이지 숫자 표시를 겸합니다).
            if (state == SkillState.Charging) { hud.Toast(Session.Party[slot].character.name+" · "+GaugeLine(slot),Config.timing.toastSec); return; }
            if (state != SkillState.Ready) return;
            selection = null; hintTime = 0; hintVisible = false; board.ClearMarks();
            PreviewSlot = slot;
            board.Preview(Session.SkillTargetsFor(slot));
            hud.ShowSkillPanel(Session.Party[slot],GaugeLine(slot)); hud.party.Highlight(slot);
        }
        // 예: "초록 블록을 모으면 게이지가 차요 (9/15)". 숫자는 블록 개수 단위(내림(단위/12) / 필요량)입니다.
        private string GaugeLine(int slot)
        {
            string color;
            colorNames.TryGetValue(Session.Party[slot].character.attr,out color);
            int unit = Config.rules.gaugeUnitsPerBlock;
            return color+" 블록을 모으면 게이지가 차요 ("+Session.Gauge[slot]/unit+"/"+Session.Required(slot)/unit+")";
        }
        private static readonly Dictionary<string,string> colorNames = new Dictionary<string,string>
        { { "R", "빨강" }, { "B", "파랑" }, { "G", "초록" }, { "Y", "노랑" }, { "P", "보라" } };
        public void CancelSkill()
        {
            if (PreviewSlot < 0) return;
            PreviewSlot = -1;
            board.Preview(null); hud.HideSkillPanel(); hud.party.Highlight(-1);
        }
        public void UseSkillNow()
        {
            if (PreviewSlot < 0 || Busy) return;
            int slot = PreviewSlot;
            CancelSkill();
            BeginTurn();
            StartCoroutine(SkillTurn(slot));
        }
        private IEnumerator SkillTurn(int slot)
        {
            LastEvents = Session.UseSkill(slot);
            if (Session.Level.tutorial != null && Session.Level.tutorial.type == "GuidedSkill") tutorialsSeen.Add(Session.Level.id);
            yield return ReplayTurn();
        }
        public void EndLastChance()
        {
            if (Busy || Session == null || !Session.LastChance) return;
            CancelSkill();
            LastEvents = Session.EndLastChance();
            hud.endButton.gameObject.SetActive(false); hud.HideTip();
            ShowResult(false);
        }
        private IEnumerator ReplayTurn()
        {
            EventPlayer player = new EventPlayer(board,hud,Config.timing);
            yield return player.Play(LastEvents);
            yield return ExplainNewItems();
            // 안내형 튜토리얼 말풍선은 첫 턴이 끝나면 닫습니다.
            if (!IsGuidedTurn() && !IsGuidedSkill()) hud.HideTip();
            hud.party.Refresh();
            hud.endButton.gameObject.SetActive(Session.LastChance);
            if (Session.LastChance) hud.ShowTip("스킬을 쓸 수 있어요");
            if (Session.Outcome == SessionOutcome.Clear)
            {
                clearBanner.SetActive(true);
                yield return new WaitForSecondsRealtime(Config.timing.clearBannerSec);
                clearBanner.SetActive(false);
                bool firstClear = bestStars[current] == 0;
                bestStars[current] = Math.Max(bestStars[current],Session.Stars);
                unlocked = Math.Max(unlocked,Math.Min(current+1,levels.Count-1));
                if (Session.Level.tutorial != null) tutorialsSeen.Add(Session.Level.id);
                // 첫 클리어 보상 캐릭터 지급(12.1). 합류 팝업은 결과 팝업의 버튼을 누른 뒤 한 번 띄웁니다(13.1).
                FirstClearData grant = Session.Level.onFirstClear;
                if (firstClear && grant != null && grant.grantCharacter != 0 && !owned.Contains(grant.grantCharacter))
                { owned.Add(grant.grantCharacter); pendingJoin = grant.grantCharacter; }
                ShowResult(true);
            }
            else if (Session.Outcome == SessionOutcome.Fail) ShowResult(false);
            Busy = false; hud.Busy(false); hintTime = 0;
        }
        // ---- 아이템 첫 생성 설명(2026-10-10 사용자 변경, 기획서 13.4) ----
        // 로켓·폭탄·프리즘·별똥별은 레벨과 상관없이 보드에 처음 생긴 턴이 끝날 때 한 번 설명합니다.
        // 매치·프리즘 조합·스킬 등 어떤 경로로 생겨도 같습니다. 진행 저장 전까지는 실행 중에만 기억합니다.
        private readonly HashSet<OccupantKind> itemsExplained = new HashSet<OccupantKind>();
        public bool ItemTutorialOpen { get; private set; }
        private static readonly Dictionary<OccupantKind,string> itemTexts = new Dictionary<OccupantKind,string>
        {
            { OccupantKind.Rocket, "4개를 일렬로 맞추면 로켓이 생겨요.\n누르거나 옮기면 한 줄을 없애요" },
            { OccupantKind.Bomb, "L자나 T자로 맞추면 폭탄이 생겨요.\n폭탄은 주변 3×3을 없애요" },
            { OccupantKind.Prism, "5개를 일자로 맞추면 프리즘이 생겨요.\n블록과 바꾸면 그 색을 모두 없애요" },
            { OccupantKind.Meteor, "2×2로 맞추면 별똥별이 생겨요.\n별똥별은 목표를 찾아가요" },
        };
        private IEnumerator ExplainNewItems()
        {
            List<OccupantKind> fresh = new List<OccupantKind>();
            foreach (GameEvent item in LastEvents)
                if (item.kind == GameEventKind.SpecialCreated && itemTexts.ContainsKey(item.occupant.kind) &&
                    !itemsExplained.Contains(item.occupant.kind) && !fresh.Contains(item.occupant.kind))
                    fresh.Add(item.occupant.kind);
            foreach (OccupantKind kind in fresh)
            {
                itemsExplained.Add(kind);
                bool closed = false;
                Popup("새로운 아이템!",itemTexts[kind],"확인",() => { modal.SetActive(false); closed = true; },null,null,null,null);
                ShowPicture(ItemPicture(kind));
                ItemTutorialOpen = true;
                // 뒤로 가기로 팝업을 닫아도 진행이 멈추지 않게, 팝업이 닫히면 끝냅니다.
                while (!closed && modal.activeSelf) yield return null;
                modal.SetActive(false); ItemTutorialOpen = false;
            }
        }
        private Sprite ItemPicture(OccupantKind kind)
        {
            switch (kind)
            {
                case OccupantKind.Rocket: return art.rocketH;
                case OccupantKind.Prism: return art.prism;
                case OccupantKind.Meteor: return art.meteor;
                default: return art.bomb;
            }
        }
        // 레벨 데이터의 아이템 설명 팝업(레벨 6 폭탄, 15 프리즘)은 첫 생성 설명으로 대신하므로 레벨 시작 때 띄우지 않습니다.
        // 레벨 22의 2×2 설명은 규칙 해금 안내라 그대로 두고, 별똥별은 설명한 것으로 봅니다.
        private static bool IsItemTutorial(TutorialData tutorial)
        { return tutorial.textKey == "tut_bomb" || tutorial.textKey == "tut_prism"; }

        private void ShowHint()
        {
            if (Session == null || Session.Outcome != SessionOutcome.Idle || Session.LastChance) return;
            bool guided = IsGuidedTurn();
            CellPosition? activation = MoveFinder.FindActivation(Session.Board);
            if (!guided && activation.HasValue)
            { board.ClearMarks(); board.Mark(activation.Value,HintColor); hintVisible = true; return; }
            List<ValidMove> moves = MoveFinder.Find(Session.Board,Session.Level.features.squareMatch);
            if (moves.Count == 0) return;
            ValidMove move = moves[0];
            TutorialData tutorial = Session.Level.tutorial;
            if (guided && tutorial.swap != null)
                move = new ValidMove(new CellPosition(tutorial.swap[0][0],tutorial.swap[0][1]),new CellPosition(tutorial.swap[1][0],tutorial.swap[1][1]));
            board.ClearMarks(); board.Mark(move.a,HintColor); board.Mark(move.b,HintColor); hintVisible = true;
        }

        // ---- 팝업 흐름(기획서 13.1~13.2) ----
        public void Pause()
        {
            if (Busy || Session == null || Session.Outcome != SessionOutcome.Idle || !gameScreen.activeSelf) return;
            CancelSkill();
            Popup("일시정지","",
                "계속하기",() => modal.SetActive(false),
                "다시 시작",RequestRestart,
                "맵으로",RequestMap);
        }
        // [다시 시작]·[맵으로]는 이동을 썼을 때만 "진행한 내용이 사라져요" 확인을 거칩니다.
        public void RequestRestart()
        {
            if (Busy) return;
            if (Session.Turns == 0) { StartLevel(current); return; }
            Popup("다시 시작할까요?","진행한 내용이 사라져요","다시 시작",() => StartLevel(current),"계속하기",() => modal.SetActive(false),null,null);
        }
        public void RequestMap()
        {
            if (Busy) return;
            if (Session.Turns == 0 || Session.Outcome != SessionOutcome.Idle) { ShowMap(); return; }
            Popup("맵으로 나갈까요?","진행한 내용이 사라져요","나가기",ShowMap,"계속하기",() => modal.SetActive(false),null,null);
        }
        public void ShowMap()
        {
            StopAllCoroutines(); Busy = false; Intro = false; PreviewSlot = -1;
            modal.SetActive(false); picker.SetActive(false); introBanner.SetActive(false); clearBanner.SetActive(false);
            gameScreen.SetActive(false); map.gameObject.SetActive(true);
            map.Refresh();
        }
        // 레벨 진입 팝업: 레벨 번호, 목표 아이콘·수량, 이동 횟수, 추천 속성, 파티 슬롯, [시작] [닫기].
        public void OpenEntry(int index)
        {
            if (!Playable(index) || index > unlocked) return;
            LevelData level = levels[index];
            entryParty = DefaultParty(level); entryIndex = index;
            Popup("레벨 "+level.id,"이동 "+level.moves,
                "시작",() => StartLevel(index,null,(int[])entryParty.Clone()),"닫기",() => modal.SetActive(false),null,null);
            ShowGoals(level,null);
            // 슬롯 0칸인 레벨은 파티·추천 속성 영역을 숨깁니다.
            popupPartyRow.SetActive(level.partySlots > 0);
            RefreshEntryParty(level);
            LayoutPopup();
        }
        private void RefreshEntryParty(LevelData level)
        {
            for (int i = 0; i < popupRecommended.Length; i++)
            {
                bool visible = level.recommended != null && i < level.recommended.Length;
                popupRecommended[i].gameObject.SetActive(visible);
                if (visible) popupRecommended[i].sprite = art.Gem(BoardGenerator.ParseColor(level.recommended[i][0]));
            }
            for (int i = 0; i < popupPartySlots.Length; i++)
            {
                PartySlotView view = popupPartySlots[i];
                bool open = i < level.partySlots;
                view.gameObject.SetActive(open);
                if (!open) continue;
                HudView.CenterSlot(view.transform,i,level.partySlots,200);
                int id = i < entryParty.Length ? entryParty[i] : 0;
                view.SetPortrait(id != 0 ? art.Portrait(id) : null);
            }
        }
        // 슬롯을 누르면 보유 캐릭터 목록에서 고르거나 비웁니다.
        private void OpenPicker(int slot)
        {
            pickerSlot = slot;
            List<int> sorted = new List<int>(owned); sorted.Sort();
            for (int i = 0; i < pickerButtons.Length; i++)
            {
                bool visible = i < sorted.Count;
                pickerButtons[i].gameObject.SetActive(visible);
                if (visible) pickerFaces[i].sprite = art.Portrait(sorted[i]);
            }
            picker.SetActive(true);
        }
        // 같은 캐릭터를 두 슬롯에 넣을 수 없으므로, 이미 다른 슬롯에 있으면 두 슬롯을 맞바꿉니다.
        private void Pick(int index)
        {
            if (pickerSlot < 0 || pickerSlot >= entryParty.Length) { picker.SetActive(false); return; }
            List<int> sorted = new List<int>(owned); sorted.Sort();
            int id = index >= 0 && index < sorted.Count ? sorted[index] : 0;
            int other = id == 0 ? -1 : Array.IndexOf(entryParty,id);
            if (other >= 0) entryParty[other] = entryParty[pickerSlot];
            entryParty[pickerSlot] = id;
            picker.SetActive(false);
            RefreshEntryParty(levels[entryIndex]);
        }
        private void ShowResult(bool clear)
        {
            if (clear)
            {
                int next = current+1;
                bool hasNext = next < levels.Count && Playable(next);
                string body = next >= levels.Count ? "모든 레벨을 클리어했어요" : "남은 이동 "+Session.MovesLeft;
                if (hasNext)
                    Popup("클리어!",body,"다음 레벨",AfterJoin(() => OpenEntry(next)),"다시 하기",AfterJoin(() => OpenEntry(current)),"맵으로",AfterJoin(ShowMap));
                else
                    Popup("클리어!",body,"맵으로",AfterJoin(ShowMap),"다시 하기",AfterJoin(() => OpenEntry(current)),null,null);
                popupStarRow.SetActive(true); LayoutPopup();
                StartCoroutine(RevealStars(Session.Stars));
            }
            else
            {
                Popup("아쉬워요!","목표를 다 모으지 못했어요","다시 하기",() => OpenEntry(current),"맵으로",ShowMap,null,null);
                int[] remaining = new int[Session.Goals.Count];
                for (int i = 0; i < remaining.Length; i++) remaining[i] = Session.Goals.Remaining(i);
                ShowGoals(Session.Level,remaining);
            }
        }
        // 새 캐릭터가 생겼으면 다음 화면으로 가기 전에 합류 팝업을 한 번 띄웁니다(13.1의 6).
        private Action AfterJoin(Action next)
        {
            return () =>
            {
                if (pendingJoin == 0) { next(); return; }
                CharacterData c = Character(pendingJoin); pendingJoin = 0;
                SkillData skill = null;
                foreach (SkillData s in catalog.skills) if (s.id == c.skillId) skill = s;
                string attr; attrNames.TryGetValue(c.attr,out attr);
                string body = attr+" 속성 · "+(skill != null ? skill.name : "");
                int nextSlots = current+1 < levels.Count ? levels[current+1].partySlots : 0;
                if (nextSlots > Session.Party.Length) body += "\n파티 슬롯이 "+nextSlots+"칸으로 늘었어요";
                Popup(c.name+" 합류!",body,"확인",next,null,null,null,null);
                ShowPicture(art.Portrait(c.id));
            };
        }
        private void ShowPicture(Sprite sprite)
        {
            popupPictureImage.sprite = sprite;
            popupPicture.SetActive(true); LayoutPopup();
        }
        // 별은 하나씩 0.3초(starIntervalSec) 간격으로 켭니다.
        private IEnumerator RevealStars(int stars)
        {
            for (int i = 0; i < popupStars.Length; i++) popupStars[i].color = new Color(0.25f,0.22f,0.38f,1);
            for (int i = 0; i < stars; i++)
            {
                yield return new WaitForSecondsRealtime(Config.timing.starIntervalSec);
                popupStars[i].color = Color.white;
                float elapsed = 0;
                while (elapsed < 0.15f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    popupStars[i].transform.localScale = Vector3.one*Mathf.Lerp(1.5f,1,elapsed/0.15f);
                    yield return null;
                }
                popupStars[i].transform.localScale = Vector3.one;
            }
        }
        private void ShowGoals(LevelData level, int[] remaining)
        {
            popupGoalRow.SetActive(true); LayoutPopup();
            for (int i = 0; i < popupGoalImages.Length; i++)
            {
                bool visible = i < level.goals.Length;
                popupGoalImages[i].transform.parent.gameObject.SetActive(visible);
                if (!visible) continue;
                popupGoalImages[i].sprite = art.Goal(level.goals[i]);
                HudView.CenterSlot(popupGoalImages[i].transform.parent,i,level.goals.Length,220);
                popupGoalTexts[i].text = (remaining == null ? level.goals[i].count : remaining[i]).ToString();
            }
        }
        private void Popup(string title, string body, string first, Action firstAction, string second, Action secondAction, string third, Action thirdAction)
        {
            selection = null; board.ClearMarks(); hintTime = 0;
            popupTitle.text = title; popupBody.text = body;
            popupBody.gameObject.SetActive(!string.IsNullOrEmpty(body));
            popupStarRow.SetActive(false); popupGoalRow.SetActive(false); popupPicture.SetActive(false);
            popupPartyRow.SetActive(false); picker.SetActive(false);
            ConfigureButton(primary,primaryText,first,firstAction);
            ConfigureButton(secondary,secondaryText,second,secondAction);
            ConfigureButton(tertiary,tertiaryText,third,thirdAction);
            modal.SetActive(true); LayoutPopup();
        }
        // 팝업 카드를 위에서부터 [제목 띠] → [별/목표/그림 줄] → [본문] → [파티] → [버튼들] 순으로 쌓고,
        // 보이는 것만큼 카드 높이를 맞춥니다. 줄을 켠 뒤에도 다시 부릅니다.
        public void LayoutPopup()
        {
            bool row = popupStarRow.activeSelf || popupGoalRow.activeSelf || popupPicture.activeSelf;
            bool body = popupBody.gameObject.activeSelf;
            bool party = popupPartyRow.activeSelf;
            UnityEngine.UI.Button[] buttons = { primary, secondary, tertiary };
            int count = 0;
            foreach (UnityEngine.UI.Button button in buttons) if (button.gameObject.activeSelf) count++;
            float height = 120+(row ? 250 : 0)+(body ? 120 : 0)+(party ? 300 : 0)+20+count*130+50;
            popupCard.sizeDelta = new Vector2(popupCard.sizeDelta.x,height);
            float y = height*0.5f;
            ((RectTransform)popupTitle.transform.parent).anchoredPosition = new Vector2(0,y);
            y -= 120;
            if (row)
            {
                foreach (GameObject item in new[] { popupStarRow, popupGoalRow, popupPicture })
                    ((RectTransform)item.transform).anchoredPosition = new Vector2(0,y-125);
                y -= 250;
            }
            if (body) { ((RectTransform)popupBody.transform).anchoredPosition = new Vector2(0,y-55); y -= 120; }
            if (party) { ((RectTransform)popupPartyRow.transform).anchoredPosition = new Vector2(0,y-150); y -= 300; }
            y -= 20;
            foreach (UnityEngine.UI.Button button in buttons)
            {
                if (!button.gameObject.activeSelf) continue;
                ((RectTransform)button.transform).anchoredPosition = new Vector2(0,y-60); y -= 130;
            }
        }
        private static void ConfigureButton(UnityEngine.UI.Button button, TMP_Text text, string caption, Action action)
        {
            button.gameObject.SetActive(caption != null); button.onClick.RemoveAllListeners();
            if (caption == null) return; text.text = caption; button.onClick.AddListener(() => action());
        }
    }
}
