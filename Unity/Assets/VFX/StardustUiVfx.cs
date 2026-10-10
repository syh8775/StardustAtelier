using UnityEngine;
using UnityEngine.UI;

namespace StardustAtelier.Vfx
{
    // 팀 의도: Overlay 캔버스에서만 그림을 그리는 독립 에셋입니다. 보드/게임 규칙에 의존하지 않습니다.
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public sealed class StardustUiVfx : MaskableGraphic
    {
        public enum Effect { BlockClear, RocketH, RocketV, Bomb, BombBig, Meteor, Prism, ComboStart,
            ItemCreate, ObstacleBreak, VineSpread, SkillCast, GaugeFull, Clear, StarPop }
        public Effect effect;
        public float duration = 0.2f;
        public float cellPixels = 128f;
        public float lengthPixels = 900f;
        public Vector2 destination = new Vector2(256f, 128f);
        public Vector2[] targets = new Vector2[0];
        public int obstacleKind;
        private float elapsed;
        private float delay;
        private bool playing;
        public bool IsPlaying { get { return playing; } }
        public float Elapsed { get { return elapsed; } }

        protected override void Awake() { base.Awake(); raycastTarget = false; }
        protected override void OnEnable()
        {
            base.OnEnable(); elapsed = 0f; playing = true; raycastTarget = false; SetVerticesDirty();
        }

        // 학습용: 모든 위치는 프리팹 루트(출발 칸 중심) 기준 캔버스 로컬 좌표입니다.
        // 팀 제약: 연결 코드는 Instantiate 후 같은 프레임에 Play를 호출합니다. delay는 호출자가 정한 시차입니다.
        public void Play(Color tint, float cellSize = 128f, float travelLength = 900f,
            Vector2 target = default(Vector2), Vector2[] targetPositions = null,
            float delaySeconds = 0f, int obstacleType = 0)
        {
            color = tint; cellPixels = Mathf.Max(1f, cellSize); lengthPixels = Mathf.Max(0f, travelLength);
            destination = target; targets = targetPositions == null ? new Vector2[0] : (Vector2[])targetPositions.Clone();
            delay = Mathf.Max(0f, delaySeconds); obstacleKind = Mathf.Clamp(obstacleType, 0, 3);
            elapsed = 0f; playing = true; gameObject.SetActive(true); SetVerticesDirty();
        }

        private void Update()
        {
            if (!playing) return;
            // 학습용: timeScale=0인 일시정지 화면에서도 연출 시간이 흐릅니다.
            elapsed += Time.unscaledDeltaTime;
            if (effect != Effect.GaugeFull && elapsed >= delay + duration)
            { playing = false; gameObject.SetActive(false); return; }
            SetVerticesDirty();
        }

        // 팀 의도: 에셋 미리보기 전용. 실제 재생은 Play/Update 경로를 사용합니다.
        public void Preview(float normalizedTime)
        { elapsed = delay + Mathf.Clamp01(normalizedTime) * duration; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (elapsed < delay || duration <= 0f) return;
            float t = effect == Effect.GaugeFull ? Mathf.Repeat((elapsed-delay)/1.8f, 1f)
                : Mathf.Clamp01((elapsed-delay)/duration);
            float s = cellPixels;
            Color tint = color;
            float fade = 1f-t;
            if (effect == Effect.GaugeFull)
            {
                float pulse = 0.65f+0.25f*Mathf.Sin(t*Mathf.PI*2f);
                Ring(mesh, Vector2.zero, s*(0.44f+0.025f*pulse), s*0.025f, Alpha(tint,pulse));
                for(int i=0;i<6;i++) Star(mesh,Direction(i*60f+t*360f)*s*0.46f,s*0.035f,Alpha(tint,0.8f));
            }
            else if (effect == Effect.RocketH || effect == Effect.RocketV)
            {
                Vector2 axis = effect == Effect.RocketH ? Vector2.right : Vector2.up;
                float reach = lengthPixels*0.5f*Mathf.Min(1f,t*1.4f);
                for(int side=-1;side<=1;side+=2)
                {
                    Vector2 end=axis*reach*side;
                    Line(mesh,axis*Mathf.Max(0f,reach-s*1.8f)*side,end,s*0.08f,Alpha(tint,fade));
                    Line(mesh,axis*Mathf.Max(0f,reach-s)*side,end,s*0.025f,Alpha(Color.white,fade));
                    Star(mesh,end,s*0.14f,Alpha(tint,fade));
                }
            }
            else if(effect == Effect.Meteor || effect == Effect.SkillCast)
            {
                Vector2 bend = new Vector2(-destination.y,destination.x).normalized*s*0.6f;
                Vector2 head = destination*t+bend*Mathf.Sin(t*Mathf.PI);
                for(int i=1;i<=8;i++)
                {
                    float q=Mathf.Max(0f,t-i*0.025f);
                    Vector2 tail=destination*q+bend*Mathf.Sin(q*Mathf.PI);
                    Star(mesh,tail,s*(0.095f-i*0.008f),Alpha(tint,(1f-i/9f)*fade));
                }
                Star(mesh,head,s*0.19f,Alpha(Color.Lerp(tint,Color.white,0.6f),Mathf.Min(1f,fade*5f)));
                if(t>0.7f) Ring(mesh,destination,s*(t-0.7f)*1.5f,s*0.025f,Alpha(tint,fade));
            }
            else if(effect == Effect.Prism)
            {
                for(int i=0;i<targets.Length;i++)
                {
                    Vector2 end=targets[i]*Mathf.Min(1f,t*3f);
                    Line(mesh,Vector2.zero,end,s*0.045f,Alpha(tint,fade*0.6f));
                    Line(mesh,Vector2.zero,end,s*0.012f,Alpha(Color.white,fade));
                    Star(mesh,end,s*0.095f,Alpha(tint,fade));
                }
                Star(mesh,Vector2.zero,s*(0.25f+0.2f*t),Alpha(Color.white,fade));
            }
            else if(effect == Effect.VineSpread)
            {
                Vector2 end=destination.sqrMagnitude>1f?destination:Vector2.right*s;
                Vector2 previous=Vector2.zero;
                for(int i=1;i<=18;i++)
                {
                    float q=t*i/18f;
                    Vector2 p=end*q+new Vector2(-end.y,end.x).normalized*Mathf.Sin(q*12f)*s*0.08f;
                    Line(mesh,previous,p,s*0.045f,Alpha(tint,Mathf.Min(1f,fade*5f)));
                    if(i%4==0) Diamond(mesh,p,s*0.1f,s*0.04f,i*37f,Alpha(tint,fade));
                    previous=p;
                }
            }
            else if(effect == Effect.Bomb || effect == Effect.BombBig)
            {
                float radius=s*(effect == Effect.BombBig?2.5f:1.5f);
                Glow(mesh,Vector2.zero,radius*t,Alpha(tint,fade*.55f));
                Ring(mesh,Vector2.zero,radius*t,s*0.08f*fade,Alpha(tint,fade));
                Ring(mesh,Vector2.zero,radius*t*0.78f,s*0.025f,Alpha(Color.white,fade));
                Burst(mesh,16,radius*0.9f,t,tint,false);
            }
            else if(effect == Effect.ComboStart)
            {
                for(int i=0;i<12;i++)
                { Vector2 p=Direction(i*30f+t*100f)*s*(1f-t)*0.85f; Star(mesh,p,s*0.075f,Alpha(tint,1f-t*0.5f)); }
                Star(mesh,Vector2.zero,s*0.32f*t,Alpha(Color.white,fade));
            }
            else if(effect == Effect.ObstacleBreak)
            {
                Color[] shades={new Color(0.55f,0.9f,1f),new Color(0.68f,0.36f,0.13f),
                    new Color(0.25f,0.8f,0.3f),new Color(0.7f,0.77f,0.9f)};
                Color c=shades[obstacleKind];
                for(int i=0;i<12;i++)
                {
                    Vector2 p=Direction(i*137.5f)*s*t*(0.35f+(i%3)*0.16f)+Vector2.down*s*t*t*0.25f;
                    if(obstacleKind==3) Ring(mesh,p,s*0.06f,s*0.02f,Alpha(c,fade));
                    else Diamond(mesh,p,s*(obstacleKind==2?0.13f:0.075f),s*0.045f,i*31f+t*160f,Alpha(c,fade));
                }
            }
            else if(effect == Effect.Clear)
            {
                for(int i=0;i<40;i++)
                {
                    Color c=Color.HSVToRGB((i*0.137f)%1f,0.55f,1f);
                    Vector2 p=new Vector2(Mathf.Sin(i*17f)*s*4f*t,s*(1.8f*t-3f*t*t)*(0.5f+(i%7)/7f));
                    Diamond(mesh,p,s*0.055f,s*0.11f,i*71f+t*240f,Alpha(c,fade));
                }
            }
            else
            {
                float radius=effect==Effect.BlockClear?s*0.65f:s*0.8f;
                Burst(mesh,12,radius,t,tint,true);
                if(effect!=Effect.BlockClear)
                { Star(mesh,Vector2.zero,s*0.3f*Mathf.Sin(t*Mathf.PI),Alpha(Color.white,fade));
                  Ring(mesh,Vector2.zero,s*t*0.5f,s*0.025f,Alpha(tint,fade)); }
            }
        }

        private static Color Alpha(Color c,float alpha) { c.a*=Mathf.Clamp01(alpha); return c; }
        private static Vector2 Direction(float degrees)
        { float a=degrees*Mathf.Deg2Rad; return new Vector2(Mathf.Cos(a),Mathf.Sin(a)); }
        private void Burst(VertexHelper m,int count,float radius,float t,Color c,bool stars)
        {
            for(int i=0;i<count;i++)
            {
                Vector2 p=Direction(i*360f/count+15f)*radius*t*(0.7f+(i%3)*0.15f);
                float size=cellPixels*(0.05f+(i%3)*0.015f)*(1f-t);
                if(stars) Star(m,p,size,Alpha(c,1f-t));
                else Diamond(m,p,size,size*0.5f,i*30f+t*90f,Alpha(c,1f-t));
            }
        }
        private static void Quad(VertexHelper m,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
        {
            int n=m.currentVertCount;
            m.AddVert(a,color,Vector2.zero); m.AddVert(b,color,Vector2.zero);
            m.AddVert(c,color,Vector2.zero); m.AddVert(d,color,Vector2.zero);
            m.AddTriangle(n,n+1,n+2); m.AddTriangle(n,n+2,n+3);
        }
        private static void Line(VertexHelper m,Vector2 a,Vector2 b,float width,Color c)
        {
            Vector2 v=(b-a).normalized; Vector2 side=new Vector2(-v.y,v.x)*width*0.5f;
            Quad(m,a-side*4f,a+side*4f,b+side*4f,b-side*4f,Alpha(c,.10f));
            Quad(m,a-side*2f,a+side*2f,b+side*2f,b-side*2f,Alpha(c,.18f));
            Quad(m,a-side,a+side,b+side,b-side,c);
        }
        private static void Diamond(VertexHelper m,Vector2 p,float x,float y,float angle,Color c)
        {
            Vector2 right=Direction(angle)*x; Vector2 up=Direction(angle+90f)*y;
            Quad(m,p-right,p+up,p+right,p-up,c);
        }
        private static void Star(VertexHelper m,Vector2 p,float size,Color c)
        {
            Glow(m,p,size*2.8f,Alpha(c,.5f));
            Diamond(m,p,size,size*0.32f,0f,c); Diamond(m,p,size,size*0.32f,90f,c);
        }
        // 팀 의도: UI 기본 머티리얼의 정점 알파로 빛 번짐을 표현해 추가 셰이더가 필요 없습니다.
        private static void Glow(VertexHelper m,Vector2 p,float radius,Color c)
        {
            int n=m.currentVertCount;m.AddVert(p,c,Vector2.zero);
            Color edge=c;edge.a=0f;
            for(int i=0;i<=24;i++)m.AddVert(p+Direction(i*15f)*radius,edge,Vector2.zero);
            for(int i=0;i<24;i++)m.AddTriangle(n,n+i+1,n+i+2);
        }
        private static void Ring(VertexHelper m,Vector2 p,float radius,float width,Color c)
        {
            for(int i=0;i<40;i++)
            {
                Vector2 a=Direction(i*9f),b=Direction((i+1)*9f);
                Quad(m,p+a*Mathf.Max(0f,radius-width),p+a*radius,p+b*radius,p+b*Mathf.Max(0f,radius-width),c);
            }
        }
    }
}
