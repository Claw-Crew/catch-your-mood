using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// Claw Crew > Build Main Scene
///
/// 모든 소품을 프로시저럴로 생성 (FBX 의존 없음 → 스케일 문제 불가)
/// PBR 텍스처(Poly Haven CC0)를 벽/바닥/천장/기계에 적용
///
/// scene_background_research.md 3.2절 조감도:
///   뒷벽(Z+2.5): 창문+커튼, 네온사인, 선반(좌)
///   중앙(Z+0.3): 인형뽑기 기계
///   플레이어(Z-1.0): XR Origin, 러그
///   앞(Z-1.5): 스툴(좌), 사이드테이블+컵(우)
/// </summary>
public static class SceneSetup
{
    const string SceneDir = "Assets/00.Main/Scenes/Main";
    const string ScenePath = "Assets/00.Main/Scenes/Main/MainScene.unity";
    // 머티리얼 경로: 카테고리별 분리
    const string RM = "Assets/00.Main/Art/Room/Materials";           // 방 구조 (벽/바닥/천장)
    const string CM = "Assets/00.Main/Art/ClawMachine/Materials";    // 인형뽑기 기계
    const string FM = "Assets/00.Main/Art/Props/Furniture/Materials"; // 가구 (의자/테이블/선반)
    const string CuM = "Assets/00.Main/Art/Props/Curtain/Materials"; // 커튼
    const string PM = "Assets/00.Main/Art/Props/Plant/Materials";    // 식물
    const string DM = "Assets/00.Main/Art/Props/Decoration/Materials"; // 장식 (시계/포스터/네온/러그/조명/꽃병)
    // 텍스처 경로
    const string RT = "Assets/00.Main/Art/Room/Textures";
    const string CT = "Assets/00.Main/Art/ClawMachine/Textures";
    const string CuT = "Assets/00.Main/Art/Props/Curtain/Textures";
    const string PT = "Assets/00.Main/Art/Props/Plant/Textures";

    const float RW = 5.5f, RD = 5f, RH = 3f, WT = 0.1f;
    const float MW = 0.78f, MD = 0.78f, MH = 2f, BH = 0.15f, TH = 0.25f, FT = 0.04f;
    static readonly Vector3 MPos = new(0, 0, 0.3f);

    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
    static readonly Color CWall=Hex("C5D5CB"), CFloor=Hex("D4C4A8"), CCeil=Hex("E8E4DC");
    static readonly Color CWood=Hex("8C6E53"), CAccent=Hex("B8A090");
    static readonly Color CFrame=Hex("F0EDE8"), CMetal=Hex("BFBFC3"), CLed=Hex("FFE3C4");

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
        // 머티리얼: 카테고리별 폴더에 저장
        // Room: 벽/바닥/천장
        var mW = PBR(RM,"M_Wall",CWall,.2f, $"{RT}/WallPlaster_Diff.jpg",$"{RT}/WallPlaster_Normal.png",null);
        var mF = PBR(RM,"M_Floor",CFloor,.3f, $"{RT}/WoodFloor_Diff.jpg",$"{RT}/WoodFloor_Normal.jpg",null);
        var mC = PBR(RM,"M_Ceil",CCeil,.15f, $"{RT}/Ceiling_Diff.jpg",$"{RT}/Ceiling_Normal.jpg",null);
        var mWd = Mat(FM,"M_Wood",CWood,.4f); // Furniture: 나무 공통
        // Curtain
        var mCurt = PBR(CuM,"M_Curtain",Hex("F0E8DD"),.1f, $"{CuT}/CurtainFabric_Diff.jpg",$"{CuT}/CurtainFabric_Normal.png",null);
        // Plant
        var mLeaf = Mat(PM,"M_Leaf",Hex("5B8C5A"),.15f);
        var mPot = Mat(PM,"M_Pot",Hex("C4846C"),.3f);

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

