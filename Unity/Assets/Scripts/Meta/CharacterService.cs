using StardustAtelier.Core;

namespace StardustAtelier.Meta
{
    // [실제 코드에 남길 주석] 캐릭터 성장(기획서 12.6). 골드만 쓰고, 효과는 필요 게이지 감소뿐입니다.
    public static class CharacterService
    {
        // Lv L에서 L+1로 올리는 비용 = goldCostPerLevel(30) × L. 1→21 합계 6,300.
        public static int LevelUpCost(GrowthData growth, int level) { return growth.goldCostPerLevel*level; }

        // 레벨업 버튼이 켜지는 조건: 최대 레벨이 아니고 골드가 충분할 때.
        public static bool CanLevelUp(SaveData save, GrowthData growth, int id)
        {
            CharacterSave character;
            if (!save.characters.TryGetValue(id.ToString(),out character) || character == null) return false;
            return character.level < growth.maxLevel && save.gold >= LevelUpCost(growth,character.level);
        }
        // 골드를 내고 레벨을 1 올립니다. 조건이 안 맞으면 아무것도 바꾸지 않고 false를 돌려줍니다.
        public static bool TryLevelUp(SaveData save, GrowthData growth, int id)
        {
            if (!CanLevelUp(save,growth,id)) return false;
            CharacterSave character = save.characters[id.ToString()];
            save.gold -= LevelUpCost(growth,character.level);
            character.level++;
            return true;
        }
    }
}
