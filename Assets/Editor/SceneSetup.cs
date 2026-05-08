using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// Claw Crew > Build Main Scene
///
/// 룸/기계는 프로시저럴 베이스로 생성하고,
/// 소품은 Kenney/오픈소스 FBX를 우선 배치(실패 시 프로시저럴 fallback)한다.
/// PBR 텍스처(Poly Haven/오픈소스)를 벽/바닥/천장/기계에 적용
///
/// scene_background_research.md 3.2절 조감도:
///   뒷벽(Z+2.5): 창문+대형 점수판, 선반(좌)
///   중앙(Z+0.3): 인형뽑기 기계
///   플레이어(Z-1.0): XR Origin, 러그
///   앞(Z-1.5): 스툴(좌), 사이드테이블+컵(우)
/// </summary>
public static class SceneSetup
{
    const string SceneDir = "Assets/00.Main/Scenes/Main";
    const string ScenePath = "Assets/00.Main/Scenes/Main/MainScene.unity";
    // 머티리얼 경로: 카테고리별 분리
    const string RM = "Assets/00.Main/Art/Room/Materials";            // 방 구조 (벽/바닥/천장)
    const string CM = "Assets/00.Main/Art/ClawMachine/Materials";     // 인형뽑기 기계
    const string FM = "Assets/00.Main/Art/Props/Furniture/Materials"; // 가구 (의자/테이블/선반)
    const string PM = "Assets/00.Main/Art/Props/Plant/Materials";     // 식물
    const string DM = "Assets/00.Main/Art/Props/Decoration/Materials"; // 장식 (시계/포스터/네온/러그/조명/꽃병)
    // 텍스처 경로
    const string RT = "Assets/00.Main/Art/Room/Textures";
    const string CT = "Assets/00.Main/Art/ClawMachine/Textures";
    const string PT = "Assets/00.Main/Art/Props/Plant/Textures";
    // Changed: PR용 씬 생성은 00.Main/Art 하위 curated 에셋만 참조.
    // Why: 원본 대형 에셋 팩(polyperfect/Furniture Mega Pack/Yughues/RunnerPackage)을 GitHub PR에 포함하지 않기 위함.
    // 모델 경로 (오픈소스 CC0/Kenney + MIT 참고 에셋)
    const string MModel = "Assets/00.Main/Art/ClawMachine/Models";
    const string FModel = "Assets/00.Main/Art/Props/Furniture/Models";
    const string PModel = "Assets/00.Main/Art/Props/Plant/Models";
    const string DModel = "Assets/00.Main/Art/Props/Decoration/Models";

    // Changed: scene_background_research.md 권장 비율(4m x 4m x 2.8m)로 방 크기 조정
    const float RW = 4f, RD = 4f, RH = 2.8f, WT = 0.1f;
    const float MW = 0.78f, MD = 0.78f, MH = 2f, BH = 0.15f, TH = 0.25f, FT = 0.04f;
    // Changed: 일반 인형뽑기 기계처럼 플레이 필드를 중상단으로 올리기 위한 기준 비율
    const float PrizeDeckRatio = 0.62f;
    // Changed: 기계-플레이어-러그 중심축 정렬을 위해 기준 좌표 명시
    static readonly Vector3 PlayerPos = new(0, 0, -1.05f);
    static readonly Vector3 MPos = new(0, 0, 0.1f);

    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
    // Changed: 저명도/고대비를 줄이고 파스텔-웜 톤 중심으로 팔레트 재정의
    static readonly Color CWall=Hex("DEE8E0"), CFloor=Hex("EDE2D3"), CCeil=Hex("FAF7F1");
    static readonly Color CWood=Hex("C1A689"), CAccent=Hex("D7C6B5");
    static readonly Color CFrame=Hex("F5F1EA"), CMetal=Hex("D6D4D8"), CLed=Hex("FFDAB8");