        // 코너 몰딩 (둥근)
        var mMd=Mat(RM,"M_Mold",CWall*.95f,.2f);
        Cyl("Mold_BL",R,V(-RW/2,RH/2,RD/2),Q0,V(.06f,RH/2,.06f),mMd);
        Cyl("Mold_BR",R,V(RW/2,RH/2,RD/2),Q0,V(.06f,RH/2,.06f),mMd);
        Cyl("Mold_FL",R,V(-RW/2,RH/2,-RD/2),Q0,V(.06f,RH/2,.06f),mMd);
        Cyl("Mold_FR",R,V(RW/2,RH/2,-RD/2),Q0,V(.06f,RH/2,.06f),mMd);

        // =========================
        //  뒷벽: 창문 + 커튼 + 네온
        // =========================
        float wW=1.4f,wH=1.1f,wB=1.2f,wZ=RD/2-.01f;
        Box("WF_T",R,V(0,wB+wH+.025f,wZ),V(wW+.1f,.05f,.03f),mWd);
        Box("WF_B",R,V(0,wB-.025f,wZ),V(wW+.1f,.05f,.04f),mWd);
        Box("WF_L",R,V(-wW/2-.025f,wB+wH/2,wZ),V(.05f,wH,.03f),mWd);
        Box("WF_R",R,V(wW/2+.025f,wB+wH/2,wZ),V(.05f,wH,.03f),mWd);
        Box("WinGlass",R,V(0,wB+wH/2,wZ+.01f),V(wW,wH,.005f),
            Glass(RM,"M_WGlass",new(.85f,.92f,1,.2f)));

        // 커튼: 주름 (Cylinder 세트)
        float cZ=RD/2-.05f, cY=wB+wH/2, cH=wH+.3f;
        for(int i=0;i<4;i++){
            Cyl($"CurtL{i}",R,V(-wW/2-.15f-i*.07f,cY,cZ),Q0,V(.06f,cH/2,.015f),mCurt);
            Cyl($"CurtR{i}",R,V(wW/2+.15f+i*.07f,cY,cZ),Q0,V(.06f,cH/2,.015f),mCurt);
        }

        // 네온 사인
        var mN=Mat(DM,"M_Neon",Hex("FFD37A"),.3f); Emit(mN,Hex("FFD37A")*1.5f);
        Box("NeonSign",R,V(0,wB+wH+.15f,RD/2-.06f),V(1,.1f,.02f),mN);

        // =========================
        //  뒷벽 좌측: 선반 + 디스플레이 인형
        // =========================
        float sx=-RW/2+.3f, sZ=1.5f;
        // 선반 = 2단 나무판 + 사이드 패널
        Box("Shelf_Side_L",R,V(sx-.31f,.75f,sZ),V(.02f,1.2f,.25f),mWd);
        Box("Shelf_Side_R",R,V(sx+.31f,.75f,sZ),V(.02f,1.2f,.25f),mWd);
        Box("Shelf_Bot",R,V(sx,.3f,sZ),V(.6f,.02f,.25f),mWd);
        Box("Shelf_Mid",R,V(sx,.7f,sZ),V(.6f,.02f,.25f),mWd);
        Box("Shelf_Top",R,V(sx,1.1f,sZ),V(.6f,.02f,.25f),mWd);
        Box("Shelf_Back",R,V(sx,.7f,sZ-.12f),V(.6f,1.2f,.01f),mWd);
        // 선반 위 디스플레이 인형
        Sph("DDoll1",R,V(sx-.12f,.78f,sZ+.05f),V(.08f,.10f,.08f),Mat(CM,"M_DD1",Hex("C8E4D4"),.15f));
        Sph("DDoll2",R,V(sx+.12f,.78f,sZ+.05f),V(.07f,.09f,.07f),Mat(CM,"M_DD2",Hex("FF8A5C"),.15f));
        Sph("DDoll3",R,V(sx,1.18f,sZ+.05f),V(.06f,.08f,.06f),Mat(CM,"M_DD3",Hex("D6CFF0"),.15f));

