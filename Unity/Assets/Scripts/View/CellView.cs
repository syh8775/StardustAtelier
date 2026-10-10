using UnityEngine;
using UnityEngine.EventSystems;
using StardustAtelier.Core;

namespace StardustAtelier.View
{
    public sealed class CellView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform tile;
        // floor: 칸 바닥(체크무늬), ice: 바닥층 얼음, gem: 점유물 그림, overlay: 특수 블록 표시,
        // chain: 덮개 사슬, highlight: 선택·힌트 빛.
        public UnityEngine.UI.Image floor, ice, gem, overlay, chain, highlight;
        public InputController input;
        public CellArt art;
        public CellPosition Position { get; set; }

        // 체크무늬 두 색. 상용 매치3처럼 칸 경계를 은은하게 보이게 합니다.
        private static readonly Color floorA = new Color(0.20f,0.17f,0.36f,0.92f);
        private static readonly Color floorB = new Color(0.25f,0.21f,0.43f,0.92f);

        public void Render(Cell cell)
        {
            gameObject.SetActive(cell.exists);
            floor.gameObject.SetActive(cell.exists);
            if (!cell.exists) return;
            floor.color = (Position.r+Position.c)%2 == 0 ? floorA : floorB;

            // 얼음은 블록 아래 바닥층이라 블록이 떨어져도 칸에 남습니다.
            ice.gameObject.SetActive(cell.ice > 0);
            if (cell.ice > 0) ice.sprite = cell.ice == 2 ? art.ice2 : art.ice1;

            bool occupied = cell.occupant.kind != OccupantKind.Empty;
            tile.gameObject.SetActive(occupied);
            if (occupied)
            {
                gem.sprite = SpriteFor(cell.occupant);
                // 아이템은 보석 없이 아이템 그림만 그립니다(2026-10-10 사용자 변경). 겹침 표시는 쓰지 않습니다.
                overlay.gameObject.SetActive(false);
            }
            chain.gameObject.SetActive(cell.chained);
            tile.anchoredPosition = Vector2.zero;
            tile.localScale = Vector3.one;
        }

        private Sprite SpriteFor(Occupant occupant)
        {
            switch (occupant.kind)
            {
                case OccupantKind.Rocket: return occupant.orientation == Orientation.H ? art.rocketH : art.rocketV;
                case OccupantKind.Bomb: return art.bomb;
                case OccupantKind.Meteor: return art.meteor;
                case OccupantKind.Prism: return art.prism;
                case OccupantKind.Box: return art.box;
                case OccupantKind.Vine: return art.vine;
                case OccupantKind.Relic: return art.relic;
                default: return art.Gem(occupant.color);
            }
        }

        // 스킬 미리보기에서 대상이 아닌 칸은 어둡게 합니다(12.4).
        public void SetDim(bool dim)
        {
            Color shade = dim ? new Color(0.35f,0.35f,0.42f,1) : Color.white;
            gem.color = shade; ice.color = shade; chain.color = shade;
        }
        public void Mark(bool visible, Color color)
        { highlight.enabled = visible; highlight.color = color; }

        // 목표 수량 글자나 효과처럼 그림 없이 색만 필요한 곳에서 씁니다.
        public static Color ColorFor(BlockColor color)
        {
            switch (color)
            {
                case BlockColor.R: return new Color(0.93f,0.29f,0.36f);
                case BlockColor.B: return new Color(0.25f,0.56f,0.98f);
                case BlockColor.G: return new Color(0.27f,0.78f,0.45f);
                case BlockColor.Y: return new Color(1f,0.78f,0.22f);
                case BlockColor.P: return new Color(0.62f,0.40f,0.93f);
                default: return Color.white;
            }
        }
        public void OnPointerDown(PointerEventData data) { if (input != null) input.Begin(Position,data); }
        public void OnDrag(PointerEventData data) { if (input != null) input.Drag(data); }
        public void OnPointerUp(PointerEventData data) { if (input != null) input.End(data); }
    }
}
