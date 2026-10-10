using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StardustAtelier.Core;

namespace StardustAtelier.View
{
    // [실제 코드에 남길 주석] Core가 낸 이벤트를 순서대로 재생합니다(7.3). 보드 상태는 바꾸지 않고 보여주기만 합니다.
    // 소리(13.5)와 효과(VFX)도 여기서 이벤트에 맞춰 냅니다.
    public sealed class EventPlayer
    {
        private readonly BoardView board;
        private readonly HudView hud;
        private readonly TimingConfig timing;
        private AudioService Audio { get { return hud.audioService; } }
        private VfxService Fx { get { return hud.vfx; } }
        public EventPlayer(BoardView view, HudView header, TimingConfig values) { board = view; hud = header; timing = values; }
        // 아이템이 새로 생길 때 부릅니다. 돌려준 것이 있으면 그것이 끝날 때까지 재생을 멈춥니다(첫 생성 설명 팝업).
        public System.Func<OccupantKind,IEnumerator> ItemCreated;

        public IEnumerator Play(List<GameEvent> events)
        {
            bool hit = false, matchPlayed = false;
            int step = 1;
            // 이번 구간에서 가장 긴 효과 시간. 낙하 전에 이만큼은 기다립니다.
            float effectWait = 0;
            for (int i = 0; i < events.Count; i++)
            {
                GameEvent item = events[i];
                if (item.kind == GameEventKind.Swapped)
                {
                    bool rejected = i+1 < events.Count && events[i+1].kind == GameEventKind.SwapRejected;
                    if (Audio != null) Audio.Play(rejected ? Audio.swapInvalid : Audio.swap);
                    yield return board.AnimateSwap(item.position,item.other,timing.swapSec);
                }
                // 무효 스왑은 문구 없이 되돌아가는 움직임으로만 알립니다.
                else if (item.kind == GameEventKind.SwapRejected) yield return board.AnimateSwap(item.position,item.other,timing.swapBackSec);
                else if (item.kind == GameEventKind.CellHit)
                { board.ApplyCell(item.position,item.cell); hit = true; }
                else if (item.kind == GameEventKind.BlockCleared)
                {
                    if (item.occupant.kind != OccupantKind.ColorBlock) continue;
                    Effect(Fx != null ? Fx.blockClear : null,item.position,CellView.ColorFor(item.occupant.color));
                    // 매치음은 단계마다 한 번, 연쇄 단계가 오를수록 반음씩 높게 냅니다.
                    if (!matchPlayed && Audio != null) { Audio.PlayMatch(step); matchPlayed = true; }
                }
                else if (item.kind == GameEventKind.SpecialActivated) effectWait = Mathf.Max(effectWait,Activate(events,i));
                else if (item.kind == GameEventKind.IceDamaged) Obstacle(item.position,0);
                else if (item.kind == GameEventKind.ObstacleDamaged) Obstacle(item.position,item.occupant.kind == OccupantKind.Vine ? 2 : 1);
                else if (item.kind == GameEventKind.ChainBroken) Obstacle(item.position,3);
                else if (item.kind == GameEventKind.SpecialCreated || item.kind == GameEventKind.VineSpread)
                {
                    board.ViewBoard[item.position].occupant = item.occupant;
                    board.At(item.position).Render(board.ViewBoard[item.position]);
                    if (item.kind == GameEventKind.SpecialCreated)
                    {
                        Effect(Fx != null ? Fx.itemCreate : null,item.position,Gold);
                        IEnumerator pause = ItemCreated != null ? ItemCreated(item.occupant.kind) : null;
                        if (pause != null) yield return pause;
                    }
                    else { SpreadVine(item.position); yield return new WaitForSecondsRealtime(timing.vineSpreadSec); }
                }
                else if (item.kind == GameEventKind.SkillUsed)
                {
                    if (Audio != null) Audio.Play(Audio.skill);
                    SkillCast(item.index);
                    yield return new WaitForSecondsRealtime(0.35f);
                }
                else if (item.kind == GameEventKind.Fell || item.kind == GameEventKind.Spawned)
                {
                    if (hit) { yield return Settle(effectWait); hit = false; effectWait = 0; board.ClearMarks(); }
                    List<GameEvent> batch = new List<GameEvent> { item };
                    while (i+1 < events.Count && events[i+1].kind == item.kind) batch.Add(events[++i]);
                    yield return board.AnimateGravity(batch,timing);
                }
                else if (item.kind == GameEventKind.RelicCollected)
                {
                    board.ViewBoard[item.position].occupant = Occupant.Empty;
                    board.At(item.position).Render(board.ViewBoard[item.position]);
                    Effect(Fx != null ? Fx.itemCreate : null,item.position,Gold);
                }
                else if (item.kind == GameEventKind.GoalChanged) hud.SetGoal(item.index,item.value);
                else if (item.kind == GameEventKind.MovesChanged) hud.SetMoves(item.value);
                else if (item.kind == GameEventKind.GaugeChanged && hud.party != null) hud.party.SetGauge(item.index,item.value);
                else if (item.kind == GameEventKind.StepEnded)
                {
                    if (hit) { yield return Settle(effectWait); hit = false; effectWait = 0; }
                    if (item.value >= 2) hud.ShowCombo(item.value);
                    step = item.value+1; matchPlayed = false;
                    yield return new WaitForSecondsRealtime(timing.stepGapSec);
                }
                else if (item.kind == GameEventKind.Shuffled)
                {
                    // 기획서 9.3 문구를 셔플 시간 동안 표시합니다.
                    hud.ShowTip("이동할 수 있는 블록이 없어 셔플합니다");
                    yield return new WaitForSecondsRealtime(timing.shuffleSec);
                    board.Configure(item.board,(int)((RectTransform)board.transform).sizeDelta.x);
                    hud.HideTip();
                }
            }
            if (hit) yield return Settle(effectWait);
            board.ClearMarks();
        }

