using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// [실제 코드에 남길 주석] 최종 그림(GPT 생성 예정)이 오기 전까지 쓸 임시 그림을 코드로 그립니다.
// 이미 있는 파일은 덮어쓰지 않습니다. 최종 그림을 같은 이름으로 넣으면 그대로 유지됩니다.
// 다시 그리려면 해당 PNG를 지우고 Build를 실행합니다.
public static class StardustArtGenerator
{
    public const string Folder = "Assets/Art/Placeholder";

    // 색 순서는 BlockColor(R, B, G, Y, P)와 같습니다. 색 이름 대신 색과 모양으로 구분합니다.
    public static readonly Color[] GemColors =
    {
        new Color(0.93f,0.29f,0.36f), new Color(0.25f,0.56f,0.98f), new Color(0.27f,0.78f,0.45f),
        new Color(1f,0.78f,0.22f), new Color(0.62f,0.40f,0.93f),
    };
    private static readonly string[] GemCodes = { "R", "B", "G", "Y", "P" };

    public static void GenerateAll()
    {
        Directory.CreateDirectory(Folder);
        for (int i = 0; i < 5; i++) { int color = i; Make("gem_"+GemCodes[i],256,p => Gem(p,color)); }
        Make("special_rocket_h",256,p => Rocket(p,false));
        Make("special_rocket_v",256,p => Rocket(p,true));
        Make("special_bomb",256,Bomb);
        Make("special_meteor",256,Meteor);
        Make("special_prism",256,Prism);
        Make("obstacle_box",256,Box);
        Make("obstacle_vine",256,Vine);
        Make("obstacle_relic",256,Relic);
        Make("obstacle_chain",256,Chain);
        Make("ice_1",256,p => Ice(p,1));
        Make("ice_2",256,p => Ice(p,2));
        Make("ui_star",128,UiStar);
        Make("ui_lock",128,UiLock);
        Make("ui_pause",128,UiPause);
        Make("ui_check",128,UiCheck);
        Make("ui_circle",128,p => p.Fill((x,y) => Sdf.Circle(x,y,0.96f),Color.white));
        Make("ui_ring",128,p => p.Fill((x,y) => Mathf.Abs(Sdf.Circle(x,y,0.88f))-0.08f,Color.white));
        Make("ui_glow",128,UiGlow);
        Make("ui_gold",128,UiGold);
        MakeSized("bg_game",720,1280,p => Background(p,11,new Color(0.09f,0.06f,0.20f),new Color(0.26f,0.14f,0.38f)));
        MakeSized("bg_map",720,1280,p => Background(p,23,new Color(0.07f,0.08f,0.22f),new Color(0.30f,0.16f,0.40f)));
        AssetDatabase.Refresh();
        foreach (string name in Directory.GetFiles(Folder,"*.png")) ConfigureSprite(name.Replace('\\','/'));
    }
    public static Sprite Load(string name) { return AssetDatabase.LoadAssetAtPath<Sprite>(Folder+"/"+name+".png"); }

