using TMPro;
using UnityEngine;
using StardustAtelier.Core;

namespace StardustAtelier.View
{
    // [실제 코드에 남길 주석] 인게임 하단 파티 초상화와 원형 게이지(기획서 13.2, 12.3).
    // 레벨 1~7(슬롯 0칸)은 통째로 숨깁니다. 잠긴 슬롯은 자물쇠와 "레벨 N"을 보여줍니다.
    public sealed class PartyBar : MonoBehaviour
    {
        public PartySlotView[] slots;
        // 슬롯이 처음 열리는 레벨(12.2): 1칸 레벨 8, 2칸 레벨 9, 3칸 레벨 16.
        private static readonly int[] unlockLevels = { 8, 9, 16 };
        private GameSession session;
        private CellArt art;

        public void Bind(StardustApp app)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                int slot = i;
                slots[i].button.onClick.AddListener(() => app.TapPortrait(slot));
            }
        }
        public void Configure(GameSession value, CellArt pictures)
        {
            session = value; art = pictures;
            gameObject.SetActive(session.Party.Length > 0);
            for (int i = 0; i < slots.Length; i++)
            {
                PartySlotView view = slots[i];
                bool open = i < session.Party.Length;
                PartyMember member = open ? session.Party[i] : null;
                view.lockGroup.SetActive(!open);
                view.lockText.text = "레벨 "+unlockLevels[i];
                view.SetPortrait(member != null ? art.Portrait(member.character.id) : null);
                if (!open && view.empty != null) view.empty.SetActive(false);
                view.gaugeBack.gameObject.SetActive(member != null);
                view.gauge.gameObject.SetActive(member != null);
                view.button.interactable = member != null;
            }
            Refresh();
        }
        public void SetGauge(int slot, int units)
        {
            if (session == null || slot >= session.Party.Length || session.Party[slot] == null) return;
            slots[slot].gauge.fillAmount = Mathf.Clamp01((float)units/session.Required(slot));
            Refresh();
        }
        // 쓸 수 있는 스킬만 빛납니다. 가득 찼지만 잠김·대상 없음이면 게이지만 가득 찬 모양입니다(12.3).
        public void Refresh()
        {
            if (session == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                bool member = i < session.Party.Length && session.Party[i] != null;
                if (member) slots[i].gauge.fillAmount = Mathf.Clamp01((float)session.Gauge[i]/session.Required(i));
                bool ready = member && session.Outcome == SessionOutcome.Idle && session.GetSkillState(i) == SkillState.Ready;
                slots[i].glow.SetActive(ready);
                // 게이지가 가득 찬 초상화 둘레의 반복 효과.
                if (slots[i].fullVfx != null && slots[i].fullVfx.activeSelf != ready) slots[i].fullVfx.SetActive(ready);
            }
        }
        // 슬롯 캐릭터의 속성 색. 스킬 효과 색에 씁니다.
        public Color SlotColor(int slot)
        {
            if (session == null || slot < 0 || slot >= session.Party.Length || session.Party[slot] == null) return Color.white;
            return CellView.ColorFor(BoardGenerator.ParseColor(session.Party[slot].character.attr[0]));
        }
        public void Highlight(int slot)
        {
            for (int i = 0; i < slots.Length; i++) slots[i].selected.SetActive(i == slot);
        }
        private void Update()
        {
            // 빛나는 초상화는 은은하게 커졌다 작아집니다.
            foreach (PartySlotView slot in slots)
                if (slot.glow.activeSelf) slot.glow.transform.localScale = Vector3.one*(1.0f+0.08f*Mathf.Sin(Time.unscaledTime*4f));
        }
    }
}
