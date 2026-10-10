using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using StardustAtelier.View;

// [실제 코드에 남길 주석] 플레이 씬(Assets/Scenes/Stardust.unity)을 처음부터 다시 만듭니다. 기존 SampleScene은 보존합니다.
// 배치는 기획서 13장(세로 1080×1920)을 따릅니다. 그림은 Assets/Art/Placeholder의 같은 이름 파일을 씁니다.
public static class StardustSceneBuilder
{
    private static TMP_FontAsset font;
    private static Material outlined;
    private static Sprite rounded, circle, ring, glow, star, lockSprite, pause, check, bgGame, bgMap;
    // 색: 밤하늘 남보라 바탕 + 금빛 강조.
    private static readonly Color ink = new Color(0.10f,0.06f,0.16f);
    private static readonly Color panel = new Color(0.17f,0.13f,0.32f,0.94f);
    private static readonly Color panelLight = new Color(0.27f,0.21f,0.47f,1f);
    private static readonly Color gold = new Color(1f,0.80f,0.36f);
    private static readonly Color goldDark = new Color(0.62f,0.40f,0.10f);
    private static readonly Color lavender = new Color(0.80f,0.76f,0.95f);

    [MenuItem("Stardust/Build Play Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building the scene.");
        Directory.CreateDirectory("Assets/UI"); Directory.CreateDirectory("Assets/Scenes");
        StardustArtGenerator.GenerateAll();
        PrepareAssets();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        GameObject cameraObject = new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));
        cameraObject.tag = "MainCamera"; cameraObject.transform.position = new Vector3(0,0,-10);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = ink;
        GameObject root = new GameObject("Stardust",typeof(StardustApp),typeof(InputController),typeof(CellArt));
        StardustApp app = root.GetComponent<StardustApp>();
        InputController input = root.GetComponent<InputController>(); input.app = app;
        CellArt art = root.GetComponent<CellArt>(); FillArt(art); app.art = art;

        GameObject canvasObject = new GameObject("Canvas",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        UnityEngine.UI.CanvasScaler scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080,1920);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;

        BuildGame(app,input,art,canvasObject.transform);
        BuildMap(app,canvasObject.transform);
        BuildModal(app,canvasObject.transform);

        GameObject eventObject = new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
        eventObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        app.Preview();
        PlayerSettings.productName = "Stardust Atelier";
        PlayerSettings.defaultScreenWidth = 540; PlayerSettings.defaultScreenHeight = 960;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.resizableWindow = true;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        string scene = "Assets/Scenes/Stardust.unity";
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),scene);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scene,true) };
        AssetDatabase.SaveAssets();
    }

    // ---- 인게임 화면: 상단 HUD / 중앙 보드 / 하단 안내 ----
    private static void BuildGame(StardustApp app, InputController input, CellArt art, Transform canvas)
    {
        RectTransform screen = Stretch("GameScreen",canvas); app.gameScreen = screen.gameObject;
        Picture(Stretch("Background",screen),bgGame,Color.white,false);

        HudView hud = screen.gameObject.AddComponent<HudView>(); app.hud = hud; hud.art = art;
        hud.pause = IconButton("Pause",screen,-452,860,112,pause,panelLight);
        hud.title = Text("Level",screen,0,868,520,80,"레벨 1",46,Color.white,true);

        // 목표 패널: 목표 최대 3개, 레벨 데이터 순서대로 왼쪽부터(10.1).
        RectTransform goals = Panel("GoalPanel",screen,-125,690,650,240,panel,gold);
        Text("GoalLabel",goals,0,92,300,44,"목표",28,lavender,false);
        hud.goalImages = new UnityEngine.UI.Image[3]; hud.goalTexts = new TMP_Text[3]; hud.goalChecks = new UnityEngine.UI.Image[3];
        for (int i = 0; i < 3; i++)
        {
            RectTransform slot = Rect("Goal"+i,goals,-205+i*205,-14,190,190);
            hud.goalImages[i] = Picture(Rect("Icon",slot,0,22,112,112),art.gems[0],Color.white,false);
            hud.goalTexts[i] = Text("Count",slot,0,-62,180,64,"12",48,Color.white,true);
            hud.goalChecks[i] = Picture(Rect("Check",slot,44,-52,66,66),check,Color.white,false);
        }
        // 남은 이동: 5 이하이면 빨간 숫자(HudView.SetMoves).
        RectTransform movesPanel = Panel("MovesPanel",screen,345,690,280,240,panel,gold);
        Text("MovesLabel",movesPanel,0,92,240,44,"이동",28,lavender,false);
        hud.moves = Text("MovesValue",movesPanel,0,-12,260,140,"20",104,Color.white,true);

        // 보드: 금색 테두리 → 어두운 판 → 칸 바닥 층 → 블록 칸.
        RectTransform rim = Panel("BoardFrame",screen,0,30,1052,1052,new Color(0.12f,0.09f,0.22f,0.88f),gold);
        RectTransform boardRect = Rect("Board",rim,0,0,1000,1000);
        boardRect.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        BoardView board = boardRect.gameObject.AddComponent<BoardView>(); app.board = board; input.board = board; board.input = input; board.art = art;
        RectTransform floorLayer = Rect("FloorLayer",boardRect,0,0,1000,1000);
        board.cells = new CellView[81];
        for (int i = 0; i < board.cells.Length; i++) board.cells[i] = CreateCell(boardRect,floorLayer,i,input,art);

        // 안내 말풍선: 튜토리얼·셔플·스킬 안내·마지막 기회 문구(13.4, 9.3, 12.3, 10.3)를 표시합니다.
        RectTransform bubble = Panel("TipBubble",screen,0,-600,920,120,new Color(1f,0.97f,0.90f,0.97f),gold);
        hud.tipBubble = bubble.gameObject;
        hud.tip = Text("Tip",bubble,0,0,860,104,"",34,ink,false);
        bubble.gameObject.SetActive(false);
        // 마지막 기회의 [종료] 버튼: 말풍선 오른쪽에 겹쳐 둡니다.
        hud.endButton = Button("EndButton",screen,370,-600,170,92,"종료",new Color(0.85f,0.25f,0.32f),Color.white);
        hud.endButton.gameObject.SetActive(false);

        // 하단 파티 바(13.2): 원형 초상화 3칸과 게이지 링. 레벨 1~7은 숨깁니다.
        RectTransform partyRect = Rect("PartyBar",screen,0,-815,1000,260);
        PartyBar party = partyRect.gameObject.AddComponent<PartyBar>(); hud.party = party;
        party.slots = new PartySlotView[3];
        for (int i = 0; i < 3; i++) party.slots[i] = CreatePartySlot(partyRect,"Slot"+i,(i-1)*300,10,200,true);

        // 스킬 미리보기 패널(12.4): 스킬 이름·설명과 [발동] [취소].
        RectTransform skill = Panel("SkillPanel",screen,0,-600,1000,190,new Color(0.19f,0.14f,0.37f,1f),gold);
        hud.skillPanel = skill.gameObject;
        hud.skillName = Text("Name",skill,-150,50,640,56,"",36,gold,true);
        hud.skillDesc = Text("Desc",skill,-150,-28,640,100,"",30,Color.white,false);
        hud.skillName.alignment = TextAlignmentOptions.Left; hud.skillDesc.alignment = TextAlignmentOptions.Left;
        hud.skillUse = Button("Use",skill,340,36,240,80,"발동",gold,ink);
        hud.skillCancel = Button("Cancel",skill,340,-52,240,72,"취소",panelLight,Color.white);
        skill.gameObject.SetActive(false);

        hud.combo = Text("Combo",screen,0,120,900,160,"2 연쇄!",110,gold,true);
        hud.combo.gameObject.SetActive(false);
        BuildIntroBanner(app,art,screen);
        RectTransform clear = Stretch("ClearBanner",screen); app.clearBanner = clear.gameObject;
        Picture(Rect("Glow",clear,0,60,1200,700),glow,new Color(1f,0.85f,0.45f,0.55f),false);
        Text("Label",clear,0,60,1000,220,"클리어!",160,gold,true);
        clear.gameObject.SetActive(false);
    }
    private static void BuildIntroBanner(StardustApp app, CellArt art, Transform screen)
    {
        // 목표 안내 배너(7.3): 화면을 탭하면 건너뜁니다. 덮개 전체가 버튼입니다.
        RectTransform veil = Stretch("IntroBanner",screen); app.introBanner = veil.gameObject;
        UnityEngine.UI.Image dim = Picture(veil,null,new Color(0.03f,0.02f,0.07f,0.55f),true);
        app.introSkip = veil.gameObject.AddComponent<UnityEngine.UI.Button>(); app.introSkip.targetGraphic = dim; app.introSkip.transition = UnityEngine.UI.Selectable.Transition.None;
        RectTransform ribbon = Panel("Ribbon",veil,0,40,1120,560,new Color(0.19f,0.13f,0.38f,0.97f),gold);
        app.introTitle = Text("Title",ribbon,0,200,900,110,"레벨 1",84,gold,true);
        Text("GoalLabel",ribbon,0,118,600,50,"목표",30,lavender,false);
        app.introGoalImages = new UnityEngine.UI.Image[3]; app.introGoalTexts = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        {
            RectTransform slot = Rect("Goal"+i,ribbon,-240+i*240,-20,220,220);
            app.introGoalImages[i] = Picture(Rect("Icon",slot,0,20,150,150),art.gems[0],Color.white,false);
            app.introGoalTexts[i] = Text("Count",slot,0,-82,200,70,"12",56,Color.white,true);
        }
        app.introMoves = Text("Moves",ribbon,0,-205,900,60,"20번 안에 모아요",36,lavender,false);
        veil.gameObject.SetActive(false);
    }
    // 파티 슬롯 하나. gauge가 true면 인게임용(게이지 링·빛남·잠금), false면 진입 팝업용(초상화와 빈 칸 표시만)입니다.
    private static PartySlotView CreatePartySlot(Transform parent, string name, float x, float y, float size, bool gauge)
    {
        RectTransform root = Rect(name,parent,x,y,size+40,size+40);
        PartySlotView view = root.gameObject.AddComponent<PartySlotView>();
        Picture(root,null,new Color(0,0,0,0),true); // 입력을 받는 투명 판
        view.button = root.gameObject.AddComponent<UnityEngine.UI.Button>();
        view.button.transition = UnityEngine.UI.Selectable.Transition.None;
        RectTransform glowRect = Rect("Glow",root,0,0,size*1.55f,size*1.55f); view.glow = glowRect.gameObject;
        Picture(glowRect,glow,new Color(1f,0.85f,0.40f,0.9f),false); glowRect.gameObject.SetActive(false);
        RectTransform selectedRect = Rect("Selected",root,0,0,size*1.22f,size*1.22f); view.selected = selectedRect.gameObject;
        Picture(selectedRect,ring,Color.white,false); selectedRect.gameObject.SetActive(false);
        view.gaugeBack = Picture(Rect("GaugeBack",root,0,0,size*1.12f,size*1.12f),ring,new Color(0.10f,0.07f,0.20f,0.9f),false);
        view.gauge = Picture(Rect("Gauge",root,0,0,size*1.12f,size*1.12f),ring,gold,false);
        view.gauge.type = UnityEngine.UI.Image.Type.Filled; view.gauge.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
        view.gauge.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top; view.gauge.fillClockwise = true; view.gauge.fillAmount = 0;
        if (!gauge) { view.gaugeBack.gameObject.SetActive(false); view.gauge.gameObject.SetActive(false); }
        // 원형 안에 초상화를 잘라 넣습니다(Mask). 빈 슬롯이면 "+"가 보입니다.
        RectTransform face = Rect("Face",root,0,0,size,size);
        Picture(face,circle,new Color(0.15f,0.11f,0.30f,1),false);
        face.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = true;
        view.empty = Text("Empty",face,0,4,size,size,"+",size*0.45f,new Color(0.65f,0.60f,0.85f),false).gameObject;
        view.portrait = Picture(Stretch("Portrait",face),null,Color.white,false); view.portrait.preserveAspect = true;
        RectTransform lockRect = Stretch("Lock",root); view.lockGroup = lockRect.gameObject;
        Picture(Rect("Dim",lockRect,0,0,size,size),circle,new Color(0.08f,0.06f,0.16f,0.92f),false);
        Picture(Rect("Icon",lockRect,0,18,size*0.32f,size*0.32f),lockSprite,new Color(0.75f,0.72f,0.88f),false);
        view.lockText = Text("Level",lockRect,0,-size*0.25f,size,40,"레벨 9",26,lavender,false);
        lockRect.gameObject.SetActive(false);
        return view;
    }
    private static CellView CreateCell(Transform board, Transform floorLayer, int index, InputController input, CellArt art)
    {
        // 바닥(체크무늬)과 얼음은 아래 층에, 블록·사슬·선택 빛은 칸 오브젝트에 둡니다.
        RectTransform floor = Rect("Floor"+index.ToString("00"),floorLayer,0,0,140,140);
        UnityEngine.UI.Image floorImage = Picture(floor,rounded,Color.white,false); floorImage.type = UnityEngine.UI.Image.Type.Sliced;
        UnityEngine.UI.Image ice = Picture(Inset("Ice",floor,6),art.ice1,Color.white,false);

        RectTransform cell = Rect("Cell"+index.ToString("00"),board,0,0,140,140);
        CellView view = cell.gameObject.AddComponent<CellView>(); view.input = input; view.art = art;
        Picture(cell,null,new Color(0,0,0,0),true); // 입력을 받는 투명 판
        view.floor = floorImage; view.ice = ice;
        view.highlight = Picture(Inset("Highlight",cell,-18),glow,Color.white,false); view.highlight.enabled = false;
        RectTransform tile = Inset("Tile",cell,5); view.tile = tile;
        view.gem = Picture(Stretch("Gem",tile),art.gems[0],Color.white,false); view.gem.preserveAspect = true;
        view.overlay = Picture(Inset("Overlay",tile,22),art.rocketH,Color.white,false); view.overlay.preserveAspect = true;
        view.chain = Picture(Inset("Chain",cell,0),art.chain,Color.white,false); view.chain.gameObject.SetActive(false);
        return view;
    }

    // ---- 레벨 맵: 아래에서 위로 1~30 ----
    private const float NodeGap = 270, NodeBase = 260;
    private static void BuildMap(StardustApp app, Transform canvas)
    {
        RectTransform screen = Stretch("MapScreen",canvas);
        MapView map = screen.gameObject.AddComponent<MapView>(); app.map = map;
        Picture(Stretch("Background",screen),bgMap,Color.white,false);

        RectTransform viewport = Rect("Viewport",screen,0,-90,1080,1740);
        Picture(viewport,null,new Color(0,0,0,0),true);
        viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        RectTransform content = Rect("Content",viewport,0,0,1080,NodeBase*2+NodeGap*29);
        content.anchorMin = new Vector2(0,0); content.anchorMax = new Vector2(1,0); content.pivot = new Vector2(0.5f,0);
        content.anchoredPosition = Vector2.zero; content.sizeDelta = new Vector2(0,NodeBase*2+NodeGap*29);
        UnityEngine.UI.ScrollRect scroll = screen.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40;
        map.scroll = scroll;

        // 길 점은 노드보다 아래 층에 두어 노드 위에 겹쳐 보이지 않게 합니다.
        RectTransform path = Stretch("Path",content);
        for (int i = 1; i < 30; i++)
            foreach (float t in new[] { 0.36f, 0.5f, 0.64f })
            {
                Vector2 dot = Vector2.Lerp(NodePosition(i-1),NodePosition(i),t);
                RectTransform d = Rect("Dot"+i+"_"+t,path,0,0,22,22); d.anchorMin = d.anchorMax = new Vector2(0.5f,0);
                d.anchoredPosition = dot; Picture(d,circle,new Color(1f,0.92f,0.75f,0.40f),false);
            }
        map.nodes = new MapNode[30];
        for (int i = 0; i < 30; i++) map.nodes[i] = CreateNode(content,i,NodePosition(i));
        // 상단 제목은 스크롤 위에 고정합니다. 최종 로고 그림으로 바꿀 자리입니다.
        RectTransform header = Panel("Header",screen,0,862,1120,200,new Color(0.10f,0.07f,0.22f,0.92f),gold);
        Text("Eyebrow",header,0,52,900,40,"S T A R D U S T   A T E L I E R",24,gold,false);
        Text("Title",header,0,-12,900,100,"별가루 아틀리에",70,Color.white,true);
        screen.gameObject.SetActive(false);
    }
    private static Vector2 NodePosition(int index)
    { return new Vector2(Mathf.Sin(index*0.85f)*270,NodeBase+index*NodeGap); }
    private static MapNode CreateNode(Transform content, int index, Vector2 position)
    {
        RectTransform root = Rect("Level"+(index+1).ToString("00"),content,0,0,240,240);
        root.anchorMin = root.anchorMax = new Vector2(0.5f,0); root.anchoredPosition = position;
        MapNode node = root.gameObject.AddComponent<MapNode>();
        RectTransform glowRect = Rect("CurrentGlow",root,0,0,330,330); node.currentGlow = glowRect.gameObject;
        Picture(glowRect,glow,new Color(1f,0.85f,0.40f,0.75f),false);
        node.ring = Picture(Rect("Ring",root,0,0,196,196),circle,gold,false);
        RectTransform faceRect = Rect("Face",root,0,0,170,170);
        node.face = Picture(faceRect,circle,gold,true);
        node.button = faceRect.gameObject.AddComponent<UnityEngine.UI.Button>(); node.button.targetGraphic = node.face;
        UnityEngine.UI.ColorBlock colors = node.button.colors; colors.disabledColor = Color.white; node.button.colors = colors;
        node.number = Text("Number",faceRect,0,-4,160,110,(index+1).ToString(),70,ink,true);
        node.lockIcon = Picture(Rect("Lock",faceRect,0,0,76,76),lockSprite,new Color(0.75f,0.72f,0.88f),false);
        node.stars = new UnityEngine.UI.Image[3];
        for (int s = 0; s < 3; s++)
            node.stars[s] = Picture(Rect("Star"+s,root,(s-1)*58,s == 1 ? -106 : -96,s == 1 ? 64 : 54,s == 1 ? 64 : 54),star,Color.white,false);
        return node;
    }

    // ---- 팝업: 진입·일시정지·확인·결과·튜토리얼을 한 카드로 씁니다 ----
    private static void BuildModal(StardustApp app, Transform canvas)
    {
        RectTransform veil = Stretch("Modal",canvas); Picture(veil,null,new Color(0.02f,0.01f,0.05f,0.78f),true); app.modal = veil.gameObject;
        RectTransform card = Panel("Card",veil,0,-20,900,1000,new Color(0.19f,0.14f,0.37f,1f),gold); app.popupCard = card;
        RectTransform ribbon = Panel("Ribbon",card,0,500,720,150,gold,new Color(1f,0.95f,0.80f));
        app.popupTitle = Text("Heading",ribbon,0,4,680,120,"",58,ink,false);
        app.popupTitle.fontStyle = FontStyles.Bold;

        RectTransform starRow = Rect("StarRow",card,0,260,700,220); app.popupStarRow = starRow.gameObject;
        app.popupStars = new UnityEngine.UI.Image[3];
        for (int i = 0; i < 3; i++)
            app.popupStars[i] = Picture(Rect("Star"+i,starRow,(i-1)*210,i == 1 ? 24 : -10,i == 1 ? 200 : 168,i == 1 ? 200 : 168),star,Color.white,false);
        RectTransform goalRow = Rect("GoalRow",card,0,250,700,220); app.popupGoalRow = goalRow.gameObject;
        app.popupGoalImages = new UnityEngine.UI.Image[3]; app.popupGoalTexts = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        {
            RectTransform slot = Rect("Goal"+i,goalRow,-220+i*220,0,200,220);
            app.popupGoalImages[i] = Picture(Rect("Icon",slot,0,30,130,130),StardustArtGenerator.Load("gem_R"),Color.white,false);
            app.popupGoalTexts[i] = Text("Count",slot,0,-72,190,64,"12",48,Color.white,true);
        }
        RectTransform picture = Rect("Picture",card,0,250,240,240); app.popupPicture = picture.gameObject;
        Picture(Rect("Glow",picture,0,0,380,380),glow,new Color(1f,0.85f,0.45f,0.5f),false);
        // 튜토리얼 그림(아이템·장애물)과 합류 팝업의 초상화를 같은 자리에 씁니다.
        app.popupPictureImage = Picture(Rect("Image",picture,0,0,230,230),StardustArtGenerator.Load("special_bomb"),Color.white,false);
        app.popupPictureImage.preserveAspect = true;

        app.popupBody = Text("Body",card,0,60,800,150,"",38,Color.white,false);
        // 레벨 진입 팝업의 파티 영역: 추천 속성(보석 아이콘)과 파티 슬롯(누르면 캐릭터 고르기).
        RectTransform partyRow = Rect("PartyRow",card,0,-100,800,300); app.popupPartyRow = partyRow.gameObject;
        Text("RecommendLabel",partyRow,-150,112,200,50,"추천",30,lavender,false);
        app.popupRecommended = new UnityEngine.UI.Image[3];
        for (int i = 0; i < 3; i++)
        { app.popupRecommended[i] = Picture(Rect("Gem"+i,partyRow,-40+i*70,112,58,58),StardustArtGenerator.Load("gem_G"),Color.white,false); app.popupRecommended[i].preserveAspect = true; }
        app.popupPartySlots = new PartySlotView[3];
        for (int i = 0; i < 3; i++) app.popupPartySlots[i] = CreatePartySlot(partyRow,"Slot"+i,(i-1)*200,-30,160,false);
        partyRow.gameObject.SetActive(false);

        app.primary = Button("Primary",card,0,-150,640,116,"",gold,ink);
        app.secondary = Button("Secondary",card,0,-285,640,104,"",panelLight,Color.white);
        app.tertiary = Button("Tertiary",card,0,-405,640,104,"",panelLight,Color.white);
        app.primaryText = app.primary.GetComponentInChildren<TMP_Text>();
        app.secondaryText = app.secondary.GetComponentInChildren<TMP_Text>();
        app.tertiaryText = app.tertiary.GetComponentInChildren<TMP_Text>();

        // 캐릭터 고르기 패널: 카드 위에 겹쳐 뜹니다. 보유 캐릭터(최대 5명) + [비우기] [닫기].
        RectTransform picker = Panel("Picker",veil,0,-20,940,480,new Color(0.14f,0.10f,0.30f,1f),gold); app.picker = picker.gameObject;
        Picture(Stretch("Block",picker),null,new Color(0,0,0,0),true).transform.SetAsFirstSibling(); // 아래 카드 입력 막기
        Text("Title",picker,0,180,800,60,"캐릭터 선택",42,Color.white,true);
        app.pickerButtons = new UnityEngine.UI.Button[5]; app.pickerFaces = new UnityEngine.UI.Image[5];
        for (int i = 0; i < 5; i++)
        {
            PartySlotView choice = CreatePartySlot(picker,"Choice"+i,(i-2)*176,30,140,false);
            app.pickerButtons[i] = choice.button; app.pickerFaces[i] = choice.portrait;
            choice.portrait.gameObject.SetActive(true); choice.empty.SetActive(false);
        }
        app.pickerClear = Button("Clear",picker,-150,-160,260,90,"비우기",panelLight,Color.white);
        app.pickerClose = Button("Close",picker,150,-160,260,90,"닫기",gold,ink);
        picker.gameObject.SetActive(false);
        veil.gameObject.SetActive(false);
    }

    // ---- 도우미 ----
    private static void FillArt(CellArt art)
    {
        string[] codes = { "R", "B", "G", "Y", "P" };
        for (int i = 0; i < 5; i++) art.gems[i] = StardustArtGenerator.Load("gem_"+codes[i]);
        art.rocketH = StardustArtGenerator.Load("special_rocket_h"); art.rocketV = StardustArtGenerator.Load("special_rocket_v");
        art.bomb = StardustArtGenerator.Load("special_bomb"); art.meteor = StardustArtGenerator.Load("special_meteor");
        art.prism = StardustArtGenerator.Load("special_prism");
        art.box = StardustArtGenerator.Load("obstacle_box"); art.vine = StardustArtGenerator.Load("obstacle_vine");
        art.relic = StardustArtGenerator.Load("obstacle_relic"); art.chain = StardustArtGenerator.Load("obstacle_chain");
        art.ice1 = StardustArtGenerator.Load("ice_1"); art.ice2 = StardustArtGenerator.Load("ice_2");
        art.check = check;
        // 캐릭터 그림(Assets/Art/Characters). 순서는 ID 105~109입니다.
        for (int i = 0; i < 5; i++)
        {
            art.portraits[i] = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Characters/char_"+(105+i)+"_portrait.png");
            art.fullBodies[i] = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Characters/char_"+(105+i)+"_full.png");
        }
    }
    private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        RectTransform rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent,false); rect.anchorMin = rect.anchorMax = new Vector2(0.5f,0.5f);
        rect.anchoredPosition = new Vector2(x,y); rect.sizeDelta = new Vector2(width,height);
        return rect;
    }
    private static RectTransform Stretch(string name, Transform parent) { return Inset(name,parent,0); }
    // 부모를 꽉 채우되 사방으로 inset만큼 들어갑니다(음수면 바깥으로 커집니다).
    private static RectTransform Inset(string name, Transform parent, float inset)
    {
        RectTransform rect = Rect(name,parent,0,0,0,0);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = new Vector2(-inset*2,-inset*2);
        return rect;
    }
    private static UnityEngine.UI.Image Picture(RectTransform rect, Sprite sprite, Color color, bool raycast)
    {
        UnityEngine.UI.Image image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = sprite; image.color = color; image.raycastTarget = raycast;
        return image;
    }
    // 둥근 패널: 테두리 색 판 위에 4px 안쪽으로 본 판을 겹칩니다.
    private static RectTransform Panel(string name, Transform parent, float x, float y, float width, float height, Color fill, Color border)
    {
        RectTransform rect = Rect(name,parent,x,y,width,height);
        UnityEngine.UI.Image edge = Picture(rect,rounded,border,false); edge.type = UnityEngine.UI.Image.Type.Sliced;
        // 본 판은 불투명하게 칠합니다. 반투명하면 아래 테두리 색이 비쳐 탁해집니다.
        fill.a = 1;
        UnityEngine.UI.Image body = Picture(Inset("Fill",rect,5),rounded,fill,false); body.type = UnityEngine.UI.Image.Type.Sliced;
        return rect;
    }
    private static TMP_Text Text(string name, Transform parent, float x, float y, float width, float height, string value, float size, Color color, bool heavy)
    {
        TextMeshProUGUI text = Rect(name,parent,x,y,width,height).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = size; text.color = color; text.text = value;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal;
        // 큰 숫자·제목은 진한 외곽선 재질과 굵게로 상용 게임처럼 또렷하게 보이게 합니다.
        if (heavy) { text.fontSharedMaterial = outlined; text.fontStyle = FontStyles.Bold; }
        return text;
    }
    private static UnityEngine.UI.Button Button(string name, Transform parent, float x, float y, float width, float height, string caption, Color background, Color foreground)
    {
        // 버튼 오브젝트는 입력만 받는 투명 판이고, 그 안에 그림자 → 면 → 글자 순으로 쌓습니다.
        // 면 위 반사광 띠는 사용자 요청으로 넣지 않습니다(2026-10-10).
        // 그림자를 아래로 깔아 눌리는 버튼처럼 보이게 하고, 버튼을 숨기면 모두 함께 숨습니다.
        RectTransform rect = Rect(name,parent,x,y,width,height);
        Picture(rect,null,new Color(0,0,0,0),true);
        RectTransform shadow = Rect("Shadow",rect,0,-10,width,height);
        Picture(shadow,rounded,new Color(background.r*0.45f,background.g*0.45f,background.b*0.45f,1),false).type = UnityEngine.UI.Image.Type.Sliced;
        UnityEngine.UI.Image face = Picture(Stretch("Face",rect),rounded,background,false); face.type = UnityEngine.UI.Image.Type.Sliced;
        UnityEngine.UI.Button button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = face;
        UnityEngine.UI.ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.08f,1.08f,1.08f); colors.pressedColor = new Color(0.82f,0.82f,0.82f);
        colors.disabledColor = new Color(0.5f,0.5f,0.5f,0.7f); button.colors = colors;
        TMP_Text label = Text("Label",rect,0,2,width-40,height-10,caption,40,foreground,false);
        label.fontStyle = FontStyles.Bold;
        return button;
    }
    private static UnityEngine.UI.Button IconButton(string name, Transform parent, float x, float y, float size, Sprite icon, Color background)
    {
        RectTransform rect = Rect(name,parent,x,y,size,size);
        UnityEngine.UI.Image face = Picture(rect,circle,background,true);
        Picture(Inset("Rim",rect,-4),ring,gold,false).transform.SetAsFirstSibling();
        Picture(Inset("Icon",rect,size*0.28f),icon,Color.white,false);
        UnityEngine.UI.Button button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = face;
        UnityEngine.UI.ColorBlock colors = button.colors; colors.disabledColor = new Color(0.6f,0.6f,0.6f,0.6f); button.colors = colors;
        return button;
    }
    private static void PrepareAssets()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR.asset");
        if (font == null)
        {
            Font source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansKR-Regular.otf");
            font = TMP_FontAsset.CreateFontAsset(source,64,8,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
            font.name = "Noto Sans KR"; AssetDatabase.CreateAsset(font,"Assets/Fonts/NotoSansKR.asset");
            foreach (Texture2D atlas in font.atlasTextures) if (atlas != null) AssetDatabase.AddObjectToAsset(atlas,font);
            AssetDatabase.AddObjectToAsset(font.material,font);
        }
        const string outlinePath = "Assets/Fonts/NotoSansKR Outline.mat";
        outlined = AssetDatabase.LoadAssetAtPath<Material>(outlinePath);
        if (outlined == null)
        {
            outlined = new Material(font.material) { name = "NotoSansKR Outline" };
            AssetDatabase.CreateAsset(outlined,outlinePath);
        }
        // 외곽선과 아래 그림자. 값을 바꾸면 다음 Build 때 반영됩니다.
        outlined.shader = font.material.shader;
        outlined.SetTexture("_MainTex",font.material.GetTexture("_MainTex"));
        outlined.EnableKeyword("OUTLINE_ON"); outlined.SetFloat("_OutlineWidth",0.22f); outlined.SetColor("_OutlineColor",ink);
        outlined.EnableKeyword("UNDERLAY_ON"); outlined.SetColor("_UnderlayColor",new Color(0,0,0,0.55f));
        outlined.SetFloat("_UnderlayOffsetY",-0.7f); outlined.SetFloat("_UnderlaySoftness",0.25f);
        outlined.SetFloat("_FaceDilate",0.12f);
        EditorUtility.SetDirty(outlined);

        rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Rounded.png");
        if (rounded == null)
        {
            Texture2D texture = new Texture2D(64,64,TextureFormat.RGBA32,false);
            Color[] pixels = new Color[4096];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float dx = Mathf.Max(14-x,x-49,0), dy = Mathf.Max(14-y,y-49,0);
                    pixels[y*64+x] = new Color(1,1,1,Mathf.Clamp01(14.5f-Mathf.Sqrt(dx*dx+dy*dy)));
                }
            texture.SetPixels(pixels); texture.Apply();
            File.WriteAllBytes("Assets/UI/Rounded.png",texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset("Assets/UI/Rounded.png");
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath("Assets/UI/Rounded.png");
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.spriteBorder = new Vector4(16,16,16,16);
            importer.SaveAndReimport(); rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Rounded.png");
        }
        circle = StardustArtGenerator.Load("ui_circle"); ring = StardustArtGenerator.Load("ui_ring");
        glow = StardustArtGenerator.Load("ui_glow"); star = StardustArtGenerator.Load("ui_star");
        lockSprite = StardustArtGenerator.Load("ui_lock"); pause = StardustArtGenerator.Load("ui_pause");
        check = StardustArtGenerator.Load("ui_check");
        bgGame = StardustArtGenerator.Load("bg_game"); bgMap = StardustArtGenerator.Load("bg_map");
    }
}
