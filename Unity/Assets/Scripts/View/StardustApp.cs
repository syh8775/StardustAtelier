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
        public CharacterView characters;
        public AudioService audioService;
        public VfxService vfx;
        // 타이틀 화면(13.1): 화면을 누르면 레벨 맵으로 갑니다. 저장을 읽지 못했으면 여기서 알립니다(14.3).
        public GameObject titleScreen;
        public UnityEngine.UI.Button titleButton;
        public TMP_Text titleNotice;
        public CellArt art;
        // 화면: 레벨 맵과 인게임. 나머지는 팝업입니다(기획서 13.1).
        public GameObject gameScreen;
        // 팝업 하나를 여러 용도(진입·일시정지·확인·결과·튜토리얼·합류)로 다시 씁니다.
        public GameObject modal, popupStarRow, popupGoalRow, popupPicture, popupPartyRow, popupGoldRow;
        // 결과 팝업의 획득 골드(10.4).
        public TMP_Text popupGoldText;
        // 소리 음량 줄: 설정 팝업과 일시정지 팝업이 함께 씁니다(13.2). 0~100, 100이 기본 음량입니다. 바꾸면 저장합니다.
        public GameObject popupSoundRow;
        public UnityEngine.UI.Slider bgmSlider, sfxSlider;
        public TMP_Text bgmValue, sfxValue;
        private bool soundDirty;
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
        // 안내형 튜토리얼의 손가락 표시(13.3).
        public TutorialGuide guide;
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
        // 진행은 save.json에 저장합니다(14.3). 아래 값은 저장 내용을 화면에서 쓰기 좋게 풀어 둔 것입니다.
        // unlocked는 열린 마지막 레벨의 번호 순서(0부터)입니다: 클리어한 레벨 + 다음 1개.
        // 테스트는 실제 저장을 건드리지 않도록 SaveDirectoryOverride로 저장 폴더를 바꿉니다.
        public static string SaveDirectoryOverride;
        public bool SaveRecovered { get; private set; }
        private SaveData save;
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
            { "tut_ice", "얼음에 갇힌 블록은 움직일 수 없어요.\n매치에 포함하면 얼음이 깨져요" },
            { "tut_box", "상자는 옆에서 매치해 깨요.\n상자가 있으면 위에서 블록이 내려오지 않아요" },
            { "tut_relic", "유물을 맨 아래까지 내리면 수집해요.\n블록과 자리를 바꿔 옮길 수도 있어요" },
            { "tut_square", "이제 2×2로도 맞출 수 있어요.\n별똥별이 목표를 찾아가요" },
            { "tut_vine", "덩굴은 옆에서 매치해 없애요.\n한 턴 동안 건드리지 않으면 번져요" },
            { "tut_chain", "사슬에 묶인 블록은 움직일 수 없어요.\n매치에 포함하면 풀려요" },
        };
        private static readonly Dictionary<string,string> attrNames = new Dictionary<string,string>
        { { "R", "불" }, { "B", "물" }, { "G", "숲" }, { "Y", "빛" }, { "P", "어둠" } };

        private void Start()
        {
            Application.runInBackground = true; Application.targetFrameRate = 60;
            Load(); LoadProgress();
            hud.pause.onClick.AddListener(Pause);
            hud.skillUse.onClick.AddListener(UseSkillNow);
            // 스킬 안내 중에는 [취소]를 받지 않습니다(지정 입력은 [발동]).
            hud.skillCancel.onClick.AddListener(() => { if (!GuideActive) CancelSkill(); });
            hud.endButton.onClick.AddListener(EndLastChance);
            hud.party.Bind(this); characters.Bind(this);
            bgmSlider.onValueChanged.AddListener(value => SetVolume(true,(int)value));
            sfxSlider.onValueChanged.AddListener(value => SetVolume(false,(int)value));
            introSkip.onClick.AddListener(SkipIntro);
            for (int i = 0; i < popupPartySlots.Length; i++) { int slot = i; popupPartySlots[i].button.onClick.AddListener(() => OpenPicker(slot)); }
            for (int i = 0; i < pickerButtons.Length; i++) { int index = i; pickerButtons[i].onClick.AddListener(() => Pick(index)); }
            pickerClear.onClick.AddListener(() => Pick(-1));
            pickerClose.onClick.AddListener(() => picker.SetActive(false));
            map.Bind(this);
            titleButton.onClick.AddListener(ShowMap);
            ApplyVolume();
            // 모든 버튼에 누름 소리를 붙입니다(13.5).
            foreach (UnityEngine.UI.Button button in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                button.onClick.AddListener(() => audioService.Play(audioService.button));
            map.Refresh();
            ShowTitle();
        }
        private void ShowTitle()
        {
            modal.SetActive(false); gameScreen.SetActive(false); map.gameObject.SetActive(false); characters.gameObject.SetActive(false);
            titleNotice.text = SaveRecovered ? "저장 파일을 읽지 못해 새로 시작해요" : "";
            titleScreen.SetActive(true);
            audioService.PlayBgm(audioService.bgmMap);
        }
        private static string SaveDirectory { get { return SaveDirectoryOverride ?? Application.persistentDataPath; } }
        // 저장을 읽어 화면에서 쓰는 값으로 풉니다.
        private void LoadProgress()
        {
            bool recovered;
            save = SaveService.Load(SaveDirectory,out recovered); SaveRecovered = recovered;
            SaveService.Normalize(save,new LevelCatalog { version = 1, levels = levels.ToArray() },catalog);
            unlocked = 0; owned.Clear(); tutorialsSeen.Clear(); itemsExplained.Clear();
            for (int i = 0; i < levels.Count; i++)
            {
                LevelSave level;
                bestStars[i] = save.levels.TryGetValue(levels[i].id.ToString(),out level) && level.cleared ? Math.Max(1,level.bestStars) : 0;
                if (bestStars[i] > 0) unlocked = Math.Max(unlocked,Math.Min(i+1,levels.Count-1));
            }
            foreach (string key in save.characters.Keys) { int id; if (int.TryParse(key,out id) && Character(id) != null) owned.Add(id); }
            lastParty = new int[save.lastParty.Length];
            for (int i = 0; i < lastParty.Length; i++) lastParty[i] = save.lastParty[i] ?? 0;
            foreach (int id in save.tutorialsSeen) tutorialsSeen.Add(id);
            foreach (string name in save.itemsSeen) { OccupantKind kind; if (Enum.TryParse(name,out kind)) itemsExplained.Add(kind); }
        }
        // 화면의 값을 저장 형식으로 옮겨 씁니다. 저장 시점: 결과 팝업, 캐릭터 지급, 튜토리얼 완료, 일시정지에서 나가기(14.3).
        private void WriteProgress()
        {
            if (save == null) return;
            for (int i = 0; i < levels.Count; i++)
            {
                if (bestStars[i] <= 0) continue;
                LevelSave level = SaveService.Level(save,levels[i].id);
                level.cleared = true; level.bestStars = bestStars[i];
            }
            foreach (int id in owned) if (!save.characters.ContainsKey(id.ToString())) save.characters[id.ToString()] = new CharacterSave();
            save.lastParty = new int?[lastParty.Length];
            for (int i = 0; i < lastParty.Length; i++) save.lastParty[i] = lastParty[i] == 0 ? (int?)null : lastParty[i];
            save.tutorialsSeen = new List<int>(tutorialsSeen); save.tutorialsSeen.Sort();
            save.itemsSeen = new List<string>();
            foreach (OccupantKind kind in itemsExplained) save.itemsSeen.Add(kind.ToString());
            save.itemsSeen.Sort();
            SaveService.Save(SaveDirectory,save);
        }
        // 한 판이 끝날 때 시도 횟수와 연속 실패(10.5)를 적습니다. 클리어하면 연속 실패는 0으로 돌아갑니다.
        // 실패, 다시 시작, 맵으로 나가기는 이동을 1 이상 쓴 경우에만 연속 실패를 1 올립니다.
        private void RecordAttempt(bool cleared)
        {
            if (save == null || Session == null) return;
            LevelSave level = SaveService.Level(save,Session.Level.id);
            level.attempts++;
            if (cleared) level.consecutiveFails = 0;
            else if (Session.Turns > 0) level.consecutiveFails++;
        }
        // 이동을 쓴 판을 버리고 나갈 때(다시 시작·맵으로) 기록하고 저장합니다.
        private void Abandon() { RecordAttempt(false); WriteProgress(); }
        public int CharacterLevel(int id)
        {
            CharacterSave character;
            return save != null && save.characters.TryGetValue(id.ToString(),out character) && character != null ? character.level : 1;
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
            // 저장을 읽기 전(Editor 미리보기 등)에는 빈 진행으로 둡니다. 실제 읽기는 Start의 LoadProgress가 합니다.
            if (save == null) save = new SaveData();
        }
        public IList<LevelData> Levels { get { Load(); return levels; } }
        public int Unlocked { get { return unlocked; } }
        public int BestStars(int index) { return bestStars[index]; }
        public bool Playable(int index) { return index >= 0 && index < levels.Count; }
        public IList<int> Owned { get { return owned; } }
        public int Gold { get { return save != null ? save.gold : 0; } }
        public CharacterCatalog Catalog { get { Load(); return catalog; } }
        public int BgmVolume { get { return save == null ? 100 : save.settings.bgmVolume; } }
        public int SfxVolume { get { return save == null ? 100 : save.settings.sfxVolume; } }
        public SkillData Skill(int id)
        {
            foreach (SkillData skill in catalog.skills) if (skill.id == id) return skill;
            return null;
        }
        // ---- 캐릭터 성장(12.6)과 설정(13.2) ----
        public bool CanLevelUp(int id) { return save != null && CharacterService.CanLevelUp(save,catalog.growth,id); }
        // 골드를 내고 레벨을 올린 뒤 즉시 저장합니다.
        public bool LevelUpCharacter(int id)
        {
            if (save == null || !CharacterService.TryLevelUp(save,catalog.growth,id)) return false;
            WriteProgress();
            return true;
        }
        public void ShowCharacters()
        {
            modal.SetActive(false); picker.SetActive(false);
            gameScreen.SetActive(false); map.gameObject.SetActive(false); titleScreen.SetActive(false);
            characters.gameObject.SetActive(true); characters.Open();
            audioService.PlayBgm(audioService.bgmMap);
        }
        // 설정 팝업: 배경음·효과음 켜기/끄기와 버전 표시.
        public void OpenSettings()
        {
            // 개발용 도구(DevTools)가 켜져 있으면 [진행 초기화] 버튼을 함께 보여줍니다.
            Popup("설정","버전 "+Application.version,"닫기",() => modal.SetActive(false),
                DevTools ? "진행 초기화 (테스트)" : null,ConfirmReset,null,null);
            popupSoundRow.SetActive(true); RefreshSound(); LayoutPopup();
        }

        // ---- 테스트용 도구(기획서의 디버그 메뉴 중 일부) ----
        // Editor와 개발 빌드에서만 켜집니다. 출시 빌드에는 버튼도 키도 나오지 않습니다.
        public static bool DevTools { get { return Application.isEditor || Debug.isDebugBuild; } }
        private void ConfirmReset()
        {
            Popup("진행을 초기화할까요?","레벨·캐릭터·골드·튜토리얼 기록이\n모두 처음 상태로 돌아가요","초기화",ResetProgress,"취소",() => modal.SetActive(false),null,null);
        }
        // 저장을 새 것으로 덮어쓰고 처음 실행한 상태로 맵을 엽니다.
        public void ResetProgress()
        {
            if (!DevTools) return;
            SaveService.Save(SaveDirectory,new SaveData());
            LoadProgress(); pendingJoin = 0; current = 0; soundDirty = false;
            ApplyVolume(); ShowMap();
        }
        // 레벨 바로 가기: 잠금과 관계없이 지금 레벨에서 step만큼 떨어진 레벨의 진입 팝업을 엽니다.
        // 잠금 해제는 이번 실행 동안만 유지하고 저장하지 않습니다.
        public void JumpLevel(int step)
        {
            if (!DevTools || titleScreen.activeSelf) return;
            int index = Mathf.Clamp(current+step,0,levels.Count-1);
            StopAllCoroutines(); Busy = false; Intro = false; PreviewSlot = -1;
            if (guide != null) guide.Hide();
            current = index; unlocked = Math.Max(unlocked,index);
            ShowMap(); OpenEntry(index);
        }
        // 슬라이더를 움직이면 소리에 바로 반영하고, 손을 뗀 뒤 한 번 저장합니다(끄는 동안 매 프레임 쓰지 않도록).
        public void SetVolume(bool bgm, int value)
        {
            if (save == null) return;
            value = Mathf.Clamp(value,0,100);
            if (bgm) { save.settings.bgmVolume = value; save.settings.bgm = value > 0; }
            else { save.settings.sfxVolume = value; save.settings.sfx = value > 0; }
            ApplyVolume(); RefreshSound(); soundDirty = true;
        }
        private void ApplyVolume() { audioService.Apply(BgmVolume/100f,SfxVolume/100f); }
        private void RefreshSound()
        {
            bgmSlider.SetValueWithoutNotify(BgmVolume); sfxSlider.SetValueWithoutNotify(SfxVolume);
            bgmValue.text = BgmVolume.ToString(); sfxValue.text = SfxVolume.ToString();
        }
        // 방금 끝난 판에서 얻은 골드. 결과 팝업에 보여줍니다.
        public int LastGold { get; private set; }

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
            // 실패 보정(10.5): 이 레벨을 연속 3회(assistFailThreshold) 이상 실패했고 고비 레벨이 아니면 그 판 내내 켭니다.
            bool assisted = !level.hard && save != null &&
                SaveService.Level(save,level.id).consecutiveFails >= Config.rules.assistFailThreshold;
            Session = new GameSession(level,Config,seed ?? unchecked((ulong)DateTime.UtcNow.Ticks),BuildParty(ids),assisted);
            lastParty = (int[])ids.Clone();
            selection = null; LastEvents = new List<GameEvent>(); PreviewSlot = -1;
            modal.SetActive(false); picker.SetActive(false); clearBanner.SetActive(false);
            map.gameObject.SetActive(false); characters.gameObject.SetActive(false); titleScreen.SetActive(false); gameScreen.SetActive(true);
            audioService.PlayBgm(audioService.bgmGame);
            hud.Configure(Session.Level,Config); hud.party.Configure(Session,art); hud.party.Highlight(-1);
            board.Configure(Session.Board,Config.layout.boardAreaPx);
            Busy = true; Intro = true; skipIntro = false; hintTime = 0; hintVisible = false; hud.Busy(true);
            if (guide != null) guide.Hide();
            StartCoroutine(BeginLevel());
        }
        private PartyMember[] BuildParty(int[] ids)
        {
            PartyMember[] party = new PartyMember[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] == 0) continue;
                // 캐릭터 레벨은 저장에서 읽습니다. 레벨업 화면(12.6)이 붙기 전까지는 모두 Lv 1입니다.
                PartyMember member = new PartyMember { level = CharacterLevel(ids[i]), costReductionPerLevel = catalog.growth.costReductionPerLevel };
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
        public CharacterData Character(int id)
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
                modal.SetActive(false); tutorialsSeen.Add(Session.Level.id); WriteProgress();
            }
            ShowIntroBanner();
            float elapsed = 0;
            while (elapsed < Config.timing.introBannerSec && !skipIntro) { elapsed += Time.unscaledDeltaTime; yield return null; }
            introBanner.SetActive(false);
            Intro = false; Busy = false; hud.Busy(false); hud.party.Refresh();
            // 안내형 튜토리얼(13.3): 말풍선과 손가락으로 지정 입력을 알려 주고, 그 입력만 받습니다.
            if (GuideActive) { hud.ShowTip(TutorialText(tutorial)); ShowGuide(); }
        }
        private Sprite TutorialPicture(string key)
        {
            // 설명 그림(600×400). 그림이 없으면 해당 아이템·장애물 그림으로 대신합니다.
            Sprite picture = art.Tutorial(key);
            if (picture != null) return picture;
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
            return Session.Turns == 0 && tutorial != null && tutorial.type == "Guided" && tutorial.swap != null &&
                !tutorialsSeen.Contains(Session.Level.id);
        }
        private bool IsGuidedSkill()
        {
            TutorialData tutorial = Session.Level.tutorial;
            return tutorial != null && tutorial.type == "GuidedSkill" && !tutorialsSeen.Contains(Session.Level.id) && Session.AnySkillReady() && !Session.SkillLocked;
        }
        // ---- 안내형 튜토리얼(기획서 13.3) ----
        // 처음 플레이할 때만, 지정한 칸·초상화만 밝게 두고 손가락으로 가리킵니다. 지정한 입력 외의 터치는 무시합니다.
        // 일시정지 버튼은 언제든 나갈 수 있도록 막지 않습니다.
        public bool GuideActive { get { return Session != null && Session.Outcome == SessionOutcome.Idle && (IsGuidedTurn() || IsGuidedSkill()); } }
        // 스킬 안내 중에는 보드를 눌러도 아무 일도 일어나지 않습니다(스왑·미리보기 취소 모두 막음).
        public bool GuideBlocksBoard { get { return Session != null && Session.Outcome == SessionOutcome.Idle && IsGuidedSkill(); } }
        private CellPosition GuideCell(int index)
        {
            int[] cell = Session.Level.tutorial.swap[index];
            return new CellPosition(cell[0],cell[1]);
        }
        private bool IsGuideSwap(CellPosition a, CellPosition b)
        {
            CellPosition first = GuideCell(0), second = GuideCell(1);
            return (a.Equals(first) && b.Equals(second)) || (a.Equals(second) && b.Equals(first));
        }
        // 스킬 안내에서 눌러야 하는 초상화 칸. 레벨 데이터의 charId 캐릭터가 파티에 없으면 -1입니다.
        private int GuideSlot()
        {
            for (int i = 0; i < Session.Party.Length; i++)
                if (Session.Party[i] != null && Session.Party[i].character.id == Session.Level.tutorial.charId &&
                    Session.GetSkillState(i) == SkillState.Ready) return i;
            return -1;
        }
        private void ShowGuide()
        {
            if (guide == null) return;
            if (!GuideActive || Busy) { HideGuide(); return; }
            hintVisible = true;
            if (IsGuidedTurn())
            {
                CellPosition a = GuideCell(0), b = GuideCell(1);
                board.Preview(new List<CellPosition> { a, b });
                guide.ShowDrag(board.At(a).transform.position,board.At(b).transform.position);
                return;
            }
            int slot = GuideSlot();
            if (slot < 0) { HideGuide(); return; }
            if (PreviewSlot == slot) guide.ShowTap(GuidePoint(hud.skillUse.transform,0.8f));
            else
            {
                // 초상화를 누르기 전에는 보드 전체를 어둡게 해 초상화로 눈이 가게 합니다.
                board.Preview(new List<CellPosition>());
                guide.ShowTap(GuidePoint(hud.party.slots[slot].transform,0.45f));
            }
        }
        // 손끝이 가리킬 곳: 글자나 얼굴을 가리지 않도록 대상의 가운데에서 아래쪽으로 내립니다(0 = 가운데, 1 = 아래 끝).
        private static Vector3 GuidePoint(Transform target, float down)
        {
            Vector3[] corners = new Vector3[4];
            ((RectTransform)target).GetWorldCorners(corners);
            Vector3 center = (corners[0]+corners[2])*0.5f, bottom = (corners[0]+corners[3])*0.5f;
            return Vector3.Lerp(center,bottom,down);
        }
        private void HideGuide()
        {
            if (guide == null || !guide.Visible) return;
            guide.Hide();
            if (PreviewSlot < 0) board.Preview(null);
        }
        private static string TutorialText(TutorialData tutorial)
        {
            string text;
            return tutorial.textKey != null && tutorialTexts.TryGetValue(tutorial.textKey,out text) ? text : "";
        }
        private void Update()
        {
            if (Config == null) return;
            // 음량을 바꾼 뒤 손을 떼면 저장합니다.
            if (soundDirty && (Pointer.current == null || !Pointer.current.press.isPressed)) { soundDirty = false; WriteProgress(); }
            // 테스트용 키: PageDown = 다음 레벨, PageUp = 이전 레벨, Home = 레벨 1, End = 마지막 레벨.
            if (DevTools && Keyboard.current != null)
            {
                if (Keyboard.current.pageDownKey.wasPressedThisFrame) JumpLevel(1);
                else if (Keyboard.current.pageUpKey.wasPressedThisFrame) JumpLevel(-1);
                else if (Keyboard.current.homeKey.wasPressedThisFrame) JumpLevel(-levels.Count);
                else if (Keyboard.current.endKey.wasPressedThisFrame) JumpLevel(levels.Count);
            }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Back();
            if (!CanInput || selection.HasValue) return;
            hintTime += Time.unscaledDeltaTime;
            if (!hintVisible && hintTime >= Config.timing.hintDelaySec) ShowHint();
        }
        // 뒤로 가기(13.3). 기기의 뒤로 가기 버튼과 키보드 Esc가 여기로 옵니다.
        // 팝업이 있으면 닫고, 캐릭터 화면이면 맵으로, 스킬 미리보기면 취소, 인게임 대기·마지막 기회면 일시정지,
        // 레벨 맵이면 종료 확인을 엽니다.
        public void Back()
        {
            if (picker.activeSelf) picker.SetActive(false);
            else if (modal.activeSelf) modal.SetActive(false);
            else if (characters.gameObject.activeSelf) ShowMap();
            else if (PreviewSlot >= 0) CancelSkill();
            else if (gameScreen.activeSelf) Pause();
            else if (map.gameObject.activeSelf) ConfirmQuit();
        }
        private void ConfirmQuit()
        {
            Popup("게임을 종료할까요?","","종료",Quit,"취소",() => modal.SetActive(false),null,null);
        }
        // 진행은 결과 팝업·튜토리얼 완료 때 이미 저장되어 있어, 여기서는 음량처럼 아직 안 쓴 변경만 마저 씁니다.
        private void Quit()
        {
            if (soundDirty) { soundDirty = false; WriteProgress(); }
            modal.SetActive(false);
#if UNITY_EDITOR
            // Editor에서는 앱 종료가 동작하지 않으므로 Play를 멈춥니다.
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        public void Touch()
        {
            // 안내 중에는 밝게 둔 칸 표시를 지우지 않습니다.
            if (GuideActive) return;
            hintTime = 0; hintVisible = false; board.ClearMarks();
            if (selection.HasValue) board.Mark(selection.Value,SelectColor);
        }
        private static readonly Color SelectColor = new Color(1,0.86f,0.45f,1);
        private static readonly Color HintColor = new Color(1,1,1,0.85f);
        public void Tap(CellPosition p)
        {
            if (!CanInput) return;
            if (GuideActive)
            {
                // 안내 중에는 지정한 두 칸만 받습니다. 한 칸을 누른 뒤 다른 칸을 누르면 교환합니다.
                if (!IsGuidedTurn() || (!p.Equals(GuideCell(0)) && !p.Equals(GuideCell(1)))) return;
                if (selection.HasValue && !selection.Value.Equals(p)) TrySwap(selection.Value,p); else selection = p;
                return;
            }
            Touch();
            // 특수 블록 클릭은 선택 여부와 관계없이 즉시 단독 발동합니다. 조합은 드래그로 교환합니다.
            // 얼음이나 사슬이 남아 있는 아이템은 발동 판정에서 거절합니다.
            if (MoveFinder.CanActivate(Session.Board,p))
            {
                selection = null; board.ClearMarks(); BeginTurn();
                StartCoroutine(ActivateTurn(p)); return;
            }
            if (!Session.Board.Contains(p) || !Session.Board[p].CanSwap)
            { selection = null; board.ClearMarks(); return; }
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
            if (!CanInput) return;
            if (GuideActive)
            {
                // 지정한 교환이 아니면 무시합니다. 지정 입력이 끝나면 안내를 닫고 본 것으로 저장합니다.
                if (!IsGuidedTurn() || !IsGuideSwap(a,b)) return;
                tutorialsSeen.Add(Session.Level.id); WriteProgress(); HideGuide();
            }
            Touch(); selection = null; board.ClearMarks();
            if (!Session.Board.Contains(a) || !Session.Board[a].CanSwap) return;
            if (!Session.Board.Contains(b) || !Session.Board[b].CanSwap)
            { StartCoroutine(board.Shake(a,Config.input.shakeSec)); return; }
            BeginTurn();
            StartCoroutine(SwapTurn(a,b));
        }
        private void BeginTurn() { HideGuide(); Busy = true; hud.Busy(true); hud.party.Refresh(); }
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
            // 안내 중에는 지정한 초상화만 받습니다(스왑 안내 중에는 초상화를 받지 않습니다).
            if (GuideActive && (!IsGuidedSkill() || slot != GuideSlot())) return;
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
            // 스킬 안내의 다음 단계: 손가락이 [발동] 버튼으로 옮겨 갑니다.
            if (GuideActive) ShowGuide();
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
            if (Session.Level.tutorial != null && Session.Level.tutorial.type == "GuidedSkill" && tutorialsSeen.Add(Session.Level.id)) WriteProgress();
            yield return ReplayTurn();
        }
        public void EndLastChance()
        {
            if (Busy || Session == null || !Session.LastChance) return;
            CancelSkill();
            LastEvents = Session.EndLastChance();
            hud.endButton.gameObject.SetActive(false); hud.HideTip();
            audioService.Play(audioService.fail);
            RecordAttempt(false); WriteProgress();
            ShowResult(false);
        }
        private IEnumerator ReplayTurn()
        {
            EventPlayer player = new EventPlayer(board,hud,Config.timing);
            // 아이템이 처음 생기면 연쇄 도중이라도 재생을 잠시 멈추고 설명 팝업을 띄웁니다.
            player.ItemCreated = ExplainItem;
            yield return player.Play(LastEvents);
            // 안내형 튜토리얼 말풍선은 첫 턴이 끝나면 닫습니다.
            if (!IsGuidedTurn() && !IsGuidedSkill()) hud.HideTip();
            hud.party.Refresh();
            hud.endButton.gameObject.SetActive(Session.LastChance);
            if (Session.LastChance) hud.ShowTip("스킬을 쓸 수 있어요");
            if (Session.Outcome == SessionOutcome.Clear)
            {
                clearBanner.SetActive(true);
                audioService.Play(audioService.clear);
                vfx.Play(vfx.clear,new Vector2(0,60),Color.white,128);
                yield return new WaitForSecondsRealtime(Config.timing.clearBannerSec);
                clearBanner.SetActive(false);
                bool firstClear = bestStars[current] == 0;
                // 골드 보상(10.4)은 최고 별을 갱신하기 전의 값으로 계산합니다.
                LastGold = ProgressService.ClearGold(Config.rewards,Session.Stars,bestStars[current]);
                save.gold += LastGold;
                bestStars[current] = Math.Max(bestStars[current],Session.Stars);
                unlocked = Math.Max(unlocked,Math.Min(current+1,levels.Count-1));
                if (Session.Level.tutorial != null) tutorialsSeen.Add(Session.Level.id);
                // 첫 클리어 보상 캐릭터 지급(12.1). 합류 팝업은 결과 팝업의 버튼을 누른 뒤 한 번 띄웁니다(13.1).
                FirstClearData grant = Session.Level.onFirstClear;
                if (firstClear && grant != null && grant.grantCharacter != 0 && !owned.Contains(grant.grantCharacter))
                { owned.Add(grant.grantCharacter); pendingJoin = grant.grantCharacter; }
                RecordAttempt(true); WriteProgress();
                ShowResult(true);
            }
            else if (Session.Outcome == SessionOutcome.Fail) { audioService.Play(audioService.fail); RecordAttempt(false); WriteProgress(); ShowResult(false); }
            Busy = false; hud.Busy(false); hintTime = 0;
        }
        // ---- 아이템 첫 생성 설명(기획서 13.4) ----
        // 로켓·폭탄·프리즘·별똥별은 레벨과 상관없이 보드에 처음 생긴 즉시(연쇄 도중에도) 한 번 설명합니다.
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
        // 이미 설명했거나 설명이 없는 종류면 null을 돌려줘 재생을 멈추지 않습니다.
        private IEnumerator ExplainItem(OccupantKind kind)
        {
            if (!itemTexts.ContainsKey(kind) || itemsExplained.Contains(kind)) return null;
            itemsExplained.Add(kind); WriteProgress();
            return ShowItemExplanation(kind);
        }
        private IEnumerator ShowItemExplanation(OccupantKind kind)
        {
            // 새로 생긴 아이템이 보드에 보인 뒤에 팝업을 띄웁니다.
            yield return new WaitForSecondsRealtime(0.25f);
            bool closed = false;
            Popup("새로운 아이템!",itemTexts[kind],"확인",() => { modal.SetActive(false); closed = true; },null,null,null,null);
            ShowPicture(ItemPicture(kind));
            ItemTutorialOpen = true;
            // 뒤로 가기로 팝업을 닫아도 진행이 멈추지 않게, 팝업이 닫히면 끝냅니다.
            while (!closed && modal.activeSelf) yield return null;
            modal.SetActive(false); ItemTutorialOpen = false;
        }
        private Sprite ItemPicture(OccupantKind kind)
        {
            switch (kind)
            {
                case OccupantKind.Rocket: return art.rocketH;
                case OccupantKind.Prism: return TutorialPicture("tut_prism");
                case OccupantKind.Meteor: return TutorialPicture("tut_square");
                default: return TutorialPicture("tut_bomb");
            }
        }
        // 레벨 데이터의 아이템 설명 팝업(레벨 6 폭탄, 15 프리즘)은 첫 생성 설명으로 대신하므로 레벨 시작 때 띄우지 않습니다.
        // 레벨 22의 2×2 설명은 규칙 해금 안내라 그대로 두고, 별똥별은 설명한 것으로 봅니다.
        private static bool IsItemTutorial(TutorialData tutorial)
        { return tutorial.textKey == "tut_bomb" || tutorial.textKey == "tut_prism"; }

        private void ShowHint()
        {
            if (Session == null || Session.Outcome != SessionOutcome.Idle || Session.LastChance) return;
            // 안내 중에는 힌트 대신 안내 표시를 다시 그립니다(일시정지 팝업을 닫은 뒤 등).
            if (GuideActive && guide != null) { ShowGuide(); return; }
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
            popupSoundRow.SetActive(true); RefreshSound(); LayoutPopup();
        }
        // [다시 시작]·[맵으로]는 이동을 썼을 때만 "진행한 내용이 사라져요" 확인을 거칩니다.
        public void RequestRestart()
        {
            if (Busy) return;
            if (Session.Turns == 0) { StartLevel(current); return; }
            Popup("다시 시작할까요?","진행한 내용이 사라져요","다시 시작",() => { Abandon(); StartLevel(current); },"계속하기",() => modal.SetActive(false),null,null);
        }
        public void RequestMap()
        {
            if (Busy) return;
            if (Session.Turns == 0 || Session.Outcome != SessionOutcome.Idle) { ShowMap(); return; }
            Popup("맵으로 나갈까요?","진행한 내용이 사라져요","나가기",() => { Abandon(); ShowMap(); },"계속하기",() => modal.SetActive(false),null,null);
        }
        public void ShowMap()
        {
            StopAllCoroutines(); Busy = false; Intro = false; PreviewSlot = -1;
            modal.SetActive(false); picker.SetActive(false); introBanner.SetActive(false); clearBanner.SetActive(false);
            gameScreen.SetActive(false); characters.gameObject.SetActive(false); titleScreen.SetActive(false); map.gameObject.SetActive(true);
            map.Refresh();
            audioService.PlayBgm(audioService.bgmMap);
        }
        // 레벨 진입 팝업: 레벨 번호, 목표 아이콘·수량, 이동 횟수, 추천 속성, 파티 슬롯, [시작] [닫기].
        public void OpenEntry(int index)
        {
            if (!Playable(index) || index > unlocked) return;
            LevelData level = levels[index];
            entryParty = DefaultParty(level); entryIndex = index;
            // 별 기준(10.4): 클리어하면 별 1개, 이동을 일정 수 이상 남기면 2개·3개입니다.
            string stars = "별 2개 · 이동 "+GameSession.StarMoves(level.moves,Config.rules.star2MovesRatio)+" 이상 남기기\n"+
                "별 3개 · 이동 "+GameSession.StarMoves(level.moves,Config.rules.star3MovesRatio)+" 이상 남기기";
            Popup("레벨 "+level.id,"이동 "+level.moves+"\n"+stars,
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
                popupStarRow.SetActive(true);
                popupGoldText.text = "+"+LastGold; popupGoldRow.SetActive(true);
                LayoutPopup();
                StartCoroutine(RevealStars(Session.Stars));
            }
            else
            {
                // 이동 횟수가 남았는데 끝난 판은 이유를 따로 알려 줍니다(9.3).
                string reason = Session.NoMoves ? "더 이동할 수 있는 블록이 없어요" : "목표를 다 모으지 못했어요";
                Popup("아쉬워요!",reason,"다시 하기",() => OpenEntry(current),"맵으로",ShowMap,null,null);
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
                // 본문은 두 줄: 속성, 그리고 스킬 이름과 효과 설명(예: 스킬 「물길」 · 맨 아래 줄을 한 번에 부숴요).
                string body = attr+" 속성";
                if (skill != null) body += "\n스킬 「"+skill.name+"」 · "+skill.desc;
                // 제목은 "세이지를 만났어요!"처럼 만난 캐릭터를 알립니다. 슬롯 수 안내는 하지 않습니다.
                Popup(c.name+ObjectParticle(c.name)+" 만났어요!",body,"확인",next,null,null,null,null);
                ShowPicture(art.Portrait(c.id));
            };
        }
        // 이름 뒤에 붙는 조사: 받침이 있으면 "을", 없으면 "를"(솔을, 세이지를).
        private static string ObjectParticle(string name)
        {
            if (string.IsNullOrEmpty(name)) return "를";
            char last = name[name.Length-1];
            bool hangul = last >= 0xAC00 && last <= 0xD7A3;
            return hangul && (last-0xAC00)%28 != 0 ? "을" : "를";
        }
        private void ShowPicture(Sprite sprite)
        {
            popupPictureImage.sprite = sprite;
            // 설명 그림은 가로로 넓고(3:2), 아이템·초상화는 정사각형입니다.
            bool wide = sprite != null && sprite.rect.width > sprite.rect.height*1.2f;
            popupPictureImage.rectTransform.sizeDelta = wide ? new Vector2(360,240) : new Vector2(230,230);
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
                vfx.Play(vfx.starPop,Vector2.zero,new Color(1f,0.85f,0.4f),150,0,null,null,0,0,popupStars[i].transform);
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
            selection = null; board.ClearMarks(); hintTime = 0; hintVisible = false;
            if (guide != null) guide.Hide();
            popupTitle.text = title; popupBody.text = body;
            popupBody.gameObject.SetActive(!string.IsNullOrEmpty(body));
            popupStarRow.SetActive(false); popupGoalRow.SetActive(false); popupPicture.SetActive(false);
            popupPartyRow.SetActive(false); popupGoldRow.SetActive(false); popupSoundRow.SetActive(false); picker.SetActive(false);
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
            bool party = popupPartyRow.activeSelf, goldRow = popupGoldRow.activeSelf, sound = popupSoundRow.activeSelf;
            UnityEngine.UI.Button[] buttons = { primary, secondary, tertiary };
            int count = 0;
            foreach (UnityEngine.UI.Button button in buttons) if (button.gameObject.activeSelf) count++;
            // 본문은 두 줄까지 120, 그보다 길면(긴 스킬 설명) 줄 수만큼 칸을 늘려 그림·버튼과 겹치지 않게 합니다.
            float bodyHeight = 120;
            if (body)
            {
                popupBody.ForceMeshUpdate();
                bodyHeight = Mathf.Max(120,popupBody.textInfo.lineCount*56+20);
                popupBody.rectTransform.sizeDelta = new Vector2(popupBody.rectTransform.sizeDelta.x,bodyHeight);
            }
            float height = 120+(row ? 250 : 0)+(body ? bodyHeight : 0)+(party ? 300 : 0)+(goldRow ? 90 : 0)+(sound ? 220 : 0)+20+count*130+50;
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
            if (body) { ((RectTransform)popupBody.transform).anchoredPosition = new Vector2(0,y-bodyHeight*0.5f+5); y -= bodyHeight; }
            if (sound) { ((RectTransform)popupSoundRow.transform).anchoredPosition = new Vector2(0,y-105); y -= 220; }
            if (goldRow) { ((RectTransform)popupGoldRow.transform).anchoredPosition = new Vector2(0,y-40); y -= 90; }
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