    [MenuItem("Claw Crew/Build Main Scene", false, 1)]
    public static void Build()
    {
        DeleteAllMats();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BuildRoom();
        BuildMachine();
        BuildLighting();
        BuildDolls();
        BuildXROrigin();
        BuildUI();
        if (!Directory.Exists(SceneDir)) Directory.CreateDirectory(SceneDir);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuild(ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[CatchYourMood] Build 완료");
    }

    [MenuItem("Claw Crew/Open Main Scene", false, 2)]
    public static void Open()
    {
        if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
        else Debug.LogWarning("MainScene 없음");
    }

    // =========================== 방 ===========================
    static void BuildRoom()
    {
        var R = new GameObject("Room");
        // Changed: 심리검사 맥락에 맞춰 저채도 파스텔 + 낮은 노이즈 재질로 통일
        var mW = PBR(RM,"M_Wall",CWall,.08f, $"{RT}/WallPlaster_Diff.jpg",$"{RT}/WallPlaster_Normal.png",$"{RT}/WallPlaster_Rough.jpg");
        // Changed: 바닥 결이 벽과 충돌하는 문제를 줄이기 위해 저대비 단색 바닥으로 전환.
        // Why: 사용자 피드백(편안한 모티프 저해)을 반영해 시각 자극을 낮추기 위함.
        var mF = Mat(RM,"M_Floor",Hex("DCCFBE"),.04f);
        var mC = PBR(RM,"M_Ceil",CCeil,.04f, $"{RT}/Ceiling_Diff.jpg",$"{RT}/Ceiling_Normal.jpg",$"{RT}/Ceiling_Rough.jpg");
        TunePBR(mW, .14f, .42f);
        TunePBR(mF, .04f, .0f);
        TunePBR(mC, .1f, .55f);
        var mWd = Mat(FM,"M_Wood",CWood,.18f);
        var mLeaf = Mat(PM,"M_Leaf",Hex("7FA98A"),.08f);
        var mPot = Mat(PM,"M_Pot",Hex("D8B79F"),.12f);

        // --- 구조: 바닥/천장/벽 ---
        Box("Floor", R, V(0,-WT/2,0), V(RW,WT,RD), mF);
        Box("Ceiling", R, V(0,RH+WT/2,0), V(RW,WT,RD), mC);
        Box("Wall_Back", R, V(0,RH/2,RD/2+WT/2), V(RW+WT*2,RH,WT), mW);
        Box("Wall_Front", R, V(0,RH/2,-RD/2-WT/2), V(RW+WT*2,RH,WT), mW);
        Box("Wall_Left", R, V(-RW/2-WT/2,RH/2,0), V(WT,RH,RD), mW);
        Box("Wall_Right", R, V(RW/2+WT/2,RH/2,0), V(WT,RH,RD), mW);

        // 걸레받이
        float bh=.08f;
        Box("Base_B",R,V(0,bh/2,RD/2-.005f),V(RW,bh,.01f),mWd);
        Box("Base_F",R,V(0,bh/2,-RD/2+.005f),V(RW,bh,.01f),mWd);
        Box("Base_L",R,V(-RW/2+.005f,bh/2,0),V(.01f,bh,RD),mWd);
        Box("Base_R",R,V(RW/2-.005f,bh/2,0),V(.01f,bh,RD),mWd);

        // 코너 몰딩
        var mMd=Mat(RM,"M_Mold",CWall*.98f,.08f);
        Cyl("Mold_BL",R,V(-RW/2,RH/2,RD/2),Q0,V(.045f,RH/2,.045f),mMd);
        Cyl("Mold_BR",R,V(RW/2,RH/2,RD/2),Q0,V(.045f,RH/2,.045f),mMd);
        Cyl("Mold_FL",R,V(-RW/2,RH/2,-RD/2),Q0,V(.045f,RH/2,.045f),mMd);
        Cyl("Mold_FR",R,V(RW/2,RH/2,-RD/2),Q0,V(.045f,RH/2,.045f),mMd);

        // =========================
        //  뒷벽: 대형 점수판(단일)
        // =========================
        // Changed: 창문/커튼 대체를 위해 점수판만 단일 배치.
        // Why: 창문 + 점수판 동시 노출로 "점수판이 2개처럼 보이는" 문제를 제거.
        BuildScoreboard(R, V(0f, 1.56f, RD/2-.05f), mWd);

        // Changed: 상단 브랜딩 플레이트 제거.
        // Why: 창문 위 흰 막대가 불필요한 시각 노이즈로 보인다는 피드백 반영.

        // =========================
        //  좌측 후면: 선반 + 책 + 진열 인형
        // =========================
        // Changed: 선반은 단일 프리팹 우선 + 목표 치수 보정으로 크기/정렬 재현성을 고정.
        // Why: 서로 다른 프리팹 단위/피벗 차이로 선반 위치가 반복적으로 깨지는 문제를 방지.
        float sx=-RW/2+.36f, sZ=RD/2-.48f;
        // Changed: 대형 원본 팩 대신 00.Main/Art/Props/Furniture/Models의 curated 모델만 사용.
        // Why: PR에 필요한 모델만 남기고 외부 에셋 팩 직접 참조를 제거.
        if (!PlaceModel(R, "ShelfModel", $"{FModel}/Shelf.fbx", V(sx, 0f, sZ), Quaternion.Euler(0, 180, 0), .86f, mWd, recenterXZ: true, snapToFloor: true, targetWorldSize: V(.72f,1.34f,.32f)))
        {
            Box("Shelf_Side_L",R,V(sx-.31f,.75f,sZ),V(.02f,1.2f,.25f),mWd);
            Box("Shelf_Side_R",R,V(sx+.31f,.75f,sZ),V(.02f,1.2f,.25f),mWd);
            Box("Shelf_Bot",R,V(sx,.3f,sZ),V(.6f,.02f,.25f),mWd);
            Box("Shelf_Mid",R,V(sx,.7f,sZ),V(.6f,.02f,.25f),mWd);
            Box("Shelf_Top",R,V(sx,1.1f,sZ),V(.6f,.02f,.25f),mWd);
            Box("Shelf_Back",R,V(sx,.7f,sZ-.12f),V(.6f,1.2f,.01f),mWd);
        }
        PlaceModel(R, "ShelfBooks", $"{FModel}/Books.fbx", V(sx-.02f,.72f,sZ+.02f), Quaternion.Euler(0, 160, 0), .42f, mWd);
        Sph("DDoll1",R,V(sx-.12f,.79f,sZ+.05f),V(.07f,.085f,.07f),Mat(CM,"M_DD1",Hex("BDD9CC"),.05f));
        Sph("DDoll2",R,V(sx+.11f,1.19f,sZ+.04f),V(.06f,.075f,.06f),Mat(CM,"M_DD2",Hex("EBC2B4"),.05f));

        // =========================
        //  식물: 좌우 후면 코너 (과밀 배치 제거)
        // =========================
        // Changed: 식물 수/스케일/위치를 정리해 동선을 막지 않도록 배치
        BuildPlant(R,"Plant_BR",V(RW/2-.32f,0,RD/2-.56f),mPot,mLeaf);
        BuildPlant(R,"Plant_BL",V(-RW/2+.34f,0,RD/2-.92f),mPot,mLeaf);

        // =========================
        //  플레이어 축: 러그 중심 정렬
        // =========================
        // Changed: 러그 중심을 기계 중심으로 고정해 정렬 불일치 문제를 제거
        var mRug = Mat(DM,"M_Rug",Hex("E3BFB2"),.04f);
        var rugPos = V(MPos.x, .001f, MPos.z);
        Cyl("Rug",R,rugPos,Q0,V(1.02f,.005f,1.02f),mRug);

        // =========================
        //  전면 좌우: 스툴 + 사이드테이블 + 램프
        // =========================
        // Changed: 좌측 좌석은 소파형으로 오인되는 의자를 제외하고, 목표 치수로 강제 보정.
        // Why: 가구가 방의 과도한 면적을 차지하는 문제와 방향 불일치를 함께 해결.
        // Changed: 스툴도 00.Main/Art 하위 curated 모델만 사용.
        // Why: Furniture Mega Pack/polyperfect 원본 폴더 없이도 씬 재생성이 가능해야 함.
        if (!PlaceModel(R, "StoolModel", $"{FModel}/Stool.fbx", V(-.92f, 0f, -1.24f), Quaternion.Euler(0, 22f, 0), .9f, mWd, recenterXZ: true, snapToFloor: true, targetWorldSize: V(.48f,.78f,.52f)))
        {
            var stool=new GameObject("Stool"); stool.transform.SetParent(R.transform);
            stool.transform.localPosition=V(-.92f,0,-1.24f);
            stool.transform.localRotation=Quaternion.Euler(0,22f,0);
            Cyl("Seat",stool,V(0,.58f,0),Q0,V(.28f,.02f,.28f),mWd);
            Cyl("SLeg0",stool,V(.09f,.28f,.09f),Q0,V(.022f,.28f,.022f),mWd);
            Cyl("SLeg1",stool,V(-.09f,.28f,.09f),Q0,V(.022f,.28f,.022f),mWd);
            Cyl("SLeg2",stool,V(.09f,.28f,-.09f),Q0,V(.022f,.28f,.022f),mWd);
            Cyl("SLeg3",stool,V(-.09f,.28f,-.09f),Q0,V(.022f,.28f,.022f),mWd);
        }
        // Changed: 우측 테이블은 과대 모델을 방지하기 위해 목표 치수 기반 스케일링을 적용.
        // Why: 테이블이 방 시야의 1/4 이상을 차지하는 반복 문제를 구조적으로 차단.
        // Changed: 사이드테이블도 curated FBX만 사용.
        // Why: 외부 prefab 피벗/스케일 차이와 PR 용량 증가를 동시에 피하기 위함.
        if (!PlaceModel(R, "SideTableModel", $"{FModel}/SideTable.fbx", V(.88f, 0f, -1.2f), Quaternion.Euler(0, -12f, 0), .82f, mWd, recenterXZ: true, snapToFloor: true, targetWorldSize: V(.76f,.58f,.56f)))
        {
            var table=new GameObject("SideTable"); table.transform.SetParent(R.transform);
            table.transform.localPosition=V(.88f,0,-1.2f);
            table.transform.localRotation=Quaternion.Euler(0,-12f,0);
            Cyl("TTop",table,V(0,.48f,0),Q0,V(.4f,.015f,.4f),mWd);
            Cyl("TLeg",table,V(0,.24f,0),Q0,V(.04f,.24f,.04f),mWd);
            Cyl("Cup",table,V(-.1f,.52f,.05f),Q0,V(.03f,.04f,.03f),Mat(DM,"M_Cup",Hex("F7EFE3"),.08f));
        }
        // Changed: 테이블 위 소품은 테이블 bounds 상단 중앙 기준으로 배치해 끝단 이탈 방지
        PlaceCupOnTop(R, "SideTableModel", Mat(DM,"M_Cup",Hex("F7EFE3"),.08f));
        PlaceCupOnTop(R, "SideTable", Mat(DM,"M_Cup",Hex("F7EFE3"),.08f));

        // =========================
        //  천장: 저자극 조명
        // =========================
        BuildPendant(R,"Pend1",V(0,RH,0.18f),mWd,Mat(DM,"M_Shade",Hex("EAD7BF"),.12f));
        BuildPendant(R,"Pend2",V(0,RH,-.88f),mWd,Mat(DM,"M_Shade2",Hex("E8D2B8"),.12f));
        var mCL=Mat(DM,"M_CeilL",Hex("F7F1E8"),.04f); Emit(mCL,new Color(1f,.94f,.86f)*.14f);
        Cyl("CL1",R,V(-1.3f,RH-.001f,0),Quaternion.Euler(180,0,0),V(.15f,.005f,.15f),mCL);
        Cyl("CL2",R,V(1.3f,RH-.001f,0),Quaternion.Euler(180,0,0),V(.15f,.005f,.15f),mCL);
        Cyl("CL3",R,V(0,RH-.001f,1.5f),Quaternion.Euler(180,0,0),V(.15f,.005f,.15f),mCL);
    }

    // 화분 + 잎 생성
    static void BuildPlant(GameObject p,string n,Vector3 pos,Material potM,Material leafM)
    {
        // Changed: 식물은 화분 포함 프리팹만 사용하고, 뾰족 잎 단독 모델을 제외.
        // Why: "바닥에 꽂힌 형태/뾰족 잎 불안감" 피드백을 반영해 안정감 있는 모티프로 교체.
        bool placed =
            // Changed: 식물은 00.Main/Art/Props/Plant/Models의 curated 화분 모델만 사용.
            // Why: polyperfect 원본 팩 없이도 화분 배치가 유지되도록 함.
            PlaceModel(p, $"{n}_Model", $"{PModel}/PottedPlant.fbx", pos, Quaternion.Euler(0, 0f, 0), .6f, potM, recenterXZ: true, snapToFloor: true, targetWorldSize: V(.42f,.6f,.42f), floorOffset: .01f);

        if (placed) return;

        var g=new GameObject(n); g.transform.SetParent(p.transform); g.transform.localPosition=pos;
        Cyl(n+"Pot",g,V(0,.15f,0),Q0,V(.15f,.15f,.15f),potM);
        Cyl(n+"PotRim",g,V(0,.3f,0),Q0,V(.17f,.005f,.17f),potM);
        // Changed: fallback 잎을 둥근 실루엣으로 구성해 뾰족한 형태를 피함.
        Sph(n+"Leaf1",g,V(0,.5f,0),V(.26f,.22f,.24f),leafM);
        Sph(n+"Leaf2",g,V(.09f,.56f,.04f),V(.17f,.16f,.14f),leafM);
        Sph(n+"Leaf3",g,V(-.08f,.54f,-.06f),V(.16f,.15f,.13f),leafM);
        foreach(Transform c in g.transform) c.gameObject.isStatic=true;
    }

    // 펜던트 조명 생성
    static void BuildPendant(GameObject p,string n,Vector3 pos,Material wireM,Material shadeM)
    {
        // Changed: 펜던트는 단일 모델 우선 배치
        if (PlaceModel(p, $"{n}_Model", $"{DModel}/PendantLamp.fbx", pos + V(0,-.04f,0), Q0, .42f, shadeM, recenterXZ: true))
            return;

        var g=new GameObject(n); g.transform.SetParent(p.transform); g.transform.localPosition=pos;
        Cyl(n+"Wire",g,V(0,-.15f,0),Q0,V(.005f,.15f,.005f),wireM);
        // 갓: 둥근 반구 (Sphere 위쪽 절반처럼)
        Cyl(n+"Shade",g,V(0,-.33f,0),Q0,V(.18f,.03f,.18f),shadeM);
        var b=Mat(DM,$"M_{n}Bulb",Hex("FFE8C8"),.8f); Emit(b,Hex("FFE8C8")*2f);
        Sph(n+"Bulb",g,V(0,-.36f,0),V(.06f,.06f,.06f),b);
        foreach(Transform c in g.transform) c.gameObject.isStatic=true;
    }

    // =========================== 기계 ===========================
    // 계층 구조:
    //   ClawMachine
    //   ├── Body          ← 본체 (정적): Base, Pillars, TopFrame, Glass, TopHousing
    //   ├── Rails         ← 레일 시스템 (이동부)
    //   │   ├── RailZL, RailZR  (정적 레일)
    //   │   └── RailX           (Z방향 이동)
    //   │       └── Carriage    (X방향 이동)
    //   │           └── ClawAssembly (Y방향 하강/상승)
    //   │               ├── Rope
    //   │               └── ClawHub  ★ 집게 끝 (바하레 인터페이스)
    //   │                   ├── Hub, F0, F1, F2
    //   ├── PrizeArea     ← 인형 바닥 + 투출구
    //   ├── Panel         ← 컨트롤 패널 (조이스틱/버튼)
    //   ├── Lighting      ← LED + 내부 스폿
    //   └── Sign          ← 간판
    static void BuildMachine()
    {
        var root=new GameObject("ClawMachine"); root.transform.position=MPos;
        // Changed: 검정/적색 고대비 외장을 제거하고 아이보리-파스텔 계열로 통일
        var mFr=Mat(CM,"M_Frame",CFrame,.14f);
        var mMe=Mat(CM,"M_Metal",CMetal,.28f);
        var mGl=Glass(CM,"M_Glass",new(.92f,.96f,1,.1f));
        var mLd=Mat(CM,"M_LED",Hex("FFE1C6"),.22f); Emit(mLd,Hex("FFE1C6")*.2f);
        var mBt=Mat(CM,"M_Btn",Hex("CFAE9E"),.2f); Emit(mBt,Hex("CFAE9E")*.07f);
        var mJs=Mat(CM,"M_Joy",Hex("8E8A86"),.2f);
        // Changed: 투출구는 고대비 텍스처 대신 저자극 재질로 완화
        var mCh=Mat(CM,"M_Chute",Hex("BBAE9E"),.12f);
        ClearLitMaps(mFr);
        ClearLitMaps(mMe);
        ClearLitMaps(mBt);

        float gH=MH-BH-TH, hW=MW/2-FT/2, hD=MD/2-FT/2;
        float playY = PrizeDeckYLocal();

        // ---- Body: 본체 (정적) ----
        var body=new GameObject("Body"); body.transform.SetParent(root.transform); body.transform.localPosition=V(0,0,0);
        Box("Base",body,V(0,BH/2,0),V(MW,BH,MD),mFr);
        Cyl("Pillar_FL",body,V(-hW,BH+(MH-BH)/2,-hD),Q0,V(FT,(MH-BH)/2,FT),mFr);
        Cyl("Pillar_FR",body,V(hW,BH+(MH-BH)/2,-hD),Q0,V(FT,(MH-BH)/2,FT),mFr);
        Cyl("Pillar_BL",body,V(-hW,BH+(MH-BH)/2,hD),Q0,V(FT,(MH-BH)/2,FT),mFr);
        Cyl("Pillar_BR",body,V(hW,BH+(MH-BH)/2,hD),Q0,V(FT,(MH-BH)/2,FT),mFr);
        float tY=MH-TH/2;
        Box("TopFrame_F",body,V(0,tY,-hD),V(MW,FT,FT),mFr);
        Box("TopFrame_B",body,V(0,tY,hD),V(MW,FT,FT),mFr);
        Box("TopFrame_L",body,V(-hW,tY,0),V(FT,FT,MD),mFr);
        Box("TopFrame_R",body,V(hW,tY,0),V(FT,FT,MD),mFr);
        float gY=BH+gH/2, gW=MW-FT*2, gD=MD-FT*2;
        // Changed: 실제 기계처럼 하단 캐비닛(불투명) + 상단 유리창 구조로 분리
        float glassTop = BH + gH - FT * .5f;
        float glassBottom = Mathf.Max(BH + .55f, playY - .08f);
        float glassH = Mathf.Max(.25f, glassTop - glassBottom);
        float glassY = glassBottom + glassH / 2f;
        float lowerPanelH = glassBottom - BH;
        Box("FrontLowerPanel",body,V(0,BH+lowerPanelH/2f,-MD/2+.006f),V(gW,lowerPanelH,.012f),mFr);
        Box("SideLowerPanel_L",body,V(-MW/2+.006f,BH+lowerPanelH/2f,0),V(.012f,lowerPanelH,gD),mFr);
        Box("SideLowerPanel_R",body,V(MW/2-.006f,BH+lowerPanelH/2f,0),V(.012f,lowerPanelH,gD),mFr);
        Box("Glass_F",body,V(0,glassY,-MD/2+.003f),V(gW,glassH,.005f),mGl);
        Box("Glass_L",body,V(-MW/2+.003f,glassY,0),V(.005f,glassH,gD),mGl);
        Box("Glass_R",body,V(MW/2-.003f,glassY,0),V(.005f,glassH,gD),mGl);
        Box("BackPanel",body,V(0,gY,MD/2-.003f),V(gW,gH-FT,.01f),mFr);
        Box("TopHousing",body,V(0,MH-TH/2,0),V(MW-.01f,TH,MD-.01f),mFr);

        // ---- Rails: 레일 + 집게 (이동부) ----
        var rails=new GameObject("Rails"); rails.transform.SetParent(root.transform); rails.transform.localPosition=V(0,0,0);
        float rY=MH-TH-.02f,rR=.012f,iW=MW-FT*2-.04f,iD=MD-FT*2-.04f;
        Cyl("RailZL",rails,V(-iW/2,rY,0),Quaternion.Euler(90,0,0),V(rR*2,iD/2,rR*2),mMe);
        Cyl("RailZR",rails,V(iW/2,rY,0),Quaternion.Euler(90,0,0),V(rR*2,iD/2,rR*2),mMe);
        // Changed: RailX는 회전/스케일된 원통 메쉬가 아니라 움직임용 빈 Transform으로 생성.
        // Why: Carriage를 원통 메쉬 아래에 붙이면 부모의 회전/비균일 스케일 때문에 local X 이동이 실제 레일 X축과 어긋남.
        var railX=new GameObject("RailX"); railX.transform.SetParent(rails.transform); railX.transform.localPosition=V(0,rY,0);
        Cyl("RailXVisual",railX,V(0,0,0),Quaternion.Euler(0,0,90),V(rR*2,iW/2,rR*2),mMe,false);
        // Changed: Carriage도 움직임용 빈 Transform으로 두고 시각 큐브는 자식으로 분리.
        // Why: ClawAssembly 하강/상승이 부모 메쉬 스케일에 오염되지 않게 하기 위함.
        var carriage=new GameObject("Carriage"); carriage.transform.SetParent(railX.transform); carriage.transform.localPosition=V(0,0,0);
        Box("CarriageVisual",carriage,V(0,0,0),V(.06f,.04f,.06f),mMe,false);
        var clawAsm=new GameObject("ClawAssembly"); clawAsm.transform.SetParent(carriage.transform); clawAsm.transform.localPosition=V(0,0,0);
        Cyl("Rope",clawAsm,V(0,-.22f,0),Q0,V(.006f,.2f,.006f),mMe,false);
        var clawHub=new GameObject("ClawHub"); clawHub.transform.SetParent(clawAsm.transform); clawHub.transform.localPosition=V(0,-.44f,0);
        Sph("Hub",clawHub,V(0,0,0),V(.04f,.03f,.04f),mMe);
        for(int i=0;i<3;i++){
            float a=i*120f,rd=a*Mathf.Deg2Rad;
            var finger=new GameObject($"F{i}"); finger.transform.SetParent(clawHub.transform);
            finger.transform.localPosition=V(Mathf.Sin(rd)*.015f,-.015f,Mathf.Cos(rd)*.015f);
            finger.transform.localRotation=Quaternion.Euler(0,a,15f);
            Box("Upper",finger,V(0,-.024f,0),V(.015f,.048f,.015f),mMe,false);
            Box("Lower",finger,V(0,-.06f,0),V(.015f,.028f,.015f),mMe,false);
        }

        // ---- PrizeArea: 인형 바닥 + 투출구 ----
        // Changed: 실제 상용 기계처럼 상단 플레이필드(raised playfield)로 인형 배치 높이 상향
        var prize=new GameObject("PrizeArea"); prize.transform.SetParent(root.transform); prize.transform.localPosition=V(0,playY,0);
        Box("PrizeFloor",prize,V(0,0,0),V(MW-FT*2,.025f,MD-FT*2),Mat(CM,"M_PF",Hex("EDE9E2"),.3f));
        Box("PrizeEdge_F",prize,V(0,.02f,-(MD/2-FT-.02f)),V(MW-FT*2,.035f,.015f),mFr);
        Box("PrizeEdge_B",prize,V(0,.02f,(MD/2-FT-.02f)),V(MW-FT*2,.035f,.015f),mFr);
        Box("PrizeEdge_L",prize,V(-(MW/2-FT-.02f),.02f,0),V(.015f,.035f,MD-FT*2),mFr);
        Box("PrizeEdge_R",prize,V((MW/2-FT-.02f),.02f,0),V(.015f,.035f,MD-FT*2),mFr);
        float half=(MW/2)-FT-.05f, hx=half*.8f, hz=-half*.8f;
        Box("DropHole",prize,V(hx,.014f,hz),V(.16f,.004f,.16f),Mat(CM,"M_Hole",Hex("8D857B"),.04f));
        // Changed: DropHole에서 전면 하단 수거함으로 이어지는 경사 슈트 추가
        var ramp=GameObject.CreatePrimitive(PrimitiveType.Cube); ramp.name="DropRamp";
        ramp.transform.SetParent(prize.transform); ramp.transform.localPosition=V(hx*.82f,-.08f,hz*.86f);
        ramp.transform.localRotation=Quaternion.Euler(34f,0,0); ramp.transform.localScale=V(.13f,.012f,.26f);
        ramp.GetComponent<Renderer>().sharedMaterial=mCh; ramp.isStatic=true;

        // Changed: 전면 하단에 실제 기계 형태의 투출구(Prize Chute) + 수거함 생성
        var chute=new GameObject("PrizeChute"); chute.transform.SetParent(root.transform); chute.transform.localPosition=V(0,0,0);
        float chW=.21f,chH=.14f,chY=BH+.22f,chFront=-MD/2+.016f;
        Box("ChuteFrameTop",chute,V(0,chY+chH/2+.024f,chFront),V(chW+.065f,.048f,.038f),mFr);
        Box("ChuteFrameBottom",chute,V(0,chY-chH/2-.024f,chFront),V(chW+.065f,.048f,.038f),mFr);
        Box("ChuteFrameL",chute,V(-chW/2-.026f,chY,chFront),V(.048f,chH,.038f),mFr);
        Box("ChuteFrameR",chute,V(chW/2+.026f,chY,chFront),V(.048f,chH,.038f),mFr);
        Box("ChuteOpening",chute,V(0,chY,chFront+.012f),V(chW,chH,.02f),mCh);
        // 본체-투출구 결합 브릿지
        Box("ChuteBridge",chute,V(0,chY,-MD/2+.001f),V(chW+.05f,chH+.06f,.026f),mFr);
        Box("ChuteGlowTop",chute,V(0,chY+chH/2+.002f,chFront+.018f),V(chW+.002f,.006f,.01f),mLd);

        var bin=new GameObject("PickupBin"); bin.transform.SetParent(chute.transform); bin.transform.localPosition=V(0,.18f,-MD/2-.062f);
        Box("BinBase",bin,V(0,0,0),V(chW+.03f,.03f,.12f),mMe);
        Box("BinWallL",bin,V(-(chW+.03f)/2f,.045f,0),V(.012f,.09f,.12f),mMe);
        Box("BinWallR",bin,V((chW+.03f)/2f,.045f,0),V(.012f,.09f,.12f),mMe);
        Box("BinWallB",bin,V(0,.045f,.06f),V(chW+.03f,.09f,.012f),mMe);

        // ---- Panel: 컨트롤 패널 ----
        var panel=new GameObject("Panel"); panel.transform.SetParent(root.transform);
        panel.transform.localPosition=V(-MW*.15f,BH+.8f*.55f,-MD/2);
        Box("Board",panel,V(0,0,-.03f),V(.25f,.08f,.06f),Mat(CM,"M_Pnl",Hex("D9D1C8"),.3f));
        Cyl("JoystickBase",panel,V(-.04f,.02f,-.06f),Q0,V(.04f,.01f,.04f),mJs);
        Cyl("JoystickStick",panel,V(-.04f,.05f,-.06f),Q0,V(.012f,.03f,.012f),mJs);
        Sph("JoystickBall",panel,V(-.04f,.08f,-.06f),V(.03f,.03f,.03f),mJs);
        Cyl("GrabButton",panel,V(.06f,.02f,-.06f),Q0,V(.045f,.012f,.045f),mBt);

        // ---- Lighting: LED + 내부 스폿 ----
        var lighting=new GameObject("Lighting"); lighting.transform.SetParent(root.transform); lighting.transform.localPosition=V(0,0,0);
        LEDs("LED_F",lighting,V(0,MH-TH-.01f,-MD/2+.01f),MW-.04f,true,mLd);
        LEDs("LED_L",lighting,V(-MW/2+.01f,MH-TH-.01f,0),MD-.04f,false,mLd);
        LEDs("LED_R",lighting,V(MW/2-.01f,MH-TH-.01f,0),MD-.04f,false,mLd);
        var intLight=new GameObject("InteriorSpot"); intLight.transform.SetParent(lighting.transform);
        intLight.transform.localPosition=V(0,MH-TH-.05f,0); intLight.transform.localRotation=Quaternion.Euler(90,0,0);
        var lt=intLight.AddComponent<Light>(); lt.type=LightType.Spot;
        lt.color=new(1,.95f,.9f); lt.intensity=1.2f; lt.range=2; lt.spotAngle=100; lt.shadows=LightShadows.None;

        // ---- Sign: 간판 ----
        // Changed: 텍스트("Claw Crew")를 제거하고 무문자 헤더로 단순화.
        // Why: 기계 상단 문구가 장면 톤과 충돌한다는 피드백을 반영.
        var mSn=Mat(CM,"M_Sign",Hex("EEDCC6"),.16f); Emit(mSn,new Color(1f,.93f,.82f)*.18f);
        Box("SignHeader",root,V(0,MH+.065f,-MD/2+.018f),V(MW*.7f,.08f,.022f),mSn);
    }

    static void LEDs(string n,GameObject p,Vector3 c,float len,bool isX,Material m){
        int cnt=Mathf.FloorToInt(len/.06f); float sp=len/cnt;
        for(int i=0;i<cnt;i++){
            float off=-len/2+sp*(i+.5f); var pos=c;
            if(isX)pos.x+=off; else pos.z+=off;
            Sph($"{n}{i}",p,pos,V(.015f,.015f,.015f),m);
        }
    }

    // =========================== 조명 ===========================
    static void BuildLighting()
    {
        var r=new GameObject("Lighting");
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
        // Changed: 전체 명도/안정감을 높이고 거친 명암 대비를 완화
        RenderSettings.ambientLight=new(.81f,.78f,.74f);
        RenderSettings.skybox=null;
        var ml=new GameObject("L_Main"); ml.transform.SetParent(r.transform);
        ml.transform.position=V(0,RH-.2f,-.12f);
        var l1=ml.AddComponent<Light>(); l1.type=LightType.Point; l1.color=new(1,.94f,.86f);
        l1.intensity=1.45f; l1.range=8f; l1.shadows=LightShadows.None;
        var wl=new GameObject("L_Win"); wl.transform.SetParent(r.transform);
        wl.transform.position=V(0,2.1f,RD/2-.22f); wl.transform.rotation=Quaternion.Euler(44,180,0);
        var l2=wl.AddComponent<Light>(); l2.type=LightType.Spot; l2.color=new(1,.96f,.9f);
        l2.intensity=1.05f; l2.range=7.2f; l2.spotAngle=88; l2.shadows=LightShadows.None;
        var fl=new GameObject("L_Fill"); fl.transform.SetParent(r.transform);
        fl.transform.position=V(0,2.2f,-1.15f);
        var l3=fl.AddComponent<Light>(); l3.type=LightType.Point; l3.color=new(.98f,.93f,.88f);
        l3.intensity=.88f; l3.range=6.2f; l3.shadows=LightShadows.None;
        // 창문 백광: 뒷배경 깊이감 보강
        var back=new GameObject("L_BackSoft"); back.transform.SetParent(r.transform);
        back.transform.position=V(0,2.05f,RD/2-.08f); back.transform.rotation=Quaternion.Euler(52,180,0);
        var l4=back.AddComponent<Light>(); l4.type=LightType.Spot;
        l4.color=new(.96f,.97f,1f); l4.intensity=.5f; l4.range=4.6f; l4.spotAngle=96f; l4.shadows=LightShadows.None;
    }

    // =========================== 인형 ===========================
    static void BuildDolls()
    {
        // Changed: 유광/기괴한 형태를 줄이고 매트한 플러시 실루엣으로 통일
        var dp=new GameObject("Dolls"); dp.transform.position=MPos;
        var dolls=new[]{
            ("Bugle","D9A19D",.12f,1.2f),
            ("Jjoing","D6DFAE",.1f,.7f),
            ("Ppong","E8BAA0",.11f,.9f),
            ("Simuruk","B9C7D9",.115f,1.25f),
            ("Kkubeok","D9CEE7",.125f,1.45f),
            ("Monggeul","BFDCCE",.11f,1f),
        };
        float sp=(MW/2)-FT-.08f;
        float deckY = PrizeDeckYLocal();
        for(int i=0;i<dolls.Length;i++){
            var(n,hex,size,ms)=dolls[i];
            int c2=i%3,rw=i/3;
            float x=(c2-1)*sp*.66f,z=(rw==0?-1:1)*sp*.38f,y=deckY+.05f+size;
            BuildPlushDoll(dp, n, Hex(hex), V(x,y,z), size, ms, (i%2==0));
        }
    }

    // =========================== XR Origin ===========================
    static void BuildXROrigin()
    {
        string path="Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        var pf=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(pf!=null){
            var xr=Object.Instantiate(pf); xr.name="XR Origin (XR Rig)";
            xr.transform.position=PlayerPos;
            xr.transform.rotation=Quaternion.LookRotation(MPos-PlayerPos);
        }else{
            var cam=new GameObject("FallbackCam");
            cam.transform.position=PlayerPos + V(0,1.6f,0); cam.transform.LookAt(V(MPos.x,1.2f,MPos.z));
            cam.tag="MainCamera"; var c=cam.AddComponent<Camera>(); c.nearClipPlane=.05f; c.fieldOfView=70;
            cam.AddComponent<AudioListener>();
        }
    }

