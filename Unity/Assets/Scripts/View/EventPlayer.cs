using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StardustAtelier.Core;

namespace StardustAtelier.View
{
    public sealed class EventPlayer
    {
        private readonly BoardView board;
        private readonly HudView hud;
        private readonly TimingConfig timing;
        public EventPlayer(BoardView view, HudView header, TimingConfig values) { board = view; hud = header; timing = values; }
        public IEnumerator Play(List<GameEvent> events)
        {
            bool hit = false;
            for (int i = 0; i < events.Count; i++)
            {
                GameEvent item = events[i];
                if (item.kind == GameEventKind.Swapped) yield return board.AnimateSwap(item.position,item.other,timing.swapSec);
                // 무효 스왑은 문구 없이 되돌아가는 움직임으로만 알립니다.
                else if (item.kind == GameEventKind.SwapRejected) yield return board.AnimateSwap(item.position,item.other,timing.swapBackSec);
                else if (item.kind == GameEventKind.CellHit)
                { board.ApplyCell(item.position,item.cell); hit = true; }
                else if (item.kind == GameEventKind.SpecialCreated || item.kind == GameEventKind.VineSpread)
                {
                    board.ViewBoard[item.position].occupant = item.occupant;
                    board.At(item.position).Render(board.ViewBoard[item.position]);
                }
                else if (item.kind == GameEventKind.Fell || item.kind == GameEventKind.Spawned)
                {
                    if (hit) { yield return board.ClearPop(timing.clearSec); hit = false; board.ClearMarks(); }
                    List<GameEvent> batch = new List<GameEvent> { item };
                    while (i+1 < events.Count && events[i+1].kind == item.kind) batch.Add(events[++i]);
                    yield return board.AnimateGravity(batch,timing);
                }
                else if (item.kind == GameEventKind.RelicCollected)
                {
                    board.ViewBoard[item.position].occupant = Occupant.Empty;
                    board.At(item.position).Render(board.ViewBoard[item.position]);
                }
                else if (item.kind == GameEventKind.GoalChanged) hud.SetGoal(item.index,item.value);
                else if (item.kind == GameEventKind.MovesChanged) hud.SetMoves(item.value);
                else if (item.kind == GameEventKind.GaugeChanged && hud.party != null) hud.party.SetGauge(item.index,item.value);
                else if (item.kind == GameEventKind.StepEnded)
                {
                    if (hit) { yield return board.ClearPop(timing.clearSec); hit = false; }
                    if (item.value >= 2) hud.ShowCombo(item.value);
                    yield return new WaitForSecondsRealtime(timing.stepGapSec);
                }
                else if (item.kind == GameEventKind.Shuffled)
                {
                    // 기획서 9.3 문구를 셔플 시간 동안 표시합니다.
                    hud.ShowTip("둘 수 있는 블록이 없어 섞습니다");
                    yield return new WaitForSecondsRealtime(timing.shuffleSec);
                    board.Configure(item.board,(int)((RectTransform)board.transform).sizeDelta.x);
                    hud.HideTip();
                }
            }
            if (hit) yield return board.ClearPop(timing.clearSec);
            board.ClearMarks();
        }
    }
}