        private static readonly Color Gold = new Color(1f,0.85f,0.45f);
        // 블록이 사라지는 연출과, 그 구간에서 가장 긴 아이템 효과가 끝날 때까지 기다립니다.
        private IEnumerator Settle(float effectWait)
        {
            yield return board.ClearPop(timing.clearSec);
            if (effectWait > timing.clearSec) yield return new WaitForSecondsRealtime(effectWait-timing.clearSec);
        }
        private void Effect(StardustAtelier.Vfx.StardustUiVfx prefab, CellPosition p, Color tint)
        { if (Fx != null && prefab != null) Fx.Play(prefab,board.Local(p),tint,board.CellSize); }
        private void Obstacle(CellPosition p, int kind)
        {
            if (Audio != null) Audio.Play(Audio.obstacle);
            if (Fx != null) Fx.Play(Fx.obstacleBreak,board.Local(p),Color.white,board.CellSize,0,null,null,0,kind);
        }
        // 아이템 발동: 종류에 맞는 효과와 소리를 내고, 그 효과의 길이(7.3)를 돌려줍니다.
        private float Activate(List<GameEvent> events, int index)
        {
            GameEvent item = events[index];
            Vector2 at = board.Local(item.position);
            float cell = board.CellSize, size = ((RectTransform)board.transform).sizeDelta.x;
            // value 2 = 아이템 두 개(또는 프리즘+색 블록)를 합친 조합입니다.
            if (item.value == 2)
            {
                if (Fx != null) Fx.Play(Fx.comboStart,at,Gold,cell);
                return timing.comboIntroSec+Combo(item,at,cell,size);
            }
            switch (item.occupant.kind)
            {
                case OccupantKind.Rocket:
                    if (Audio != null) Audio.Play(Audio.rocket);
                    // 로켓 효과는 양쪽으로 길이의 절반씩 뻗으므로, 어느 칸에서든 보드 끝에 닿도록 보드 두 배 길이를 줍니다.
                    if (Fx != null) Fx.Play(item.occupant.orientation == Orientation.H ? Fx.rocketH : Fx.rocketV,at,Gold,cell,size*2);
                    return timing.rocketSec;
                case OccupantKind.Bomb:
                    if (Audio != null) Audio.Play(Audio.bomb);
                    if (Fx != null) Fx.Play(Fx.bomb,at,Gold,cell);
                    return timing.bombSec;
                case OccupantKind.Meteor:
                {
                    if (Audio != null) Audio.Play(Audio.meteor);
                    // 별똥별이 날아갈 칸 = 이 발동 바로 뒤에 타격받는 칸.
                    List<Vector2> targets = HitsAfter(events,index,1);
                    if (Fx != null && targets.Count > 0) Fx.Play(Fx.meteor,at,Gold,cell,0,targets[0]);
                    return timing.meteorFlightSec;
                }
                case OccupantKind.Prism:
                {
                    if (Audio != null) Audio.Play(Audio.prism);
                    List<Vector2> targets = HitsAfter(events,index,81);
                    if (Fx != null) Fx.Play(Fx.prism,at,Color.white,cell,0,null,targets.ToArray());
                    return timing.prismSec;
                }
            }
            return 0;
        }
        // 조합(6.4)별 연출. 모이는 빛(조합 시작) 뒤에 이어서 터지도록 지연을 주고, 그 길이를 돌려줍니다.
        private float Combo(GameEvent item, Vector2 at, float cell, float size)
        {
            OccupantKind a = (OccupantKind)item.index, b = item.occupant.kind;
            bool rocket = a == OccupantKind.Rocket || b == OccupantKind.Rocket;
            bool bomb = a == OccupantKind.Bomb || b == OccupantKind.Bomb;
            float delay = timing.comboIntroSec;
            if (a == OccupantKind.Bomb && b == OccupantKind.Bomb)
            {
                // 폭탄 + 폭탄: 5×5 큰 폭발.
                if (Audio != null) Audio.Play(Audio.bomb);
                if (Fx != null) Fx.Play(Fx.bombBig,at,Gold,cell,0,null,null,delay);
                return timing.bombSec;
            }
            if (a == OccupantKind.Rocket && b == OccupantKind.Rocket)
            {
                // 로켓 + 로켓: 중심의 행과 열.
                if (Audio != null) Audio.Play(Audio.rocket);
                if (Fx != null) { Fx.Play(Fx.rocketH,at,Gold,cell,size*2,null,null,delay); Fx.Play(Fx.rocketV,at,Gold,cell,size*2,null,null,delay); }
                return timing.rocketSec;
            }
            if (rocket && bomb)
            {
                // 로켓 + 폭탄: 중심 행·열과 그 양옆, 세 줄씩.
                if (Audio != null) { Audio.Play(Audio.rocket); Audio.Play(Audio.bomb); }
                if (Fx != null)
                    for (int offset = -1; offset <= 1; offset++)
                    {
                        Fx.Play(Fx.rocketH,at+new Vector2(0,offset*cell),Gold,cell,size*2,null,null,delay);
                        Fx.Play(Fx.rocketV,at+new Vector2(offset*cell,0),Gold,cell,size*2,null,null,delay);
                    }
                return timing.rocketSec;
            }
            // 프리즘·별똥별이 낀 조합은 모이는 빛과 프리즘 소리로 알립니다.
            if (Audio != null) Audio.Play(a == OccupantKind.Meteor || b == OccupantKind.Meteor ? Audio.meteor : Audio.prism);
            return 0;
        }
        // 발동 뒤에 이어지는 타격 칸들(다음 발동이나 낙하 전까지).
        private List<Vector2> HitsAfter(List<GameEvent> events, int index, int limit)
        {
            List<Vector2> result = new List<Vector2>();
            for (int i = index+1; i < events.Count && result.Count < limit; i++)
            {
                GameEventKind kind = events[i].kind;
                if (kind == GameEventKind.SpecialActivated || kind == GameEventKind.Fell || kind == GameEventKind.Spawned || kind == GameEventKind.StepEnded) break;
                if (kind == GameEventKind.CellHit) result.Add(board.Local(events[i].position));
            }
            return result;
        }
        // 덩굴 번짐: 이웃한 덩굴 칸에서 새 칸으로 뻗는 효과.
        private void SpreadVine(CellPosition target)
        {
            if (Fx == null) return;
            foreach (CellPosition q in BoardGenerator.Neighbors(target))
            {
                if (!board.ViewBoard.Contains(q) || board.ViewBoard[q].occupant.kind != OccupantKind.Vine) continue;
                Fx.Play(Fx.vineSpread,board.Local(q),new Color(0.35f,0.85f,0.4f),board.CellSize,0,board.Local(target));
                return;
            }
        }
        // 스킬 발동: 초상화에서 보드 가운데로 퍼지는 빛(캐릭터 속성 색).
        private void SkillCast(int slot)
        {
            if (Fx == null || hud.party == null || slot < 0 || slot >= hud.party.slots.Length) return;
            Vector2 from = Fx.layer.InverseTransformPoint(hud.party.slots[slot].transform.position);
            Fx.Play(Fx.skillCast,from,hud.party.SlotColor(slot),board.CellSize,0,Vector2.zero);
        }
    }
}
