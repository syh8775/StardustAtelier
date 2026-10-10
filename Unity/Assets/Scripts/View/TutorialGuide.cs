using UnityEngine;

namespace StardustAtelier.View
{
    // [실제 코드에 남길 주석] 안내형 튜토리얼(기획서 13.3)의 손가락 표시입니다.
    // 어떤 입력을 받을지는 StardustApp이 정하고, 여기서는 손가락을 움직이기만 합니다.
    public sealed class TutorialGuide : MonoBehaviour
    {
        // 손가락 그림. 그림의 기준점(pivot)이 손끝이라, 위치를 주면 손끝이 그곳을 가리킵니다.
        public RectTransform finger;
        private Vector3 from, to;
        private bool drag;
        private float time;
        public bool Visible { get { return finger != null && finger.gameObject.activeSelf; } }

        // 한 칸에서 옆 칸으로 끄는 동작을 되풀이해 보여줍니다(스왑 안내).
        public void ShowDrag(Vector3 start, Vector3 end) { Show(start,end,true); }
        // 한 곳을 톡톡 누르는 동작을 되풀이해 보여줍니다(초상화·버튼 안내).
        public void ShowTap(Vector3 target) { Show(target,target,false); }
        private void Show(Vector3 start, Vector3 end, bool dragging)
        {
            // 같은 안내를 다시 요청하면 동작을 처음부터 다시 시작하지 않습니다.
            if (Visible && drag == dragging && from == start && to == end) return;
            from = start; to = end; drag = dragging; time = 0;
            finger.gameObject.SetActive(true);
            Apply();
        }
        public void Hide() { if (finger != null) finger.gameObject.SetActive(false); }

        private void Update()
        {
            if (!Visible) return;
            time += Time.unscaledDeltaTime;
            Apply();
        }
        private void Apply()
        {
            if (drag)
            {
                // 1.4초 주기: 누르고(0.25초) → 옆 칸으로 끌고(0.55초) → 잠깐 머문 뒤 처음으로 돌아갑니다.
                float t = time%1.4f;
                float move = Mathf.Clamp01((t-0.25f)/0.55f);
                move = move*move*(3-2*move);
                finger.position = Vector3.Lerp(from,to,move);
                finger.localScale = Vector3.one*(t < 0.25f ? Mathf.Lerp(1.15f,1f,t/0.25f) : 1f);
            }
            else
            {
                // 0.9초 주기로 눌렀다 떼는 것처럼 작아졌다 커집니다.
                float t = time%0.9f;
                finger.position = from;
                finger.localScale = Vector3.one*(1f+0.15f*Mathf.Abs(Mathf.Sin(t/0.9f*Mathf.PI)));
            }
        }
    }
}