    // =========================== UI ===========================
    static void BuildUI()
    {
        var cv=new GameObject("UI"); var cn=cv.AddComponent<Canvas>();
        cn.renderMode=RenderMode.WorldSpace;
        cv.GetComponent<RectTransform>().sizeDelta=new(600,400);
        // Changed: 안내 UI 가독성을 위해 시선 높이/각도 재조정
        cv.transform.position=V(-RW/2+.06f,1.44f,-.72f);
        cv.transform.rotation=Quaternion.Euler(0,-90,0);
        cv.transform.localScale=V(.00078f,.00078f,.00078f);
        var bg=new GameObject("BG"); bg.transform.SetParent(cv.transform,false);
        bg.AddComponent<UnityEngine.UI.Image>().color=new(.12f,.11f,.1f,.82f);
        var brt=bg.GetComponent<RectTransform>();
        brt.anchorMin=V2(0,0); brt.anchorMax=V2(1,1); brt.offsetMin=V2(0,0); brt.offsetMax=V2(0,0);
        var tx=new GameObject("Txt"); tx.transform.SetParent(cv.transform,false);
        var t=tx.AddComponent<UnityEngine.UI.Text>();
        // Changed: Left Primary 전환과 왼손 스틱 전용 집게 조작 방식에 맞춰 안내 문구 갱신.
        // Why: 실제 코드가 오른손 A/B를 더 이상 사용하지 않으므로 씬 내부 조작법도 같은 규칙을 보여야 함.
        t.text="< Controls >\n\n[Quest 3]\nX: 모드전환\nLeft Stick: 이동/집게\nY: 집게 하강\n\n[Keyboard]\n1: 모드전환\nWASD: 이동/집게\n2: 집게 하강\n\n[Simulator]\nShift+IJKL: Left Stick";
        t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize=36; t.color=new Color(.97f,.96f,.94f,1f); t.alignment=TextAnchor.MiddleCenter;
        var trt=tx.GetComponent<RectTransform>();
        trt.anchorMin=V2(.07f,.08f); trt.anchorMax=V2(.93f,.92f); trt.offsetMin=V2(0,0); trt.offsetMax=V2(0,0);
    }

