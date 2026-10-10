using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StardustAtelier.Core;

namespace StardustAtelier.View
{
    public sealed class BoardView : MonoBehaviour
    {
        public CellView[] cells;
        public InputController input;
        public CellArt art;
        // 이번 단계에서 사라지는 블록. 사라지는 연출이 끝날 때까지 그림을 남겨 둡니다.
        private readonly List<CellPosition> popping = new List<CellPosition>();
        public Board ViewBoard { get; private set; }
        public float CellSize { get; private set; }
        public void Configure(Board board, int area)
        {
            ViewBoard = board.Clone();
            CellSize = Mathf.Min((float)area/board.Width,(float)area/board.Height);
            ((RectTransform)transform).sizeDelta = new Vector2(area,area);
            for (int i = 0; i < cells.Length; i++)
            {
                bool active = i < board.Width*board.Height;
                cells[i].gameObject.SetActive(active);
                if (!active) continue;
                CellPosition p = new CellPosition(i/board.Width,i%board.Width);
                cells[i].Position = p; cells[i].input = input; cells[i].art = art;
                // 이전 판의 스킬 미리보기·튜토리얼 안내로 어두워진 칸이 남지 않게 되돌립니다.
                cells[i].SetDim(false);
                RectTransform rect = (RectTransform)cells[i].transform;
                rect.anchoredPosition = Local(p); rect.sizeDelta = Vector2.one*(CellSize-4);
                // 바닥은 별도 층(FloorLayer)에 있어 떨어지는 블록을 가리지 않습니다. 위치만 같은 칸에 맞춥니다.
                RectTransform floorRect = cells[i].floor.rectTransform;
                floorRect.anchoredPosition = Local(p); floorRect.sizeDelta = Vector2.one*(CellSize-4);
                cells[i].floor.gameObject.SetActive(true);
            }
            for (int i = board.Width*board.Height; i < cells.Length; i++) cells[i].floor.gameObject.SetActive(false);
            popping.Clear(); RenderAll(); ClearMarks();
        }
        public Vector2 Local(CellPosition p)
        { return new Vector2((p.c-(ViewBoard.Width-1)*0.5f)*CellSize,((ViewBoard.Height-1)*0.5f-p.r)*CellSize); }
        public CellView At(CellPosition p) { return cells[p.r*ViewBoard.Width+p.c]; }
        public void RenderAll()
        { foreach (CellPosition p in ViewBoard.Positions()) At(p).Render(ViewBoard[p]); }
        // targets가 null이면 미리보기를 끕니다. 대상 칸은 밝게 빛나고 나머지는 어두워집니다.
        public void Preview(List<CellPosition> targets)
        {
            foreach (CellPosition p in ViewBoard.Positions())
            {
                bool target = targets != null && targets.Contains(p);
                At(p).SetDim(targets != null && !target);
                if (target) Mark(p,new Color(1f,0.95f,0.6f,1));
            }
            if (targets == null) ClearMarks();
        }
        public void ClearMarks()
        { foreach (CellView cell in cells) cell.Mark(false,Color.white); }
        public void Mark(CellPosition p, Color color)
        { if (ViewBoard.Contains(p) && ViewBoard[p].exists) At(p).Mark(true,color); }
        public void ApplyCell(CellPosition p, Cell source)
        {
            bool wasOccupied = ViewBoard[p].occupant.kind != OccupantKind.Empty;
            ViewBoard[p].exists = source.exists; ViewBoard[p].ice = source.ice;
            ViewBoard[p].chained = source.chained; ViewBoard[p].occupant = source.occupant;
            CellView view = At(p);
            if (wasOccupied && source.occupant.kind == OccupantKind.Empty)
            {
                // 칸의 상태(얼음·사슬)는 바로 반영하고, 블록 그림만 잠시 남겨 터지는 연출에 씁니다.
                Sprite last = view.gem.sprite; bool hadOverlay = view.overlay.gameObject.activeSelf;
                view.Render(ViewBoard[p]);
                view.tile.gameObject.SetActive(true); view.gem.sprite = last; view.overlay.gameObject.SetActive(hadOverlay);
                if (!popping.Contains(p)) popping.Add(p);
            }
            else view.Render(ViewBoard[p]);
        }
        // 사라지는 블록을 살짝 부풀린 뒤 줄여서 없앱니다. 시간은 config의 clearSec입니다.
        public IEnumerator ClearPop(float seconds)
        {
            if (popping.Count == 0) yield break;
            List<CellPosition> list = new List<CellPosition>(popping); popping.Clear();
            float elapsed = 0;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(seconds <= 0 ? 1 : elapsed/seconds);
                float scale = t < 0.3f ? 1+0.25f*(t/0.3f) : 1.25f*(1-(t-0.3f)/0.7f);
                foreach (CellPosition p in list) At(p).tile.localScale = Vector3.one*Mathf.Max(0,scale);
                yield return null;
            }
            // 연출 사이에 다른 이벤트가 칸을 채웠을 수 있으므로 현재 보기 보드로 다시 그립니다.
            foreach (CellPosition p in list) At(p).Render(ViewBoard[p]);
        }
        public IEnumerator AnimateSwap(CellPosition a, CellPosition b, float seconds)
        {
            // 교환 중인 블록이 이웃 칸 아래로 가려지지 않게 맨 위로 올립니다.
            At(a).transform.SetAsLastSibling(); At(b).transform.SetAsLastSibling();
            RectTransform first = At(a).tile, second = At(b).tile;
            Vector2 distance = Local(b)-Local(a);
            float elapsed = 0;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0,1,seconds <= 0 ? 1 : elapsed/seconds);
                first.anchoredPosition = distance*t; second.anchoredPosition = -distance*t;
                yield return null;
            }
            ViewBoard.Swap(a,b); RenderAll();
        }
        public IEnumerator AnimateGravity(List<GameEvent> batch, TimingConfig timing)
        {
            bool spawning = batch[0].kind == GameEventKind.Spawned;
            List<RectTransform> tiles = new List<RectTransform>();
            List<Vector2> start = new List<Vector2>(), finish = new List<Vector2>();
            float duration = timing.fallMinSec;
            foreach (GameEvent item in batch)
            {
                int distance = Math.Abs(item.position.r-item.other.r);
                duration = Mathf.Max(duration,distance*timing.fallPerCellSec);
                if (spawning)
                {
                    ViewBoard[item.position].occupant = item.occupant;
                    At(item.position).Render(ViewBoard[item.position]);
                }
                RectTransform tile = spawning ? At(item.position).tile : At(item.other).tile;
                Vector2 delta = Local(item.position)-Local(item.other);
                tiles.Add(tile); start.Add(spawning ? -delta : Vector2.zero); finish.Add(spawning ? Vector2.zero : delta);
                tile.anchoredPosition = start[start.Count-1];
            }
            float elapsed = 0;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed/duration);
                for (int i = 0; i < tiles.Count; i++) tiles[i].anchoredPosition = Vector2.Lerp(start[i],finish[i],t*t);
                yield return null;
            }
            if (!spawning)
                foreach (GameEvent item in batch)
                {
                    ViewBoard[item.position].occupant = item.occupant;
                    ViewBoard[item.other].occupant = Occupant.Empty;
                }
            RenderAll();
            elapsed = 0;
            while (elapsed < timing.landBounceSec)
            {
                elapsed += Time.unscaledDeltaTime;
                float scale = 1+0.05f*Mathf.Sin(Mathf.Clamp01(elapsed/timing.landBounceSec)*Mathf.PI);
                foreach (GameEvent item in batch) At(item.position).tile.localScale = Vector3.one*scale;
                yield return null;
            }
            foreach (GameEvent item in batch) At(item.position).tile.localScale = Vector3.one;
        }
        public IEnumerator Shake(CellPosition p, float seconds)
        {
            RectTransform rect = (RectTransform)At(p).transform;
            Vector2 original = rect.anchoredPosition;
            float elapsed = 0;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                rect.anchoredPosition = original+Vector2.right*Mathf.Sin(elapsed/seconds*Mathf.PI*4)*8;
                yield return null;
            }
            rect.anchoredPosition = original;
        }
    }
}
