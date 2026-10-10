using TMPro;
using UnityEngine;

namespace StardustAtelier.View
{
    // 파티 슬롯 하나: 초상화, 원형 게이지(Filled 이미지), 빛남, 선택 테두리, 잠금 표시.
    public sealed class PartySlotView : MonoBehaviour
    {
        public UnityEngine.UI.Button button;
        public UnityEngine.UI.Image portrait, gauge, gaugeBack;
        public GameObject glow, selected, lockGroup, empty;
        // 초상화를 보이거나 숨기고, 빈 슬롯이면 "+"를 보입니다.
        public void SetPortrait(Sprite sprite)
        {
            portrait.gameObject.SetActive(sprite != null);
            if (sprite != null) portrait.sprite = sprite;
            if (empty != null) empty.SetActive(sprite == null);
        }
        public TMP_Text lockText;
    }
}