    // Changed: 커튼 제거 후 뒷벽 점수판 생성
    static void BuildScoreboard(GameObject parent, Vector3 center, Material frameMat)
    {
        // Changed: 점수판은 절차형 대형 보드 1개만 생성(프리팹 분기 제거).
        // Why: 소형 scoreboard 중복 생성/좌우 반전 재발을 구조적으로 차단하기 위함.
        for (int i = parent.transform.childCount - 1; i >= 0; i--)
        {
            var child = parent.transform.GetChild(i);
            // Changed: 과거 버전 잔여물(점수판/창문/틴트)을 일괄 제거.
            // Why: 점수판 2개처럼 보이는 중복 시각 요소를 확실히 제거.
            if (child.name.Contains("Scoreboard") ||
                child.name.StartsWith("WF_") ||
                child.name == "WinGlass" ||
                child.name == "OutsideTint")
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        // Changed: 점수판 방향은 뒷벽 정면(-Z)으로 고정.
        // Why: 자동 LookRotation이 각도 변화를 만들며 가독성이 흔들리는 문제를 방지.
        var faceRot = Quaternion.Euler(0f, 180f, 0f);
        var g = new GameObject("Scoreboard_Main");
        g.transform.SetParent(parent.transform);
        g.transform.localPosition = center;
        var panelMat = Mat(DM, "M_ScorePanel", Hex("CBB79E"), .03f);
        g.transform.localRotation = faceRot;
        // Changed: 점수판 크기를 확대해 커튼 대체 용도로 가시성을 확보.
        // Why: "너무 작은 점수판" 피드백 반영.
        Box("Frame", g, V(0,0,0), V(1.62f,1.08f,.03f), frameMat);
        Box("Panel", g, V(0,0,.01f), V(1.48f,.94f,.012f), panelMat);
        var title = new GameObject("Title");
        title.transform.SetParent(g.transform, false);
        title.transform.localPosition = V(0,.3f,.025f);
        var t1 = title.AddComponent<TextMesh>();
        t1.text = "SCORE";
        t1.anchor = TextAnchor.MiddleCenter;
        t1.alignment = TextAlignment.Center;
        t1.fontSize = 116;
        t1.characterSize = .017f;
        t1.color = Hex("F4E9D9");
        var value = new GameObject("Value");
        value.transform.SetParent(g.transform, false);
        value.transform.localPosition = V(0,-.14f,.025f);
        var t2 = value.AddComponent<TextMesh>();
        t2.text = "000";
        t2.anchor = TextAnchor.MiddleCenter;
        t2.alignment = TextAlignment.Center;
        t2.fontSize = 168;
        t2.characterSize = .016f;
        t2.color = Hex("FFE8C8");
        title.transform.localRotation = Quaternion.identity;
        value.transform.localRotation = Quaternion.identity;
        foreach (Transform c in g.transform) c.gameObject.isStatic = true;
    }

    // 인형: 구체 대신 플러시 실루엣(캡슐 본체 + 머리/귀/손발) 생성
    static void BuildPlushDoll(GameObject parent, string name, Color color, Vector3 pos, float size, float mass, bool earUp)
    {
        var bodyMat = Mat(CM, $"M_D{name}", color, .04f);
        var accent = Color.Lerp(color, Color.white, .25f);
        var accentMat = Mat(CM, $"M_D{name}_A", accent, .04f);
        TunePBR(bodyMat, .05f, .1f);
        TunePBR(accentMat, .05f, .1f);

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = name;
        body.transform.SetParent(parent.transform);
        body.transform.localPosition = pos;
        body.transform.localScale = V(size * .9f, size * 1.15f, size * .9f);
        body.GetComponent<Renderer>().sharedMaterial = bodyMat;
        body.isStatic = false;

        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(body.transform);
        head.transform.localPosition = V(0, .62f, 0);
        head.transform.localScale = V(.65f,.55f,.62f);
        head.GetComponent<Renderer>().sharedMaterial = bodyMat;
        RemoveCollider(head);

        var earY = earUp ? .9f : .82f;
        var earL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        earL.name = "EarL";
        earL.transform.SetParent(body.transform);
        earL.transform.localPosition = V(-.19f, earY, -.04f);
        earL.transform.localScale = V(.18f,.26f,.14f);
        earL.GetComponent<Renderer>().sharedMaterial = accentMat;
        RemoveCollider(earL);

        var earR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        earR.name = "EarR";
        earR.transform.SetParent(body.transform);
        earR.transform.localPosition = V(.19f, earY, -.04f);
        earR.transform.localScale = V(.18f,.26f,.14f);
        earR.GetComponent<Renderer>().sharedMaterial = accentMat;
        RemoveCollider(earR);

        var footL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        footL.name = "FootL";
        footL.transform.SetParent(body.transform);
        footL.transform.localPosition = V(-.13f,-.54f,.12f);
        footL.transform.localScale = V(.25f,.16f,.22f);
        footL.GetComponent<Renderer>().sharedMaterial = accentMat;
        RemoveCollider(footL);

        var footR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        footR.name = "FootR";
        footR.transform.SetParent(body.transform);
        footR.transform.localPosition = V(.13f,-.54f,.12f);
        footR.transform.localScale = V(.25f,.16f,.22f);
        footR.GetComponent<Renderer>().sharedMaterial = accentMat;
        RemoveCollider(footR);

        // Changed: 얼굴 포인트를 추가해 무표정/기괴한 인상을 줄임
        var eyeMat = Mat(CM, $"M_D{name}_Eye", Hex("5A5753"), .02f);
        var eyeL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eyeL.name = "EyeL";
        eyeL.transform.SetParent(head.transform);
        eyeL.transform.localPosition = V(-.12f, .03f, .3f);
        eyeL.transform.localScale = V(.09f,.09f,.03f);
        eyeL.GetComponent<Renderer>().sharedMaterial = eyeMat;
        RemoveCollider(eyeL);
        var eyeR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eyeR.name = "EyeR";
        eyeR.transform.SetParent(head.transform);
        eyeR.transform.localPosition = V(.12f, .03f, .3f);
        eyeR.transform.localScale = V(.09f,.09f,.03f);
        eyeR.GetComponent<Renderer>().sharedMaterial = eyeMat;
        RemoveCollider(eyeR);

        var rb=body.AddComponent<Rigidbody>();
        rb.mass=mass;
        rb.linearDamping=.95f;
        rb.angularDamping=.5f;
    }

    static void RemoveCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
    }

