using TMPro;
using UnityEngine;
using StardustAtelier.Core;

namespace StardustAtelier.View
{
    // [실제 코드에 남길 주석] 기획서 13.2 레벨 맵: 아래에서 위로 1~30 세로 스크롤.
    // 클리어한 레벨 + 다음 1개만 열리고, 레벨마다 최고 별, 고비 레벨은 붉은 테두리와 "고비" 표시.
    // 상단 골드와 하단 [캐릭터]·[설정]은 저장·성장 기능이 붙을 때 추가합니다.
    public sealed class MapView : MonoBehaviour
    {
        public UnityEngine.UI.ScrollRect scroll;
        public MapNode[] nodes;
        // 상단 보유 골드(13.2).
        public TMP_Text goldText;
        // 하단 [캐릭터] [설정] 버튼(13.2). 스크롤과 상관없이 화면 아래에 고정합니다.
        public UnityEngine.UI.Button charactersButton, settingsButton;
        private StardustApp app;

        public void Bind(StardustApp owner)
        {
            app = owner;
            charactersButton.onClick.AddListener(app.ShowCharacters);
            settingsButton.onClick.AddListener(app.OpenSettings);
            for (int i = 0; i < nodes.Length; i++)
            {
                int index = i;
                nodes[i].button.onClick.AddListener(() => app.OpenEntry(index));
            }
        }
        public void Refresh()
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                LevelData level = i < app.Levels.Count ? app.Levels[i] : null;
                bool open = level != null && i <= app.Unlocked && app.Playable(i);
                bool current = open && i == app.Unlocked;
                nodes[i].Show(level != null ? level.id : i+1,open,current,level != null && level.hard,app.BestStars(i));
            }
            if (goldText != null) goldText.text = app.Gold.ToString("N0");
            ScrollTo(app.Unlocked);
        }
        // 열면 다음에 할 레벨로 스크롤합니다. 콘텐츠는 아래가 1번이므로 위치 비율을 그대로 씁니다.
        public void ScrollTo(int index)
        {
            Canvas.ForceUpdateCanvases();
            float content = scroll.content.rect.height, view = ((RectTransform)scroll.transform).rect.height;
            if (content <= view) return;
            RectTransform node = (RectTransform)nodes[Mathf.Clamp(index,0,nodes.Length-1)].transform;
            // 노드의 y는 콘텐츠 아래 끝 기준입니다(anchor 하단).
            float y = node.anchoredPosition.y - view*0.4f;
            scroll.verticalNormalizedPosition = Mathf.Clamp01(y/(content-view));
        }
    }
}
