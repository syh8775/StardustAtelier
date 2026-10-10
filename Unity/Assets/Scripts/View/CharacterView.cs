using System.Collections.Generic;
using TMPro;
using UnityEngine;
using StardustAtelier.Core;
using StardustAtelier.Meta;

namespace StardustAtelier.View
{
    // [실제 코드에 남길 주석] 캐릭터 화면(기획서 13.2, 12.6).
    // 보유 캐릭터 카드 목록 → 고르면 이름·속성·레벨·스킬 설명·"필요 게이지 15 → 14"·[레벨업 (비용)].
    // 미보유 캐릭터는 보여주지 않습니다. 레벨업하면 바로 저장합니다.
    public sealed class CharacterView : MonoBehaviour
    {
        public UnityEngine.UI.Button back, levelUp;
        public PartySlotView[] cards;
        public GameObject detail, emptyNote;
        public UnityEngine.UI.Image fullBody, attrIcon;
        public TMP_Text goldText, nameText, attrText, levelText, gaugeText, skillName, skillDesc, levelUpText;
        private StardustApp app;
        private readonly List<int> shown = new List<int>();
        private int selected;

        private static readonly Dictionary<string,string> attrNames = new Dictionary<string,string>
        { { "R", "불" }, { "B", "물" }, { "G", "숲" }, { "Y", "빛" }, { "P", "어둠" } };

        public int SelectedId { get { return selected >= 0 && selected < shown.Count ? shown[selected] : 0; } }

        public void Bind(StardustApp owner)
        {
            app = owner;
            back.onClick.AddListener(app.ShowMap);
            levelUp.onClick.AddListener(LevelUp);
            for (int i = 0; i < cards.Length; i++) { int index = i; cards[i].button.onClick.AddListener(() => Select(index)); }
        }
        // 화면을 열 때마다 보유 목록을 ID 순으로 다시 만듭니다.
        public void Open()
        {
            shown.Clear(); shown.AddRange(app.Owned); shown.Sort();
            if (selected >= shown.Count) selected = 0;
            Refresh();
        }
        public void Select(int index)
        {
            if (index < 0 || index >= shown.Count) return;
            selected = index; Refresh();
        }
        private void LevelUp()
        {
            if (SelectedId != 0 && app.LevelUpCharacter(SelectedId)) Refresh();
        }
        public void Refresh()
        {
            goldText.text = app.Gold.ToString("N0");
            for (int i = 0; i < cards.Length; i++)
            {
                bool visible = i < shown.Count;
                cards[i].gameObject.SetActive(visible);
                if (!visible) continue;
                HudView.CenterSlot(cards[i].transform,i,shown.Count,190);
                cards[i].SetPortrait(app.art.Portrait(shown[i]));
                cards[i].selected.SetActive(i == selected);
            }
            bool any = shown.Count > 0;
            detail.SetActive(any); emptyNote.SetActive(!any);
            if (!any) return;

            int id = SelectedId;
            CharacterData character = app.Character(id);
            SkillData skill = app.Skill(character.skillId);
            GrowthData growth = app.Catalog.growth;
            int level = app.CharacterLevel(id);
            fullBody.sprite = app.art.FullBody(id);
            nameText.text = character.name;
            attrIcon.sprite = app.art.Gem(BoardGenerator.ParseColor(character.attr[0]));
            string attr; attrNames.TryGetValue(character.attr,out attr);
            attrText.text = attr+" 속성";
            levelText.text = "Lv "+level+(level >= growth.maxLevel ? " (최대)" : "");
            skillName.text = skill.name; skillDesc.text = skill.desc;

            // 레벨업 효과는 필요 게이지 감소뿐입니다(12.3 식). 다음 레벨 값을 함께 보여줍니다.
            int unit = app.Config.rules.gaugeUnitsPerBlock;
            PartyMember now = new PartyMember { character = character, level = level, costReductionPerLevel = growth.costReductionPerLevel };
            int current = now.RequiredUnits(unit)/unit;
            if (level >= growth.maxLevel)
            {
                gaugeText.text = "필요 게이지 "+current;
                levelUpText.text = "최대 레벨";
            }
            else
            {
                PartyMember next = new PartyMember { character = character, level = level+1, costReductionPerLevel = growth.costReductionPerLevel };
                gaugeText.text = "필요 게이지 "+current+" → "+next.RequiredUnits(unit)/unit;
                levelUpText.text = "레벨업   "+CharacterService.LevelUpCost(growth,level).ToString("N0")+" 골드";
            }
            // 골드 부족 또는 최대 레벨이면 버튼을 끕니다.
            levelUp.interactable = app.CanLevelUp(id);
        }
    }
}
