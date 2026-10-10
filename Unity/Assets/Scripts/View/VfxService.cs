using System.Collections;
using UnityEngine;
using StardustAtelier.Vfx;

namespace StardustAtelier.View
{
    // [실제 코드에 남길 주석] 효과(VFX) 프리팹을 보드 위 층에 띄웁니다(기획서 7.3).
    // 효과는 보이기만 하며 보드 상태를 바꾸지 않습니다. 언제·어디서 띄울지는 EventPlayer가 정합니다.
    public sealed class VfxService : MonoBehaviour
    {
        public StardustUiVfx blockClear, rocketH, rocketV, bomb, bombBig, meteor, prism, comboStart,
            itemCreate, obstacleBreak, vineSpread, skillCast, clear, starPop;
        // 보드와 같은 크기·위치의 층. 위치는 보드 칸 좌표(BoardView.Local)와 같은 기준입니다.
        public RectTransform layer;

        // position·target은 layer 기준 좌표입니다. target은 출발점에서 본 상대 위치로 바꿔 넘깁니다.
        public void Play(StardustUiVfx prefab, Vector2 position, Color tint, float cellSize, float length = 0,
            Vector2? target = null, Vector2[] targets = null, float delay = 0, int obstacleKind = 0, Transform parent = null)
        {
            if (prefab == null || layer == null) return;
            StardustUiVfx effect = Instantiate(prefab,parent != null ? parent : layer);
            ((RectTransform)effect.transform).anchoredPosition = position;
            Vector2[] relative = null;
            if (targets != null)
            {
                relative = new Vector2[targets.Length];
                for (int i = 0; i < targets.Length; i++) relative[i] = targets[i]-position;
            }
            effect.Play(tint,cellSize,length,target.HasValue ? target.Value-position : Vector2.zero,relative,delay,obstacleKind);
            StartCoroutine(Remove(effect,delay+effect.duration+0.1f));
        }
        // 단발 효과는 끝나면 스스로 꺼집니다. 꺼진 오브젝트가 쌓이지 않게 지웁니다.
        private static IEnumerator Remove(StardustUiVfx effect, float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (effect != null) Destroy(effect.gameObject);
        }
    }
}