    private static void Make(string name, int size, Action<Paint> draw) { MakeSized(name,size,size,draw); }
    private static void MakeSized(string name, int width, int height, Action<Paint> draw)
    {
        string path = Folder+"/"+name+".png";
        if (File.Exists(path)) return;
        Paint paint = new Paint(width,height); draw(paint);
        File.WriteAllBytes(path,paint.Encode());
    }
    private static void ConfigureSprite(string path)
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) return;
        // 2D 템플릿 기본값(Multiple)은 떨어진 조각마다 스프라이트를 나눠, 그림 일부만 쓰게 됩니다. 한 장(Single)으로 고정합니다.
        if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single && !importer.mipmapEnabled) return;
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    // ---- 그림 ----
    private static Color Mul(Color c, float k) { return new Color(c.r*k,c.g*k,c.b*k,c.a); }
    private static Color Lift(Color c, float k) { return Color.Lerp(c,Color.white,k); }
    private static readonly Color Ink = new Color(0.10f,0.06f,0.16f);

    // 보석: 진한 테두리 → 아래로 어두워지는 몸통 → 위쪽 밝은 면 → 반사광.
    private static void Gem(Paint p, int color)
    {
        Color c = GemColors[color];
        Func<float,float,float> shape;
        switch (color)
        {
            case 0: shape = (x,y) => Sdf.Diamond(x,y,0.84f)-0.03f; break;
            case 1: shape = (x,y) => Sdf.SmoothUnion(Sdf.Circle(x,y+0.20f,0.62f),Sdf.Polygon(x,y,new[] { -0.50f,0.06f, 0f,0.88f, 0.50f,0.06f }),0.12f); break;
            case 2: shape = (x,y) => { Sdf.Rotate(ref x,ref y,45); return Sdf.Vesica(x,y,1.02f,0.60f); }; break;
            case 3: shape = (x,y) => Sdf.Star5(x,y+0.04f,0.90f,0.52f)-0.04f; break;
            default: shape = (x,y) => Mathf.Max(Sdf.Circle(x,y,0.82f),-Sdf.Circle(x-0.40f,y-0.26f,0.62f)); break;
        }
        ShadedBody(p,shape,c);
    }
    private static void ShadedBody(Paint p, Func<float,float,float> shape, Color c)
    {
        p.Fill((x,y) => shape(x,y)-0.055f,Mul(c,0.32f));
        p.Shade(shape,(x,y,d) =>
        {
            float t = Mathf.Clamp01((y+1)*0.5f);
            Color body = Color.Lerp(Mul(c,0.68f),Lift(c,0.20f),t);
            body = Color.Lerp(Mul(c,0.52f),body,Mathf.SmoothStep(0,1,Mathf.Clamp01(-d/0.12f)));
            if (shape(x,y)+0.17f < 0) body = Color.Lerp(body,Lift(c,0.30f),0.35f*Mathf.Clamp01(t*1.3f));
            float hx = x+0.26f, hy = y-0.40f; Sdf.Rotate(ref hx,ref hy,-28);
            float h = 1-(hx*hx/(0.30f*0.30f)+hy*hy/(0.12f*0.12f));
            if (h > 0 && d < -0.08f) body = Color.Lerp(body,Color.white,0.75f*Mathf.SmoothStep(0,0.6f,h));
            float s = 1-((x-0.34f)*(x-0.34f)+(y-0.30f)*(y-0.30f))/(0.055f*0.055f);
            if (s > 0) body = Color.Lerp(body,Color.white,0.8f*s);
            return body;
        });
    }
    private static void Rocket(Paint p, bool vertical)
    {
        Func<float,float,float> arrow = (x,y) =>
        {
            if (vertical) { float t = x; x = y; y = t; }
            float shaft = Sdf.Box(x,y,0.40f,0.10f,0.05f);
            float right = Sdf.Polygon(x,y,new[] { 0.34f,0.34f, 0.80f,0f, 0.34f,-0.34f });
            float left = Sdf.Polygon(-x,y,new[] { 0.34f,0.34f, 0.80f,0f, 0.34f,-0.34f });
            return Mathf.Min(shaft,Mathf.Min(right,left))-0.02f;
        };
        p.Fill((x,y) => arrow(x,y)-0.07f,Ink);
        p.Shade(arrow,(x,y,d) => Color.Lerp(new Color(1f,0.95f,0.80f),Color.white,Mathf.Clamp01((y+0.4f)*1.2f)));
    }
    private static void Bomb(Paint p)
    {
        Func<float,float,float> ball = (x,y) => Sdf.Circle(x,y+0.10f,0.44f);
        Func<float,float,float> fuse = (x,y) => Sdf.Segment(x,y,0.20f,0.24f,0.36f,0.48f,0.07f);
        p.Fill((x,y) => Mathf.Min(ball(x,y),fuse(x,y))-0.07f,Color.white);
        p.Fill(fuse,new Color(0.85f,0.70f,0.45f));
        p.Shade(ball,(x,y,d) =>
        {
            Color body = Color.Lerp(new Color(0.10f,0.08f,0.16f),new Color(0.30f,0.26f,0.42f),Mathf.Clamp01((y+0.5f)));
            float h = 1-((x+0.14f)*(x+0.14f)+(y-0.06f)*(y-0.06f))/(0.12f*0.12f);
            return h > 0 ? Color.Lerp(body,Color.white,0.7f*h) : body;
        });
        p.Fill((x,y) => Sdf.Star5(x-0.42f,y-0.56f,0.20f,0.45f),new Color(1f,0.85f,0.30f));
    }
    private static void Meteor(Paint p)
    {
        for (int i = 0; i < 3; i++)
        {
            float off = (i-1)*0.20f;
            p.Fill((x,y) => Sdf.Segment(x,y,-0.70f+off*0.3f,-0.70f-off*0.3f+off,0.0f+off*0.2f,0.0f+off*0.2f,0.06f-i*0.01f),new Color(1,1,1,0.75f-i*0.15f));
        }
        Func<float,float,float> star = (x,y) => Sdf.Star5(x-0.14f,y-0.14f,0.52f,0.48f)-0.03f;
        p.Fill((x,y) => star(x,y)-0.07f,Ink);
        p.Shade(star,(x,y,d) => Color.Lerp(new Color(1f,0.92f,0.65f),Color.white,Mathf.Clamp01(y+0.3f)));
    }
    // 프리즘은 색이 없으므로 무지개 결이 있는 육각 보석입니다.
    private static void Prism(Paint p)
    {
        Func<float,float,float> shape = (x,y) => Sdf.Hexagon(x,y,0.80f)-0.05f;
        p.Fill((x,y) => shape(x,y)-0.055f,Ink);
        p.Shade(shape,(x,y,d) =>
        {
            float hue = (Mathf.Atan2(y,x)/(2*Mathf.PI)+1f) % 1f;
            Color rainbow = Color.HSVToRGB(hue,0.45f,1f);
            Color body = Color.Lerp(Mul(rainbow,0.75f),Lift(rainbow,0.35f),Mathf.Clamp01((y+1)*0.5f));
            if (shape(x,y)+0.22f < 0) body = Color.Lerp(body,Color.white,0.45f);
            float hx = x+0.24f, hy = y-0.36f;
            float h = 1-(hx*hx/(0.26f*0.26f)+hy*hy/(0.10f*0.10f));
            return h > 0 ? Color.Lerp(body,Color.white,0.8f*h) : body;
        });
    }
    private static void Box(Paint p)
    {
        Color wood = new Color(0.78f,0.50f,0.27f);
        Func<float,float,float> shape = (x,y) => Sdf.Box(x,y,0.84f,0.84f,0.14f);
        p.Fill((x,y) => shape(x,y)-0.04f,Mul(wood,0.30f));
        p.Shade(shape,(x,y,d) =>
        {
            Color body = Color.Lerp(Mul(wood,0.80f),Lift(wood,0.12f),Mathf.Clamp01((y+1)*0.5f));
            if (d > -0.14f) body = Mul(body,0.72f);
            if (Mathf.Abs(Mathf.Abs(y)-0.28f) < 0.025f && d < -0.14f) body = Mul(body,0.70f);
            float cross = Mathf.Min(Sdf.Segment(x,y,-0.62f,-0.62f,0.62f,0.62f,0.09f),Sdf.Segment(x,y,-0.62f,0.62f,0.62f,-0.62f,0.09f));
            if (cross < 0 && d < -0.14f) body = Color.Lerp(Mul(wood,0.62f),Mul(wood,0.85f),Mathf.Clamp01((y+1)*0.5f));
            foreach (float nx in new[] { -0.66f, 0.66f }) foreach (float ny in new[] { -0.66f, 0.66f })
                if (Sdf.Circle(x-nx,y-ny,0.06f) < 0) body = new Color(0.85f,0.82f,0.75f);
            return body;
        });
    }
    private static void Vine(Paint p)
    {
        Color leaf = new Color(0.28f,0.62f,0.30f);
        Func<float,float,float> shape = (x,y) => Sdf.Box(x,y,0.84f,0.84f,0.30f);
        p.Fill((x,y) => shape(x,y)-0.04f,new Color(0.06f,0.18f,0.08f));
        p.Shade(shape,(x,y,d) => Color.Lerp(new Color(0.13f,0.34f,0.16f),new Color(0.20f,0.46f,0.22f),Mathf.Clamp01((y+1)*0.5f)));
        p.Fill((x,y) => Mathf.Abs(Sdf.Circle(x+0.1f,y,0.50f))-0.05f,new Color(0.10f,0.26f,0.10f));
        float[][] leaves = { new[] { -0.42f,0.42f,30f }, new[] { 0.46f,0.30f,-40f }, new[] { 0.10f,-0.50f,80f }, new[] { -0.50f,-0.30f,-20f } };
        foreach (float[] l in leaves)
        {
            float lx = l[0], ly = l[1], angle = l[2];
            Func<float,float,float> s = (x,y) => { x -= lx; y -= ly; Sdf.Rotate(ref x,ref y,angle); return Sdf.Vesica(x*2.4f,y*2.4f,1.0f,0.6f)/2.4f; };
            p.Fill((x,y) => s(x,y)-0.03f,new Color(0.06f,0.18f,0.08f));
            p.Shade(s,(x,y,d) => Color.Lerp(leaf,Lift(leaf,0.35f),Mathf.Clamp01((y-ly+0.2f)*2.5f)));
        }
    }
    private static void Relic(Paint p)
    {
        Color gold = new Color(0.98f,0.76f,0.30f);
        Func<float,float,float> shape = (x,y) => Sdf.Circle(x,y,0.80f);
        p.Fill((x,y) => shape(x,y)-0.05f,new Color(0.35f,0.20f,0.05f));
        p.Shade(shape,(x,y,d) =>
        {
            Color body = Color.Lerp(Mul(gold,0.70f),Lift(gold,0.25f),Mathf.Clamp01((y+1)*0.5f));
            if (Mathf.Abs(Sdf.Circle(x,y,0.60f)) < 0.04f) body = Mul(gold,0.55f);
            if (Sdf.Star5(x,y+0.03f,0.42f,0.5f) < 0) body = Color.Lerp(Lift(gold,0.45f),Mul(gold,0.85f),Mathf.Clamp01(0.5f-y));
            float h = 1-((x+0.30f)*(x+0.30f)/(0.22f*0.22f)+(y-0.42f)*(y-0.42f)/(0.10f*0.10f));
            return h > 0 ? Color.Lerp(body,Color.white,0.7f*h) : body;
        });
    }
    // 사슬: 칸 위에 X자로 걸치는 쇠고리입니다. 아래 블록이 보이도록 고리 안은 비웁니다.
    private static void Chain(Paint p)
    {
        Func<float,float,float> links = (x,y) =>
        {
            float d = 10;
            for (int i = -2; i <= 2; i++)
            {
                float t = i*0.34f;
                foreach (int dir in new[] { 1, -1 })
                {
                    float lx = x-t, ly = y-t*dir;
                    Sdf.Rotate(ref lx,ref ly,dir*45+(i%2 == 0 ? 0 : 90));
                    d = Mathf.Min(d,Mathf.Abs(Sdf.Box(lx,ly,0.19f,0.10f,0.10f))-0.04f);
                }
            }
            return d;
        };
        p.Fill((x,y) => links(x,y)-0.04f,new Color(0.12f,0.10f,0.16f));
        p.Shade(links,(x,y,d) => Color.Lerp(new Color(0.55f,0.58f,0.66f),new Color(0.92f,0.94f,0.98f),Mathf.Clamp01((y+1)*0.5f)));
    }
    private static void Ice(Paint p, int layers)
    {
        Func<float,float,float> shape = (x,y) => Sdf.Box(x,y,0.94f,0.94f,0.16f);
        Color tint = layers == 2 ? new Color(0.50f,0.80f,1f,0.88f) : new Color(0.72f,0.90f,1f,0.62f);
        p.Shade(shape,(x,y,d) =>
        {
            Color body = tint;
            if (d > -0.07f) body = new Color(1,1,1,0.85f);
            float stripe = Mathf.Abs(x+y-0.35f);
            if (stripe < 0.16f) body = Color.Lerp(body,new Color(1,1,1,0.9f),0.55f);
            if (layers == 2 && (Sdf.Segment(x,y,-0.6f,0.1f,-0.1f,-0.2f,0.025f) < 0 || Sdf.Segment(x,y,-0.1f,-0.2f,0.4f,-0.65f,0.025f) < 0))
                body = new Color(1,1,1,0.95f);
            return body;
        });
    }
    private static void UiStar(Paint p)
    {
        Func<float,float,float> star = (x,y) => Sdf.Star5(x,y+0.05f,0.92f,0.50f)-0.04f;
        p.Fill((x,y) => star(x,y)-0.07f,new Color(0.40f,0.22f,0.05f));
        p.Shade(star,(x,y,d) =>
        {
            Color body = Color.Lerp(new Color(1f,0.60f,0.10f),new Color(1f,0.90f,0.40f),Mathf.Clamp01((y+1)*0.5f));
            float h = 1-((x+0.18f)*(x+0.18f)/(0.22f*0.22f)+(y-0.30f)*(y-0.30f)/(0.10f*0.10f));
            return h > 0 ? Color.Lerp(body,Color.white,0.75f*h) : body;
        });
    }
    private static void UiLock(Paint p)
    {
        p.Fill((x,y) => Mathf.Max(Mathf.Abs(Sdf.Circle(x,y-0.18f,0.34f))-0.10f,-(y-0.05f)),Color.white);
        p.Fill((x,y) => Sdf.Box(x,y+0.28f,0.56f,0.42f,0.12f),Color.white);
        p.Fill((x,y) => Sdf.Segment(x,y,0,-0.18f,0,-0.40f,0.07f),new Color(0.2f,0.18f,0.3f));
    }
    private static void UiPause(Paint p)
    {
        p.Fill((x,y) => Mathf.Min(Sdf.Box(x+0.26f,y,0.12f,0.48f,0.08f),Sdf.Box(x-0.26f,y,0.12f,0.48f,0.08f)),Color.white);
    }
    private static void UiCheck(Paint p)
    {
        p.Fill((x,y) => Sdf.Circle(x,y,0.92f),new Color(0.10f,0.40f,0.20f));
        p.Shade((x,y) => Sdf.Circle(x,y,0.84f),(x,y,d) => Color.Lerp(new Color(0.20f,0.66f,0.36f),new Color(0.40f,0.88f,0.52f),Mathf.Clamp01((y+1)*0.5f)));
        p.Fill((x,y) => Mathf.Min(Sdf.Segment(x,y,-0.42f,0.02f,-0.12f,-0.30f,0.11f),Sdf.Segment(x,y,-0.12f,-0.30f,0.46f,0.36f,0.11f)),Color.white);
    }
    // 골드(동전) 아이콘: 금색 원 + 안쪽 테두리 + 가운데 마름모. 최종 그림으로 바꿀 자리입니다.
    private static void UiGold(Paint p)
    {
        Color coin = new Color(1f,0.78f,0.25f);
        Func<float,float,float> shape = (x,y) => Sdf.Circle(x,y,0.86f);
        p.Fill((x,y) => shape(x,y)-0.07f,new Color(0.42f,0.24f,0.04f));
        p.Shade(shape,(x,y,d) =>
        {
            Color body = Color.Lerp(Mul(coin,0.72f),Lift(coin,0.30f),Mathf.Clamp01((y+1)*0.5f));
            if (Mathf.Abs(Sdf.Circle(x,y,0.66f)) < 0.045f) body = Mul(coin,0.58f);
            if (Sdf.Diamond(x,y,0.38f) < 0) body = Color.Lerp(Lift(coin,0.55f),Mul(coin,0.9f),Mathf.Clamp01(0.5f-y));
            float h = 1-((x+0.30f)*(x+0.30f)/(0.22f*0.22f)+(y-0.46f)*(y-0.46f)/(0.09f*0.09f));
            return h > 0 ? Color.Lerp(body,Color.white,0.7f*h) : body;
        });
    }
    private static void UiGlow(Paint p)
    {
        p.Shade((x,y) => Sdf.Circle(x,y,1f),(x,y,d) => { float r = Mathf.Sqrt(x*x+y*y); return new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-r),1.6f)); });
    }
    // 밤하늘 배경: 세로 그라데이션 + 흐린 성운 + 작은 별. 최종 배경 그림으로 바꿀 자리입니다.
    private static void Background(Paint p, int seed, Color top, Color bottom)
    {
        p.Pixels((u,v) => Color.Lerp(bottom,top,v));
        System.Random random = new System.Random(seed);
        for (int i = 0; i < 6; i++)
        {
            float cx = (float)random.NextDouble(), cy = (float)random.NextDouble(), radius = 0.18f+(float)random.NextDouble()*0.22f;
            Color tint = i%2 == 0 ? new Color(0.55f,0.25f,0.75f) : new Color(0.20f,0.35f,0.80f);
            p.Blend((u,v) =>
            {
                float dx = (u-cx)*p.Width/(float)p.Height, dy = v-cy;
                float a = Mathf.Clamp01(1-Mathf.Sqrt(dx*dx+dy*dy)/radius);
                return new Color(tint.r,tint.g,tint.b,0.18f*a*a);
            });
        }
        for (int i = 0; i < 260; i++)
        {
            int x = random.Next(p.Width), y = random.Next(p.Height);
            float bright = 0.35f+(float)random.NextDouble()*0.65f; int size = random.NextDouble() < 0.12 ? 2 : 1;
            p.Dot(x,y,size,new Color(1,0.96f,0.88f,bright));
        }
    }

    // ---- 최소 그리기 도구: SDF(부호 있는 거리)로 안티에일리어싱된 모양을 그립니다. 좌표는 -1~1, y는 위쪽. ----
    private sealed class Paint
    {
        public readonly int Width, Height;
        private readonly Color[] pixels;
        public Paint(int width, int height) { Width = width; Height = height; pixels = new Color[width*height]; }
        private float Unit { get { return 2f/Mathf.Min(Width,Height); } }
        public void Fill(Func<float,float,float> sdf, Color color) { Shade(sdf,(x,y,d) => color); }
        public void Shade(Func<float,float,float> sdf, Func<float,float,float,Color> color)
        {
            float unit = Unit;
            for (int py = 0; py < Height; py++)
                for (int px = 0; px < Width; px++)
                {
                    float x = (px+0.5f)*unit-Width*unit*0.5f, y = (py+0.5f)*unit-Height*unit*0.5f;
                    float d = sdf(x,y);
                    float coverage = Mathf.Clamp01(0.5f-d/unit);
                    if (coverage <= 0) continue;
                    Color c = color(x,y,d); c.a *= coverage;
                    Over(py*Width+px,c);
                }
        }
        public void Pixels(Func<float,float,Color> color)
        { for (int py = 0; py < Height; py++) for (int px = 0; px < Width; px++) pixels[py*Width+px] = color((px+0.5f)/Width,(py+0.5f)/Height); }
        public void Blend(Func<float,float,Color> color)
        { for (int py = 0; py < Height; py++) for (int px = 0; px < Width; px++) Over(py*Width+px,color((px+0.5f)/Width,(py+0.5f)/Height)); }
        public void Dot(int x, int y, int size, Color color)
        {
            for (int dy = -size; dy <= size; dy++) for (int dx = -size; dx <= size; dx++)
            {
                int px = x+dx, py = y+dy; if (px < 0 || py < 0 || px >= Width || py >= Height) continue;
                float falloff = size == 1 ? (dx == 0 && dy == 0 ? 1 : 0.25f) : Mathf.Clamp01(1-(Mathf.Abs(dx)+Mathf.Abs(dy))/3f);
                Color c = color; c.a *= falloff; Over(py*Width+px,c);
            }
        }
        private void Over(int index, Color c)
        {
            Color dst = pixels[index];
            float a = c.a+dst.a*(1-c.a);
            if (a <= 0) { pixels[index] = new Color(0,0,0,0); return; }
            pixels[index] = new Color((c.r*c.a+dst.r*dst.a*(1-c.a))/a,(c.g*c.a+dst.g*dst.a*(1-c.a))/a,(c.b*c.a+dst.b*dst.a*(1-c.a))/a,a);
        }
        public byte[] Encode()
        {
            Texture2D texture = new Texture2D(Width,Height,TextureFormat.RGBA32,false);
            texture.SetPixels(pixels); texture.Apply();
            byte[] png = texture.EncodeToPNG(); UnityEngine.Object.DestroyImmediate(texture);
            return png;
        }
    }

    // 모양별 거리 함수. 안쪽이 음수입니다. (출처: Inigo Quilez의 2D 거리 함수 공식)
    private static class Sdf
    {
        public static float Circle(float x, float y, float r) { return Mathf.Sqrt(x*x+y*y)-r; }
        public static float Diamond(float x, float y, float r) { return (Mathf.Abs(x)+Mathf.Abs(y)-r)*0.70710678f; }
        public static float Box(float x, float y, float hx, float hy, float radius)
        {
            float qx = Mathf.Abs(x)-hx+radius, qy = Mathf.Abs(y)-hy+radius;
            float ox = Mathf.Max(qx,0), oy = Mathf.Max(qy,0);
            return Mathf.Sqrt(ox*ox+oy*oy)+Mathf.Min(Mathf.Max(qx,qy),0)-radius;
        }
        public static float Segment(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float px = x-ax, py = y-ay, ex = bx-ax, ey = by-ay;
            float h = Mathf.Clamp01((px*ex+py*ey)/(ex*ex+ey*ey));
            float dx = px-ex*h, dy = py-ey*h;
            return Mathf.Sqrt(dx*dx+dy*dy)-r;
        }
        public static float Vesica(float x, float y, float r, float d)
        {
            x = Mathf.Abs(x); y = Mathf.Abs(y);
            float b = Mathf.Sqrt(r*r-d*d);
            return (y-b)*d > x*b ? Mathf.Sqrt(x*x+(y-b)*(y-b)) : Mathf.Sqrt((x+d)*(x+d)+y*y)-r;
        }
        public static float Hexagon(float x, float y, float r)
        {
            const float kx = -0.866025404f, ky = 0.5f, kz = 0.577350269f;
            x = Mathf.Abs(x); y = Mathf.Abs(y);
            float dot = 2*Mathf.Min(kx*x+ky*y,0);
            x -= dot*kx; y -= dot*ky;
            x -= Mathf.Clamp(x,-kz*r,kz*r); y -= r;
            return Mathf.Sqrt(x*x+y*y)*Mathf.Sign(y);
        }
        public static float Star5(float x, float y, float r, float rf)
        {
            const float k1x = 0.809016994375f, k1y = -0.587785252292f;
            float k2x = -k1x, k2y = k1y;
            x = Mathf.Abs(x);
            float d1 = 2*Mathf.Max(k1x*x+k1y*y,0); x -= d1*k1x; y -= d1*k1y;
            float d2 = 2*Mathf.Max(k2x*x+k2y*y,0); x -= d2*k2x; y -= d2*k2y;
            x = Mathf.Abs(x); y -= r;
            float bax = rf*-k1y, bay = rf*k1x-1;
            float h = Mathf.Clamp((x*bax+y*bay)/(bax*bax+bay*bay),0,r);
            float dx = x-bax*h, dy = y-bay*h;
            return Mathf.Sqrt(dx*dx+dy*dy)*Mathf.Sign(y*bax-x*bay);
        }
        // points: x0,y0,x1,y1,... 순서의 꼭짓점 목록
        public static float Polygon(float x, float y, float[] points)
        {
            int n = points.Length/2;
            float dx0 = x-points[0], dy0 = y-points[1];
            float d = dx0*dx0+dy0*dy0, s = 1;
            for (int i = 0, j = n-1; i < n; j = i, i++)
            {
                float vix = points[i*2], viy = points[i*2+1], vjx = points[j*2], vjy = points[j*2+1];
                float ex = vjx-vix, ey = vjy-viy, wx = x-vix, wy = y-viy;
                float t = Mathf.Clamp01((wx*ex+wy*ey)/(ex*ex+ey*ey));
                float bx = wx-ex*t, by = wy-ey*t;
                d = Mathf.Min(d,bx*bx+by*by);
                bool c1 = y >= viy, c2 = y < vjy, c3 = ex*wy > ey*wx;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s*Mathf.Sqrt(d);
        }
        public static float SmoothUnion(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f+0.5f*(b-a)/k);
            return Mathf.Lerp(b,a,h)-k*h*(1-h);
        }
        public static void Rotate(ref float x, ref float y, float degrees)
        {
            float r = degrees*Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            float nx = c*x-s*y, ny = s*x+c*y; x = nx; y = ny;
        }
    }
}