    // =========================== 유틸 ===========================
    static Vector3 V(float x,float y,float z)=>new(x,y,z);
    static Vector2 V2(float x,float y)=>new(x,y);
    static readonly Quaternion Q0=Quaternion.identity;
    // Changed: BuildMachine/BuildDolls가 같은 플레이필드 높이 기준을 공유하도록 공통 함수화
    static float PrizeDeckYLocal() => BH + (MH - BH - TH) * PrizeDeckRatio;

    static GameObject Box(string n,GameObject p,Vector3 pos,Vector3 sc,Material m,bool s=true){
        var o=GameObject.CreatePrimitive(PrimitiveType.Cube); o.name=n;
        o.transform.SetParent(p.transform); o.transform.localPosition=pos; o.transform.localScale=sc;
        o.GetComponent<Renderer>().sharedMaterial=m; o.isStatic=s; return o;
    }
    static GameObject Cyl(string n,GameObject p,Vector3 pos,Quaternion rot,Vector3 sc,Material m,bool s=true){
        var o=GameObject.CreatePrimitive(PrimitiveType.Cylinder); o.name=n;
        o.transform.SetParent(p.transform); o.transform.localPosition=pos; o.transform.localRotation=rot;
        o.transform.localScale=sc; o.GetComponent<Renderer>().sharedMaterial=m; o.isStatic=s; return o;
    }
    static GameObject Sph(string n,GameObject p,Vector3 pos,Vector3 sc,Material m){
        var o=GameObject.CreatePrimitive(PrimitiveType.Sphere); o.name=n;
        o.transform.SetParent(p.transform); o.transform.localPosition=pos; o.transform.localScale=sc;
        o.GetComponent<Renderer>().sharedMaterial=m; o.isStatic=true; return o;
    }
    // Changed: Kenney/오픈소스 FBX를 SceneSetup에서 직접 배치하기 위한 공통 로더 추가
    static bool PlaceModel(
        GameObject parent,
        string name,
        string assetPath,
        Vector3 localPos,
        Quaternion localRot,
        float uniformScale,
        Material fallbackMaterial,
        bool recenterXZ = false,
        bool snapToFloor = false,
        float floorY = 0f,
        Vector3? targetWorldSize = null,
        float floorOffset = 0f)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null) return false;

        GameObject instance;
        if (PrefabUtility.IsPartOfPrefabAsset(prefab))
            instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        else
            instance = Object.Instantiate(prefab);
        if (instance == null) return false;

        instance.name = name;
        instance.transform.SetParent(parent.transform, false);
        instance.transform.localPosition = localPos;
        instance.transform.localRotation = localRot;
        instance.transform.localScale = Vector3.one * uniformScale;

        if (fallbackMaterial != null)
        {
            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (r.sharedMaterials == null || r.sharedMaterials.Length == 0)
                {
                    r.sharedMaterial = fallbackMaterial;
                    continue;
                }
                bool hasAny = false;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null) { hasAny = true; break; }
                }
                if (!hasAny) r.sharedMaterial = fallbackMaterial;
            }
        }

        // Changed: 프리팹마다 원본 단위가 달라 가구/점수판 과대 문제가 반복되어 바운드 기반 목표 크기 보정을 추가.
        // Why: 모델 교체 시에도 폭/높이 상한이 유지되도록 공통 스케일 규칙을 강제하기 위함.
        if (targetWorldSize.HasValue && TryGetWorldBounds(instance, out var fitBefore))
        {
            var target = targetWorldSize.Value;
            float sx = (target.x > 0.0001f && fitBefore.size.x > 0.0001f) ? target.x / fitBefore.size.x : float.PositiveInfinity;
            float sy = (target.y > 0.0001f && fitBefore.size.y > 0.0001f) ? target.y / fitBefore.size.y : float.PositiveInfinity;
            float sz = (target.z > 0.0001f && fitBefore.size.z > 0.0001f) ? target.z / fitBefore.size.z : float.PositiveInfinity;
            float s = Mathf.Min(sx, Mathf.Min(sy, sz));
            if (!float.IsNaN(s) && !float.IsInfinity(s) && s > 0.0001f) instance.transform.localScale *= s;
        }

        if (recenterXZ && TryGetWorldBounds(instance, out var xzBounds))
        {
            var targetWorld = parent.transform.TransformPoint(localPos);
            var delta = new Vector3(targetWorld.x - xzBounds.center.x, 0f, targetWorld.z - xzBounds.center.z);
            instance.transform.position += delta;
        }

        if (snapToFloor && TryGetWorldBounds(instance, out var floorBounds))
        {
            var yDelta = (floorY + floorOffset) - floorBounds.min.y;
            instance.transform.position += new Vector3(0f, yDelta, 0f);
        }

        foreach (var t in instance.GetComponentsInChildren<Transform>(true))
            t.gameObject.isStatic = true;
        return true;
    }
    // Changed: 모델 배치 안정화를 위해 Renderer 기준 월드 바운드 공통 계산
    static bool TryGetWorldBounds(GameObject go, out Bounds b)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            b = default;
            return false;
        }
        b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return true;
    }
    // Changed: 테이블 위 소품을 상단 중앙에 고정해 끝단 이탈 배치를 방지
    static void PlaceCupOnTop(GameObject parent, string hostName, Material cupMat)
    {
        var host = FindChildRecursive(parent.transform, hostName);
        if (host == null || !TryGetWorldBounds(host.gameObject, out var b)) return;
        // Changed: 촛불/컵이 지나치게 작아 보이는 문제를 막기 위해 테이블 크기 비례 스케일을 적용.
        // Why: 모델 교체 시에도 소품 가독성을 유지하기 위한 최소 시인성 규칙.
        float topSpan = Mathf.Max(0.001f, Mathf.Min(b.size.x, b.size.z));
        float cupRadius = Mathf.Clamp(topSpan * .12f, .03f, .07f);
        float cupHeight = Mathf.Clamp(topSpan * .23f, .06f, .15f);

        var cup = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cup.name = $"{hostName}_Cup";
        cup.transform.SetParent(parent.transform, true);
        cup.transform.position = new Vector3(b.center.x, b.max.y + cupHeight * .45f, b.center.z);
        cup.transform.localScale = V(cupRadius, cupHeight, cupRadius);
        cup.GetComponent<Renderer>().sharedMaterial = cupMat;
        cup.isStatic = true;
    }
    static Transform FindChildRecursive(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var r = FindChildRecursive(root.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }
    static Material Mat(string dir,string n,Color c,float sm){
        string p=$"{dir}/{n}.mat";
        var ex=AssetDatabase.LoadAssetAtPath<Material>(p); if(ex)return ex;
        ED(dir);
        var s=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
        var m=new Material(s); m.name=n; m.SetColor("_BaseColor",c); m.SetFloat("_Smoothness",sm);
        AssetDatabase.CreateAsset(m,p); return m;
    }
    static Material PBR(string dir,string n,Color tint,float sm,string alb,string nor,string rgh){
        string p=$"{dir}/{n}.mat";
        var ex=AssetDatabase.LoadAssetAtPath<Material>(p); if(ex)return ex;
        ED(dir);
        var s=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
        var m=new Material(s); m.name=n; m.SetColor("_BaseColor",tint); m.SetFloat("_Smoothness",sm);
        if(!string.IsNullOrEmpty(alb)){var t=AssetDatabase.LoadAssetAtPath<Texture2D>(alb); if(t)m.SetTexture("_BaseMap",t);}
        if(!string.IsNullOrEmpty(nor)){
            var imp=AssetImporter.GetAtPath(nor) as TextureImporter;
            if(imp!=null&&imp.textureType!=TextureImporterType.NormalMap){imp.textureType=TextureImporterType.NormalMap;imp.SaveAndReimport();}
            var t=AssetDatabase.LoadAssetAtPath<Texture2D>(nor); if(t){m.SetTexture("_BumpMap",t);m.SetFloat("_BumpScale",1);m.EnableKeyword("_NORMALMAP");}
        }
        AssetDatabase.CreateAsset(m,p); return m;
    }
    // Changed: 재질 언어 통일(저광택/저노멀)을 위해 기존 머티리얼도 강제 보정
    static void TunePBR(Material m, float smoothness, float bumpScale)
    {
        if (m == null) return;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", Mathf.Clamp01(smoothness));
        if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", Mathf.Max(0f, bumpScale));
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (m.HasProperty("_OcclusionStrength")) m.SetFloat("_OcclusionStrength", .9f);
    }
    // Changed: 강한 베이스 텍스처(검정/빨강) 잔존 방지를 위해 Lit 텍스처 슬롯 정리
    static void ClearLitMaps(Material m)
    {
        if (m == null) return;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", null);
        if (m.HasProperty("_BumpMap")) { m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); }
    }
    static Material Glass(string dir,string n,Color c){
        string p=$"{dir}/{n}.mat";
        var ex=AssetDatabase.LoadAssetAtPath<Material>(p); if(ex)return ex;
        ED(dir);
        var s=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
        var m=new Material(s); m.name=n; m.SetFloat("_Surface",1); m.SetColor("_BaseColor",c);
        m.SetFloat("_Smoothness",.95f); m.renderQueue=3000;
        m.SetOverrideTag("RenderType","Transparent"); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        AssetDatabase.CreateAsset(m,p); return m;
    }
    static void Emit(Material m,Color c){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",c);}
    static void ED(string p){
        if(AssetDatabase.IsValidFolder(p))return;
        var parts=p.Split('/'); string cur=parts[0];
        for(int i=1;i<parts.Length;i++){string nx=cur+"/"+parts[i]; if(!AssetDatabase.IsValidFolder(nx))AssetDatabase.CreateFolder(cur,parts[i]); cur=nx;}
    }
    static void AddToBuild(string p){
        var ss=EditorBuildSettings.scenes; foreach(var s in ss)if(s.path==p)return;
        var ns=new EditorBuildSettingsScene[ss.Length+1]; ss.CopyTo(ns,0);
        ns[ss.Length]=new EditorBuildSettingsScene(p,true); EditorBuildSettings.scenes=ns;
    }
    static void DeleteAllMats(){
        // Changed: 커튼 경로(CuM) 제거 이후 머티리얼 삭제 대상 배열을 명시형으로 정리.
        // Why: 제거된 상수 참조 및 암시적 배열 타입 추론 오류를 방지.
        var materialDirs = new string[]
        {
            RM, CM, FM, PM, DM, "Assets/00.Main/Art/Shared/Materials"
        };
        foreach(var d in materialDirs){ // Shared 잔여 .mat도 삭제
            if(!AssetDatabase.IsValidFolder(d))continue;
            foreach(var g in AssetDatabase.FindAssets("t:Material",new string[]{d}))
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(g));
        }
        AssetDatabase.Refresh();
    }
}