        // =========================
        //  뒷벽 우측: 식물 화분
        // =========================
        BuildPlant(R,"Plant_BR",V(RW/2-.5f,0,2f),mPot,mLeaf);

        // =========================
        //  좌측 벽: 식물 + 포스터
        // =========================
        BuildPlant(R,"Plant_L",V(-RW/2+.4f,0,-.3f),mPot,mLeaf);
        // 포스터
        float pLx=-RW/2+.16f;
        Box("PostL_F",R,V(pLx,1.6f,.5f),V(.04f,.42f,.32f),mWd);
        Box("PostL_A",R,V(pLx+.015f,1.6f,.5f),V(.01f,.35f,.25f),Mat(DM,"M_Post1",Hex("E8DDD0"),.2f));

        // =========================
        //  우측 벽: 포스터
        // =========================
        float pRx=RW/2-.16f;
        Box("PostR_F",R,V(pRx,1.6f,-.3f),V(.04f,.42f,.32f),mWd);
        Box("PostR_A",R,V(pRx-.015f,1.6f,-.3f),V(.01f,.35f,.25f),Mat(DM,"M_Post2",Hex("DDD5CC"),.2f));

        // =========================
        //  플레이어 영역 (Z=-1.0): 러그
        // =========================
        Cyl("Rug",R,V(0,.005f,-.8f),Q0,V(1.2f,.005f,1.2f),Mat(DM,"M_Rug",Hex("C4A08A"),.1f));

        // =========================
        //  앞쪽 좌측: 스툴 (Cylinder 좌석 + 다리)
        // =========================
        var stool=new GameObject("Stool"); stool.transform.SetParent(R.transform);
        stool.transform.localPosition=V(-1.3f,0,-1.6f);
        Cyl("Seat",stool,V(0,.65f,0),Q0,V(.32f,.02f,.32f),mWd);
        Cyl("SLeg0",stool,V(.1f,.32f,.1f),Q0,V(.025f,.32f,.025f),mWd);
        Cyl("SLeg1",stool,V(-.1f,.32f,.1f),Q0,V(.025f,.32f,.025f),mWd);
        Cyl("SLeg2",stool,V(.1f,.32f,-.1f),Q0,V(.025f,.32f,.025f),mWd);
        Cyl("SLeg3",stool,V(-.1f,.32f,-.1f),Q0,V(.025f,.32f,.025f),mWd);
        // 등받이 (둥근)
        Cyl("SBack",stool,V(0,.85f,-.12f),Quaternion.Euler(10,0,0),V(.14f,.12f,.01f),mWd);

        // =========================
        //  앞쪽 우측: 사이드 테이블 + 소품
        // =========================
        var table=new GameObject("SideTable"); table.transform.SetParent(R.transform);
        table.transform.localPosition=V(1.3f,0,-1.6f);
        // 원형 테이블 (Cylinder)
        Cyl("TTop",table,V(0,.48f,0),Q0,V(.45f,.015f,.45f),mWd);
        Cyl("TLeg",table,V(0,.24f,0),Q0,V(.04f,.24f,.04f),mWd);
        Cyl("TBase",table,V(0,.01f,0),Q0,V(.2f,.01f,.2f),mWd);
        // 커피컵 (테이블 위)
        Cyl("Cup",table,V(-.1f,.52f,.05f),Q0,V(.035f,.04f,.035f),Mat(DM,"M_Cup",Hex("F5F0E8"),.5f));
        Cyl("CupHandle",table,V(-.14f,.52f,.05f),Q0,V(.008f,.02f,.015f),Mat(DM,"M_Cup2",Hex("F5F0E8"),.5f));
        // 꽃병 (Sphere 형태)
        Sph("Vase",table,V(.1f,.56f,-.05f),V(.06f,.1f,.06f),Mat(DM,"M_Vase",Hex("A8C4B8"),.4f));

