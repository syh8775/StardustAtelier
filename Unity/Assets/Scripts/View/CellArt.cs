using UnityEngine;
using StardustAtelier.Core;

namespace StardustAtelier.View
{
    // [실제 코드에 남길 주석] 보드와 HUD가 함께 쓰는 그림 묶음입니다.
    // 지금은 Editor가 만든 임시 그림(Assets/Art/Placeholder)을 연결합니다.
    // 최종 그림은 같은 파일 이름으로 덮어쓰면 씬을 다시 만들지 않아도 바뀝니다.
    public sealed class CellArt : MonoBehaviour
    {
        // 색 순서는 BlockColor(R, B, G, Y, P)와 같습니다.
        public Sprite[] gems = new Sprite[5];
        public Sprite rocketH, rocketV, bomb, meteor, prism;
        public Sprite box, vine, relic, chain, ice1, ice2;
        public Sprite check;
        // 캐릭터 그림. 순서는 ID 105~109(세이지·루미·피오·솔·모르)입니다.
        public Sprite[] portraits = new Sprite[5];
        public Sprite[] fullBodies = new Sprite[5];
        // 설명형 튜토리얼 그림. tutorialKeys와 같은 순서입니다.
        public string[] tutorialKeys = new string[0];
        public Sprite[] tutorials = new Sprite[0];
        public Sprite Tutorial(string key)
        {
            int index = System.Array.IndexOf(tutorialKeys,key);
            return index >= 0 && index < tutorials.Length ? tutorials[index] : null;
        }
        public Sprite Portrait(int id) { int i = id-105; return i >= 0 && i < portraits.Length ? portraits[i] : null; }
        public Sprite FullBody(int id) { int i = id-105; return i >= 0 && i < fullBodies.Length ? fullBodies[i] : null; }

        public Sprite Gem(BlockColor color)
        {
            int index = (int)color;
            return index >= 0 && index < gems.Length ? gems[index] : null;
        }

        // 목표 칸 아이콘: 색 수집은 그 색 블록, 장애물은 장애물 그림을 씁니다.
        public Sprite Goal(GoalData goal)
        {
            switch (goal.type)
            {
                case "Collect": return Gem(BoardGenerator.ParseColor(goal.color[0]));
                case "Ice": return ice2;
                case "Box": return box;
                case "Vine": return vine;
                case "Chain": return chain;
                case "Relic": return relic;
                default: return null;
            }
        }
    }
}
