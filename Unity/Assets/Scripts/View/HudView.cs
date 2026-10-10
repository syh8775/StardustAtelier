using System.Collections;
using TMPro;
using UnityEngine;
using StardustAtelier.Core;

namespace StardustAtelier.View
{
    // [실제 코드에 남길 주석] 기획서 13.2 인게임 배치: 상단 목표(최대 3)·남은 이동·일시정지, 중앙 보드, 하단 파티.
    // 파티 영역은 스킬이 연결될 때 붙입니다(레벨 1~7은 파티 슬롯 0칸이라 숨김).
    public sealed class HudView : MonoBehaviour
    {
        public TMP_Text title, moves;
        public TMP_Text[] goalTexts;
        public UnityEngine.UI.Image[] goalImages, goalChecks;
        public UnityEngine.UI.Button pause;
        // tip: 튜토리얼·셔플처럼 기획서가 정한 안내 문구를 보드 아래 말풍선으로 보여줍니다.
        public GameObject tipBubble;
        public TMP_Text tip;
        // combo: 연쇄 2단계부터 보드 위에 잠깐 띄우는 글자입니다.
        public TMP_Text combo;
        public CellArt art;
        // 하단 파티 바, 스킬 미리보기 패널(12.4), 마지막 기회의 [종료] 버튼(10.3).
        public PartyBar party;
        public GameObject skillPanel;
        public TMP_Text skillName, skillDesc;
        public UnityEngine.UI.Button skillUse, skillCancel, endButton;
        private Coroutine toastRoutine;
        private LevelData level;
        private int lowMoves;
        private Coroutine comboRoutine;

        public void Configure(LevelData value, GameConfig config)
        {
            level = value; lowMoves = config.layout.lowMovesWarning;
            title.text = "레벨 "+value.id;
            for (int i = 0; i < goalTexts.Length; i++)
            {
                bool visible = i < value.goals.Length;
                goalImages[i].transform.parent.gameObject.SetActive(visible);
                if (!visible) continue;
                goalImages[i].sprite = art.Goal(value.goals[i]);
                CenterSlot(goalImages[i].transform.parent,i,value.goals.Length,205);
                SetGoal(i,value.goals[i].count);
            }
            SetMoves(value.moves); HideTip(); HideSkillPanel(); endButton.gameObject.SetActive(false);
            if (combo != null) combo.gameObject.SetActive(false);
        }
        // 남은 이동이 config의 lowMovesWarning(5) 이하이면 숫자를 빨간색으로 표시합니다(13.2).
        public void SetMoves(int value)
        { moves.text = value.ToString(); moves.color = value <= lowMoves ? new Color(1,0.42f,0.42f) : Color.white; }
        // 0이 된 목표는 숫자 대신 체크 표시를 합니다(10.1).
        public void SetGoal(int index, int count)
        {
            goalTexts[index].text = count.ToString();
            goalTexts[index].gameObject.SetActive(count > 0);
            goalChecks[index].gameObject.SetActive(count == 0);
        }
        // 목표 칸을 개수에 맞춰 가운데 정렬합니다(1개면 가운데, 3개면 좌·중·우).
        public static void CenterSlot(Transform slot, int index, int count, float spacing)
        {
            RectTransform rect = (RectTransform)slot;
            rect.anchoredPosition = new Vector2((index-(count-1)*0.5f)*spacing,rect.anchoredPosition.y);
        }
        public void ShowTip(string text) { tip.text = text; tipBubble.SetActive(!string.IsNullOrEmpty(text)); }
        public void HideTip() { tip.text = ""; tipBubble.SetActive(false); }
        // 잠깐 띄우는 안내(예: "다음 스왑 뒤에 쓸 수 있어요" 1.5초, 12.3). 끝나면 원래 말풍선 문구로 돌아갑니다.
        public void Toast(string text, float seconds)
        {
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine(text,seconds));
        }
        private IEnumerator ToastRoutine(string text, float seconds)
        {
            string previous = tipBubble.activeSelf ? tip.text : null;
            ShowTip(text);
            yield return new WaitForSecondsRealtime(seconds);
            if (previous != null) ShowTip(previous); else HideTip();
            toastRoutine = null;
        }
        // 첫 줄: 스킬 효과, 둘째 줄: 게이지가 차는 방법.
        public void ShowSkillPanel(PartyMember member, string gaugeLine)
        {
            skillName.text = member.character.name+" · "+member.skill.name;
            skillDesc.text = member.skill.desc+"\n<size=80%><color=#CFC6F2>"+gaugeLine+"</color></size>";
            skillPanel.SetActive(true);
        }
        public void HideSkillPanel() { skillPanel.SetActive(false); }
        public void ShowCombo(int step)
        {
            if (combo == null) return;
            if (comboRoutine != null) StopCoroutine(comboRoutine);
            comboRoutine = StartCoroutine(Combo(step));
        }
        private IEnumerator Combo(int step)
        {
            combo.text = step+" 연쇄!"; combo.gameObject.SetActive(true);
            float elapsed = 0, seconds = 0.7f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed/seconds;
                combo.transform.localScale = Vector3.one*(t < 0.2f ? Mathf.Lerp(0.6f,1.15f,t/0.2f) : 1.15f-0.15f*t);
                combo.alpha = t < 0.7f ? 1 : 1-(t-0.7f)/0.3f;
                yield return null;
            }
            combo.gameObject.SetActive(false); comboRoutine = null;
        }
        // 해결 과정 중에는 일시정지 버튼을 흐리게 하고 무시합니다(7.4).
        public void Busy(bool busy) { pause.interactable = !busy; }
    }
}
