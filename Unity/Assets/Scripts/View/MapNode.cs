using TMPro;
using UnityEngine;

namespace StardustAtelier.View
{
    // 레벨 맵의 버튼 하나. 숫자·별 3개·자물쇠·고비 표시를 가집니다.
    public sealed class MapNode : MonoBehaviour
    {
        public UnityEngine.UI.Button button;
        public UnityEngine.UI.Image face, ring, lockIcon;
        public UnityEngine.UI.Image[] stars;
        public TMP_Text number;
        public GameObject currentGlow;

        private static readonly Color openFace = new Color(0.98f,0.80f,0.36f);
        private static readonly Color clearedFace = new Color(0.56f,0.42f,0.95f);
        private static readonly Color lockedFace = new Color(0.27f,0.24f,0.40f);
        private static readonly Color starOff = new Color(0.20f,0.17f,0.32f,0.9f);

        public void Show(int id, bool open, bool current, bool hard, int best)
        {
            number.text = id.ToString();
            number.gameObject.SetActive(open);
            lockIcon.gameObject.SetActive(!open);
            button.interactable = open;
            face.color = !open ? lockedFace : best > 0 ? clearedFace : openFace;
            number.color = best > 0 || !open ? Color.white : new Color(0.24f,0.14f,0.05f);
            // 고비 레벨은 붉은 테두리로만 표시하고 "고비" 글자는 쓰지 않습니다.
            ring.color = hard ? new Color(0.95f,0.25f,0.30f) : open ? new Color(1f,0.93f,0.70f) : new Color(0.38f,0.34f,0.52f);
            currentGlow.SetActive(current);
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i].gameObject.SetActive(open && best > 0);
                stars[i].color = i < best ? Color.white : starOff;
            }
        }
        private void Update()
        {
            // 다음에 할 레벨은 살짝 숨 쉬듯 커졌다 작아집니다.
            if (currentGlow.activeSelf)
                currentGlow.transform.localScale = Vector3.one*(1.05f+0.07f*Mathf.Sin(Time.unscaledTime*3f));
        }
    }
}
