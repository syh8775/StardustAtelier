using UnityEngine;
using UnityEngine.EventSystems;
using StardustAtelier.Core;

namespace StardustAtelier.View
{
    public sealed class InputController : MonoBehaviour
    {
        public StardustApp app;
        public BoardView board;
        private CellPosition origin;
        private Vector2 start;
        private bool accepted, sent;
        // [실제 코드에 남길 주석] 해결 중 시작된 드래그를 대기 상태로 이월하지 않습니다.
        public void Begin(CellPosition p, PointerEventData data)
        {
            accepted = false; sent = false;
            if (app.Intro) { app.SkipIntro(); return; }
            // 스킬 안내 중에는 보드를 눌러도 미리보기를 취소하거나 교환하지 않습니다(13.3).
            if (app.GuideBlocksBoard) return;
            // 스킬 미리보기 중 보드의 다른 곳을 누르면 취소합니다(12.4).
            if (app.PreviewSlot >= 0) { app.CancelSkill(); return; }
            if (!app.CanInput) return;
            app.Touch(); origin = p;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)board.transform,data.position,data.pressEventCamera,out start);
            accepted = true;
        }
        public void Drag(PointerEventData data)
        {
            if (!accepted || sent || !app.CanInput) return;
            Vector2 current;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)board.transform,data.position,data.pressEventCamera,out current);
            Vector2 delta = current-start;
            if (Mathf.Max(Mathf.Abs(delta.x),Mathf.Abs(delta.y)) < board.CellSize*app.Config.input.dragThresholdCellRatio) return;
            CellPosition target = origin;
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)) target.c += delta.x > 0 ? 1 : -1;
            else target.r += delta.y > 0 ? -1 : 1;
            sent = true; app.TrySwap(origin,target);
        }
        public void End(PointerEventData data)
        {
            if (accepted && !sent && app.CanInput) app.Tap(origin);
            accepted = false;
        }
    }
}