        // =========================
        //  앞벽: 벽시계
        // =========================
        Cyl("Clock",R,V(0,2.2f,-RD/2+.06f),Quaternion.Euler(90,0,0),V(.18f,.01f,.18f),
            Mat(DM,"M_Clock",Hex("F0EDE8"),.5f));
        // 시침/분침
        Box("ClockH",R,V(0,2.2f,-RD/2+.07f),V(.005f,.06f,.005f),Mat(DM,"M_CH",Hex("333333"),.5f));
        Box("ClockM",R,V(.02f,2.23f,-RD/2+.07f),V(.004f,.08f,.004f),Mat(DM,"M_CM",Hex("333333"),.5f));

        // =========================
        //  천장: 펜던트 조명 2개 + 매입등 3개
        // =========================
        BuildPendant(R,"Pend1",V(0,RH,0.3f),mWd,Mat(DM,"M_Shade",CAccent,.3f));
        BuildPendant(R,"Pend2",V(0,RH,-1.2f),mWd,Mat(DM,"M_Shade2",CAccent,.3f));
        var mCL=Mat(DM,"M_CeilL",Hex("F5F0E8"),.1f); Emit(mCL,new Color(1,.95f,.9f)*.3f);
        Cyl("CL1",R,V(-1.5f,RH-.001f,0),Quaternion.Euler(180,0,0),V(.18f,.005f,.18f),mCL);
        Cyl("CL2",R,V(1.5f,RH-.001f,0),Quaternion.Euler(180,0,0),V(.18f,.005f,.18f),mCL);
        Cyl("CL3",R,V(0,RH-.001f,1.8f),Quaternion.Euler(180,0,0),V(.18f,.005f,.18f),mCL);
    }

    // 화분 + 잎 생성
    static void BuildPlant(GameObject p,string n,Vector3 pos,Material potM,Material leafM)
    {
        var g=new GameObject(n); g.transform.SetParent(p.transform); g.transform.localPosition=pos;
        Cyl(n+"Pot",g,V(0,.15f,0),Q0,V(.15f,.15f,.15f),potM);
        Cyl(n+"PotRim",g,V(0,.3f,0),Q0,V(.17f,.005f,.17f),potM);
        Sph(n+"Leaf1",g,V(0,.5f,0),V(.25f,.3f,.25f),leafM);
        Sph(n+"Leaf2",g,V(.08f,.55f,.06f),V(.15f,.2f,.12f),leafM);
        Sph(n+"Leaf3",g,V(-.06f,.52f,-.08f),V(.12f,.18f,.15f),leafM);
        foreach(Transform c in g.transform) c.gameObject.isStatic=true;
    }

    // 펜던트 조명 생성
    static void BuildPendant(GameObject p,string n,Vector3 pos,Material wireM,Material shadeM)
    {
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

        var mFr=PBR(CM,"M_Frame",CFrame,.3f,$"{CT}/redBoxTiles.png",$"{CT}/redBoxTilesNormal.png",$"{CT}/redBoxTilesRoughness.png");
        var mMe=Mat(CM,"M_Metal",CMetal,.6f);
        var mGl=Glass(CM,"M_Glass",new(.9f,.95f,1,.12f));
        var mLd=Mat(CM,"M_LED",CLed,.8f); Emit(mLd,CLed*1.5f);
        var mBt=Mat(CM,"M_Btn",Hex("F25959"),.6f); Emit(mBt,Hex("F25959")*.3f);
        var mJs=Mat(CM,"M_Joy",Hex("4D4D59"),.5f);

        float gH=MH-BH-TH, hW=MW/2-FT/2, hD=MD/2-FT/2;

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
        Box("Glass_F",body,V(0,gY,-MD/2+.003f),V(gW,gH-FT,.005f),mGl);
        Box("Glass_L",body,V(-MW/2+.003f,gY,0),V(.005f,gH-FT,gD),mGl);
        Box("Glass_R",body,V(MW/2-.003f,gY,0),V(.005f,gH-FT,gD),mGl);
        Box("BackPanel",body,V(0,gY,MD/2-.003f),V(gW,gH-FT,.01f),mFr);
        Box("TopHousing",body,V(0,MH-TH/2,0),V(MW-.01f,TH,MD-.01f),mFr);

        // ---- Rails: 레일 + 집게 (이동부) ----
        var rails=new GameObject("Rails"); rails.transform.SetParent(root.transform); rails.transform.localPosition=V(0,0,0);
        float rY=MH-TH-.02f,rR=.012f,iW=MW-FT*2-.04f,iD=MD-FT*2-.04f;
        Cyl("RailZL",rails,V(-iW/2,rY,0),Quaternion.Euler(90,0,0),V(rR*2,iD/2,rR*2),mMe);
        Cyl("RailZR",rails,V(iW/2,rY,0),Quaternion.Euler(90,0,0),V(rR*2,iD/2,rR*2),mMe);
        var railX=Cyl("RailX",rails,V(0,rY,0),Quaternion.Euler(0,0,90),V(rR*2,iW/2,rR*2),mMe,false);
        var carriage=Box("Carriage",railX,V(0,0,0),V(.06f,.04f,.06f),mMe,false);
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
        var prize=new GameObject("PrizeArea"); prize.transform.SetParent(root.transform); prize.transform.localPosition=V(0,BH,0);
        var pf=GameObject.CreatePrimitive(PrimitiveType.Plane); pf.name="PrizeFloor";
        pf.transform.SetParent(prize.transform); pf.transform.localPosition=V(0,.005f,0);
        pf.transform.localScale=V((MW-FT*2)/10,1,(MD-FT*2)/10);
        pf.GetComponent<Renderer>().sharedMaterial=Mat(CM,"M_PF",Hex("EDE9E2"),.3f); pf.isStatic=true;
        float half=(MW/2)-FT-.05f, hx=half*.8f, hz=-half*.8f;
        Box("DropHole",prize,V(hx,.003f,hz),V(.16f,.002f,.16f),Mat(CM,"M_Hole",new(.15f,.15f,.15f,1),.1f));

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
        var mSn=Mat(CM,"M_Sign",Hex("F2E6D0"),.4f); Emit(mSn,new Color(1,.95f,.85f)*.5f);
        Box("Sign",root,V(0,MH+.1f,-MD/2+.02f),V(MW*.8f,.15f,.03f),mSn);
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
        RenderSettings.ambientLight=new(.55f,.50f,.45f);
        RenderSettings.skybox=null;
        var ml=new GameObject("L_Main"); ml.transform.SetParent(r.transform);
        ml.transform.position=V(0,RH-.3f,0);
        var l1=ml.AddComponent<Light>(); l1.type=LightType.Point; l1.color=new(1,.92f,.82f);
        l1.intensity=1.5f; l1.range=8; l1.shadows=LightShadows.Soft;
        var wl=new GameObject("L_Win"); wl.transform.SetParent(r.transform);
        wl.transform.position=V(0,2,RD/2-.3f); wl.transform.rotation=Quaternion.Euler(40,180,0);
        var l2=wl.AddComponent<Light>(); l2.type=LightType.Spot; l2.color=new(1,.96f,.88f);
        l2.intensity=.8f; l2.range=6; l2.spotAngle=80; l2.shadows=LightShadows.None;
        var fl=new GameObject("L_Fill"); fl.transform.SetParent(r.transform);
        fl.transform.position=V(0,2.5f,-1.5f);
        var l3=fl.AddComponent<Light>(); l3.type=LightType.Point; l3.color=new(.95f,.90f,.85f);
        l3.intensity=.5f; l3.range=5; l3.shadows=LightShadows.None;
    }

    // =========================== 인형 ===========================
    static void BuildDolls()
    {
        // 인형은 기계 부품이 아니라 독립 물리 오브젝트 → 씬 루트에 배치
        // 기계 내부 좌표를 월드 좌표로 변환하여 배치
        var dp=new GameObject("Dolls"); dp.transform.position=MPos;
        var dolls=new[]{
            ("Bugle","C43131",V(.12f,.11f,.12f),1.2f),
            ("Jjoing","C5DB77",V(.09f,.15f,.09f),.6f),
            ("Ppong","FF8A5C",V(.13f,.13f,.13f),.8f),
            ("Simuruk","8B9BBF",V(.11f,.13f,.11f),1.4f),
            ("Kkubeok","D6CFF0",V(.15f,.13f,.15f),1.6f),
            ("Monggeul","C8E4D4",V(.12f,.12f,.12f),1f),
        };
        float sp=(MW/2)-FT-.08f;
        for(int i=0;i<dolls.Length;i++){
            var(n,hex,sz,ms)=dolls[i];
            int c2=i%3,rw=i/3;
            float x=(c2-1)*sp*.7f,z=(rw==0?-1:1)*sp*.4f,y=BH+.01f+sz.y/2;
            var mat=Mat(CM,$"M_D{n}",Hex(hex),.15f);
            var d=GameObject.CreatePrimitive(PrimitiveType.Sphere); d.name=n;
            d.transform.SetParent(dp.transform); d.transform.localPosition=V(x,y,z);
            d.transform.localScale=sz; d.GetComponent<Renderer>().sharedMaterial=mat;
            var rb=d.AddComponent<Rigidbody>(); rb.mass=ms; rb.linearDamping=1; rb.angularDamping=.5f;
        }
    }

    // =========================== XR Origin ===========================
    static void BuildXROrigin()
    {
        string path="Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        var pf=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(pf!=null){
            var xr=Object.Instantiate(pf); xr.name="XR Origin (XR Rig)";
            xr.transform.position=V(0,0,-1f);
            xr.transform.rotation=Quaternion.LookRotation(MPos-V(0,0,-1f));
        }else{
            var cam=new GameObject("FallbackCam");
            cam.transform.position=V(0,1.6f,-1f); cam.transform.LookAt(V(MPos.x,1.2f,MPos.z));
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
        cv.transform.position=V(-RW/2+.15f,1.5f,-1.2f);
        cv.transform.rotation=Quaternion.Euler(0,-90,0);
        cv.transform.localScale=V(.001f,.001f,.001f);
        var bg=new GameObject("BG"); bg.transform.SetParent(cv.transform,false);
        bg.AddComponent<UnityEngine.UI.Image>().color=new(.15f,.15f,.15f,.85f);
        var brt=bg.GetComponent<RectTransform>();
        brt.anchorMin=V2(0,0); brt.anchorMax=V2(1,1); brt.offsetMin=V2(0,0); brt.offsetMax=V2(0,0);
        var tx=new GameObject("Txt"); tx.transform.SetParent(cv.transform,false);
        var t=tx.AddComponent<UnityEngine.UI.Text>();
        t.text="< Controls >\n\n[Quest 3]\nX=Toggle  Stick=Move  Y=Drop\n\n[Keyboard]\n1=Toggle  WASD=Move  2=Drop";
        t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize=28; t.color=Color.white; t.alignment=TextAnchor.MiddleCenter;
        var trt=tx.GetComponent<RectTransform>();
        trt.anchorMin=V2(.05f,.05f); trt.anchorMax=V2(.95f,.95f); trt.offsetMin=V2(0,0); trt.offsetMax=V2(0,0);
    }

    // =========================== 유틸 ===========================
    static Vector3 V(float x,float y,float z)=>new(x,y,z);
    static Vector2 V2(float x,float y)=>new(x,y);
    static readonly Quaternion Q0=Quaternion.identity;

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
        foreach(var d in new[]{RM,CM,FM,CuM,PM,DM,
            "Assets/00.Main/Art/Shared/Materials"}){ // Shared 잔여 .mat도 삭제
            if(!AssetDatabase.IsValidFolder(d))continue;
            foreach(var g in AssetDatabase.FindAssets("t:Material",new[]{d}))
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(g));
        }
        AssetDatabase.Refresh();
    }
}
