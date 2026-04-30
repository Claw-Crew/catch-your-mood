using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// 메뉴 > Claw Crew > Build Main Scene 으로 씬을 생성한다.
/// 생성 후 씬에 저장되므로 이후에는 열기만 하면 된다.
///
/// scene_background_research.md 기준:
/// - 방: 5.5x5x3m 카페풍, 세이지 그린 벽, 웜 베이지 바닥
/// - 기계: 78x78x200cm
/// - 조명: 따뜻한 앰버 3000-4000K
/// - XR Origin: Instantiate로 자식 구조 보존
/// </summary>
public static class SceneSetup
{
    const string SceneDir = "Assets/00.Main/Scenes/Main";
    const string ScenePath = "Assets/00.Main/Scenes/Main/MainScene.unity";
    const string SharedMatDir = "Assets/00.Main/Art/Shared/Materials";
    const string ClawMatDir = "Assets/00.Main/Art/ClawMachine/Materials";

    // 방 치수 (scene_background_research.md 3.5절)
    const float RW = 5.5f, RD = 5f, RH = 3f, WT = 0.1f;
    // 기계 치수 (character_design_brief.md)
    const float MW = 0.78f, MD = 0.78f, MH = 2f, BH = 0.15f, TH = 0.25f, PH = 0.8f, FT = 0.04f;
    static readonly Vector3 MPos = new(0, 0, 0.3f);

    // 색상 (scene_background_research.md 3.6절)
    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
    static readonly Color CWall = Hex("C5D5CB"), CFloor = Hex("D4C4A8"), CCeil = Hex("E8E4DC");
    static readonly Color CWood = Hex("8C6E53"), CAccent = Hex("B8A090");
    static readonly Color CFrame = Hex("F0EDE8"), CMetal = Hex("BFBFC3"), CLed = Hex("FFE3C4");
    static readonly Color CPanel = Hex("D9D1C8"), CJoy = Hex("4D4D59"), CBtn = Hex("F25959");

    [MenuItem("Claw Crew/Build Main Scene", false, 1)]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BuildRoom();
        BuildMachine();
        BuildLighting();
        BuildDolls();
        BuildXROrigin(); // 변경: Instantiate로 자식 구조 보존
        BuildUI();
        if (!Directory.Exists(SceneDir)) Directory.CreateDirectory(SceneDir);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuild(ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[CatchYourMood] Main Scene 생성 완료: " + ScenePath);
    }

    [MenuItem("Claw Crew/Open Main Scene", false, 2)]
    public static void Open()
    {
        if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
        else Debug.LogWarning("MainScene 없음. Build Main Scene 먼저 실행.");
    }

    // ===================== 방 =====================
    static void BuildRoom()
    {
        var r = new GameObject("Room");
        var mW = Mat(SharedMatDir, "M_Wall", CWall, 0.2f);
        var mF = Mat(SharedMatDir, "M_Floor", CFloor, 0.3f);
        var mC = Mat(SharedMatDir, "M_Ceiling", CCeil, 0.15f);
        var mWd = Mat(SharedMatDir, "M_Wood", CWood, 0.4f);

        // 바닥, 천장
        Box("Floor", r, new(0, -WT/2, 0), new(RW, WT, RD), mF);
        Box("Ceiling", r, new(0, RH+WT/2, 0), new(RW, WT, RD), mC);
        // 벽 4면
        Box("Wall_Back", r, new(0, RH/2, RD/2+WT/2), new(RW+WT*2, RH, WT), mW);
        Box("Wall_Front", r, new(0, RH/2, -RD/2-WT/2), new(RW+WT*2, RH, WT), mW);
        Box("Wall_Left", r, new(-RW/2-WT/2, RH/2, 0), new(WT, RH, RD), mW);
        Box("Wall_Right", r, new(RW/2+WT/2, RH/2, 0), new(WT, RH, RD), mW);
        // 걸레받이
        float bh = 0.08f;
        Box("Base_B", r, new(0, bh/2, RD/2-0.005f), new(RW, bh, 0.01f), mWd);
        Box("Base_F", r, new(0, bh/2, -RD/2+0.005f), new(RW, bh, 0.01f), mWd);
        Box("Base_L", r, new(-RW/2+0.005f, bh/2, 0), new(0.01f, bh, RD), mWd);
        Box("Base_R", r, new(RW/2-0.005f, bh/2, 0), new(0.01f, bh, RD), mWd);
        // 창문 (뒷벽)
        float wW=1.6f, wH=1.2f, wB=1.1f, wZ=RD/2-0.005f;
        Box("WinFrame_T", r, new(0, wB+wH+0.025f, wZ), new(wW+0.1f, 0.05f, 0.03f), mWd);
        Box("WinFrame_B", r, new(0, wB-0.025f, wZ), new(wW+0.1f, 0.05f, 0.04f), mWd);
        Box("WinFrame_L", r, new(-wW/2-0.025f, wB+wH/2, wZ), new(0.05f, wH, 0.03f), mWd);
        Box("WinFrame_R", r, new(wW/2+0.025f, wB+wH/2, wZ), new(0.05f, wH, 0.03f), mWd);
        Box("WinGlass", r, new(0, wB+wH/2, wZ+0.01f), new(wW, wH, 0.005f),
            GlassMat(SharedMatDir, "M_WinGlass", new(0.85f,0.92f,1,0.2f)));
        // 러그
        Box("Rug", r, new(0, 0.005f, -0.5f), new(2, 0.01f, 1.5f), Mat(SharedMatDir, "M_Rug", Hex("D9CFC2"), 0.15f));
    }

    // ===================== 기계 =====================
    static void BuildMachine()
    {
        var r = new GameObject("ClawMachine");
        r.transform.position = MPos;
        float gH = MH-BH-TH;
        var mFr = Mat(ClawMatDir, "M_Frame", CFrame, 0.3f);
        var mMe = Mat(ClawMatDir, "M_Metal", CMetal, 0.6f);
        var mGl = GlassMat(ClawMatDir, "M_Glass", new(0.9f,0.95f,1,0.15f));
        var mLd = Mat(ClawMatDir, "M_LED", CLed, 0.8f); Emit(mLd, CLed*1.5f);
        var mBt = Mat(ClawMatDir, "M_Button", CBtn, 0.6f); Emit(mBt, CBtn*0.3f);
        var mJs = Mat(ClawMatDir, "M_Joystick", CJoy, 0.5f);
        var mPf = Mat(ClawMatDir, "M_PrizeFloor", Hex("EDE9E2"), 0.3f);

        // 베이스
        Box("Base", r, new(0, BH/2, 0), new(MW, BH, MD), mFr);
        // 기둥 4개
        float pH = MH-BH, hW = MW/2-FT/2, hD = MD/2-FT/2;
        Vector3[] corners = { new(-hW, BH+pH/2, -hD), new(hW, BH+pH/2, -hD), new(-hW, BH+pH/2, hD), new(hW, BH+pH/2, hD) };
        for (int i = 0; i < 4; i++) Box($"Pillar_{i}", r, corners[i], new(FT, pH, FT), mFr);
        // 상단 프레임
        float tY = MH-TH/2;
        Box("TopF_F", r, new(0, tY, -hD), new(MW, FT, FT), mFr);
        Box("TopF_B", r, new(0, tY, hD), new(MW, FT, FT), mFr);
        Box("TopF_L", r, new(-hW, tY, 0), new(FT, FT, MD), mFr);
        Box("TopF_R", r, new(hW, tY, 0), new(FT, FT, MD), mFr);
        // 유리
        float gY = BH+gH/2, gW = MW-FT*2, gD = MD-FT*2;
        Box("Glass_F", r, new(0, gY, -MD/2+0.003f), new(gW, gH-FT, 0.005f), mGl);
        Box("Glass_L", r, new(-MW/2+0.003f, gY, 0), new(0.005f, gH-FT, gD), mGl);
        Box("Glass_R", r, new(MW/2-0.003f, gY, 0), new(0.005f, gH-FT, gD), mGl);
        Box("Panel_Back", r, new(0, gY, MD/2-0.003f), new(gW, gH-FT, 0.01f), mFr);
        // 상단 하우징
        Box("TopHousing", r, new(0, MH-TH/2, 0), new(MW-0.01f, TH, MD-0.01f), mFr);
        // 레일
        float rY = MH-TH-0.02f, rR = 0.012f;
        float iW = MW-FT*2-0.04f, iD = MD-FT*2-0.04f;
        Cyl("Rail_Z_L", r, new(-iW/2, rY, 0), Quaternion.Euler(90,0,0), new(rR*2, iD/2, rR*2), mMe, true);
        Cyl("Rail_Z_R", r, new(iW/2, rY, 0), Quaternion.Euler(90,0,0), new(rR*2, iD/2, rR*2), mMe, true);
        var xRail = Cyl("Rail_X", r, new(0, rY, 0), Quaternion.Euler(0,0,90), new(rR*2, iW/2, rR*2), mMe, false);
        var carr = Box("Carriage", xRail, Vector3.zero, new(0.06f, 0.04f, 0.06f), mMe, false);
        // 집게
        var clawR = new GameObject("ClawAssembly"); clawR.transform.SetParent(carr.transform); clawR.transform.localPosition = Vector3.zero;
        float rpL = 0.4f;
        Cyl("Rope", clawR, new(0, -rpL/2-0.02f, 0), Quaternion.identity, new(0.006f, rpL/2, 0.006f), mMe, false);
        var hub = new GameObject("ClawHub"); hub.transform.SetParent(clawR.transform); hub.transform.localPosition = new(0, -rpL-0.04f, 0);
        Box("HubBody", hub, Vector3.zero, new(0.04f, 0.03f, 0.04f), mMe, false);
        float fL = 0.08f, fW = 0.015f;
        for (int i = 0; i < 3; i++)
        {
            float a = i*120f, rad = a*Mathf.Deg2Rad;
            var fin = new GameObject($"Finger_{i}"); fin.transform.SetParent(hub.transform);
            fin.transform.localPosition = new(Mathf.Sin(rad)*0.015f, -0.015f, Mathf.Cos(rad)*0.015f);
            fin.transform.localRotation = Quaternion.Euler(0, a, 15f);
            var u = GameObject.CreatePrimitive(PrimitiveType.Cube); u.name = "Upper"; u.transform.SetParent(fin.transform);
            u.transform.localPosition = new(0, -fL*0.3f, 0); u.transform.localScale = new(fW, fL*0.6f, fW);
            u.GetComponent<Renderer>().sharedMaterial = mMe;
            var lo = GameObject.CreatePrimitive(PrimitiveType.Cube); lo.name = "Lower"; lo.transform.SetParent(fin.transform);
            lo.transform.localPosition = new(0, -fL*0.75f, 0); lo.transform.localRotation = Quaternion.Euler(0,0,-20f);
            lo.transform.localScale = new(fW, fL*0.35f, fW); lo.GetComponent<Renderer>().sharedMaterial = mMe;
        }
        // 인형 바닥
        var pa = new GameObject("PrizeArea"); pa.transform.SetParent(r.transform); pa.transform.localPosition = new(0, BH, 0);
        var pf = GameObject.CreatePrimitive(PrimitiveType.Plane); pf.name = "PrizeFloor"; pf.transform.SetParent(pa.transform);
        pf.transform.localPosition = new(0, 0.005f, 0); pf.transform.localScale = new((MW-FT*2)/10, 1, (MD-FT*2)/10);
        pf.GetComponent<Renderer>().sharedMaterial = mPf; pf.isStatic = true;
        // 투출구 구멍
        float half = (MW/2)-FT-0.05f;
        float holeX = half*0.8f, holeZ = -half*0.8f;
        Box("DropHole", pa, new(holeX, 0.003f, holeZ), new(0.16f, 0.002f, 0.16f), Mat(ClawMatDir, "M_Hole", new(0.15f,0.15f,0.15f,1), 0.1f));
        var mHR = Mat(ClawMatDir, "M_HoleRim", Hex("FFD37A"), 0.4f); Emit(mHR, Hex("FFD37A")*0.8f);
        Box("DropHoleRim", pa, new(holeX, 0.004f, holeZ), new(0.19f, 0.001f, 0.19f), mHR);
        // 투출구 아래 꺼내기 공간
        var bin = new GameObject("PrizePickup"); bin.transform.SetParent(r.transform);
        bin.transform.localPosition = new(holeX, 0, -MD/2-0.12f);
        float bW=0.35f, bH2=0.25f, bD=0.25f;
        Box("Bin_Floor", bin, new(0, 0.01f, 0), new(bW, 0.02f, bD), mFr);
        Box("Bin_L", bin, new(-bW/2, bH2/2, 0), new(0.02f, bH2, bD), mFr);
        Box("Bin_R", bin, new(bW/2, bH2/2, 0), new(0.02f, bH2, bD), mFr);
        Box("Bin_Back", bin, new(0, bH2/2, bD/2), new(bW, bH2, 0.02f), mFr);
        // 컨트롤 패널
        var pn = new GameObject("ControlPanel"); pn.transform.SetParent(r.transform);
        pn.transform.localPosition = new(-MW*0.15f, BH+PH*0.55f, -MD/2);
        Box("PanelBoard", pn, new(0, 0, -0.03f), new(0.25f, 0.08f, 0.06f), Mat(ClawMatDir, "M_Panel", CPanel, 0.3f));
        Cyl("JsBase", pn, new(-0.04f, 0.02f, -0.06f), Quaternion.identity, new(0.04f, 0.01f, 0.04f), mJs, true);
        Cyl("JsStick", pn, new(-0.04f, 0.05f, -0.06f), Quaternion.identity, new(0.012f, 0.03f, 0.012f), mJs, true);
        var jb = GameObject.CreatePrimitive(PrimitiveType.Sphere); jb.name = "JsBall"; jb.transform.SetParent(pn.transform);
        jb.transform.localPosition = new(-0.04f, 0.08f, -0.06f); jb.transform.localScale = Vector3.one*0.03f;
        jb.GetComponent<Renderer>().sharedMaterial = mJs;
        Cyl("GrabBtn", pn, new(0.06f, 0.02f, -0.06f), Quaternion.identity, new(0.045f, 0.012f, 0.045f), mBt, true);
        // LED
        float lY = MH-TH-0.01f, lHW = MW/2-0.01f, lHD = MD/2-0.01f;
        LEDStrip("LED_F", r, new(0, lY, -lHD), MW-0.04f, true, mLd);
        LEDStrip("LED_L", r, new(-lHW, lY, 0), MD-0.04f, false, mLd);
        LEDStrip("LED_R", r, new(lHW, lY, 0), MD-0.04f, false, mLd);
        // 내부 조명
        var il = new GameObject("Light_Interior"); il.transform.SetParent(r.transform);
        il.transform.localPosition = new(0, MH-TH-0.05f, 0); il.transform.localRotation = Quaternion.Euler(90,0,0);
        var lt = il.AddComponent<Light>(); lt.type = LightType.Spot; lt.color = new(1, 0.95f, 0.9f);
        lt.intensity = 1.2f; lt.range = 2; lt.spotAngle = 100; lt.shadows = LightShadows.None;
        // 간판
        var mSn = Mat(ClawMatDir, "M_Sign", Hex("F2E6D0"), 0.4f); Emit(mSn, new Color(1, 0.95f, 0.85f)*0.5f);
        Box("Signage", r, new(0, MH+0.1f, -MD/2+0.02f), new(MW*0.8f, 0.15f, 0.03f), mSn);
    }

    static void LEDStrip(string n, GameObject p, Vector3 c, float len, bool isX, Material m)
    {
        int cnt = Mathf.FloorToInt(len/0.06f); float sp = len/cnt;
        for (int i = 0; i < cnt; i++)
        {
            float off = -len/2+sp*(i+0.5f); var pos = c;
            if (isX) pos.x += off; else pos.z += off;
            var led = GameObject.CreatePrimitive(PrimitiveType.Sphere); led.name = $"{n}_{i}";
            led.transform.SetParent(p.transform); led.transform.localPosition = pos;
            led.transform.localScale = Vector3.one*0.015f; led.GetComponent<Renderer>().sharedMaterial = m; led.isStatic = true;
        }
    }

    // ===================== 조명 =====================
    static void BuildLighting()
    {
        var r = new GameObject("Lighting");
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new(0.55f, 0.50f, 0.45f);
        RenderSettings.skybox = null;
        // 메인
        var ml = new GameObject("Light_Main"); ml.transform.SetParent(r.transform);
        ml.transform.position = new(0, RH-0.3f, -0.5f);
        var l1 = ml.AddComponent<Light>(); l1.type = LightType.Point; l1.color = new(1, 0.92f, 0.82f);
        l1.intensity = 1.5f; l1.range = 8; l1.shadows = LightShadows.Soft;
        // 창문
        var wl = new GameObject("Light_Window"); wl.transform.SetParent(r.transform);
        wl.transform.position = new(0, 2, RD/2-0.3f); wl.transform.rotation = Quaternion.Euler(40, 180, 0);
        var l2 = wl.AddComponent<Light>(); l2.type = LightType.Spot; l2.color = new(1, 0.96f, 0.88f);
        l2.intensity = 0.8f; l2.range = 6; l2.spotAngle = 80; l2.shadows = LightShadows.None;
        // 보조
        var fl = new GameObject("Light_Fill"); fl.transform.SetParent(r.transform);
        fl.transform.position = new(-2, 2, -1);
        var l3 = fl.AddComponent<Light>(); l3.type = LightType.Point; l3.color = new(0.95f, 0.90f, 0.85f);
        l3.intensity = 0.5f; l3.range = 5; l3.shadows = LightShadows.None;
    }

    // ===================== 더미 인형 =====================
    static void BuildDolls()
    {
        var machine = GameObject.Find("ClawMachine");
        if (machine == null) return;
        var dp = new GameObject("DummyDolls"); dp.transform.SetParent(machine.transform); dp.transform.localPosition = Vector3.zero;
        var dolls = new[] {
            ("부글", "C43131", new Vector3(0.12f, 0.11f, 0.12f), 1.2f),
            ("쪼잉", "C5DB77", new Vector3(0.09f, 0.15f, 0.09f), 0.6f),
            ("뽕뽕", "FF8A5C", new Vector3(0.13f, 0.13f, 0.13f), 0.8f),
            ("시무룩", "8B9BBF", new Vector3(0.11f, 0.13f, 0.11f), 1.4f),
            ("꾸벅", "D6CFF0", new Vector3(0.15f, 0.13f, 0.15f), 1.6f),
            ("몽글", "C8E4D4", new Vector3(0.12f, 0.12f, 0.12f), 1.0f),
        };
        float half = (MW/2)-FT-0.05f;
        for (int i = 0; i < dolls.Length; i++)
        {
            var (name, hex, size, mass) = dolls[i];
            int col = i%3, row = i/3;
            float x = (col-1)*half*0.7f, z = (row==0?-1:1)*half*0.4f, y = BH+0.01f+size.y/2;
            var m = Mat(ClawMatDir, $"M_Doll_{name}", Hex(hex), 0.15f);
            var d = GameObject.CreatePrimitive(PrimitiveType.Cube); d.name = name;
            d.transform.SetParent(dp.transform); d.transform.localPosition = new(x, y, z);
            d.transform.localScale = size; d.GetComponent<Renderer>().sharedMaterial = m;
            var rb = d.AddComponent<Rigidbody>(); rb.mass = mass; rb.linearDamping = 1; rb.angularDamping = 0.5f;
        }
    }

    // ===================== XR Origin =====================
    // 변경: PrefabUtility.InstantiatePrefab → Object.Instantiate
    // 이유: InstantiatePrefab이 자식 구조를 보존하지 못하는 문제 발생.
    //       Object.Instantiate는 프리팹 링크는 끊어지지만 자식 구조(Camera Offset,
    //       Left Controller, Right Controller 등)를 완전히 복사한다.
    static void BuildXROrigin()
    {
        string path = "Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null)
        {
            var xr = Object.Instantiate(prefab);
            xr.name = "XR Origin (XR Rig)"; // Instantiate가 "(Clone)" 붙이는 것 제거
            xr.transform.position = new(0, 0, -1.0f);
            xr.transform.rotation = Quaternion.LookRotation(MPos - new Vector3(0, 0, -1.0f));
            Debug.Log("[CatchYourMood] XR Origin (XR Rig) 배치 완료 (Instantiate).");
        }
        else
        {
            Debug.LogWarning("[CatchYourMood] XR Origin 프리팹 없음: " + path);
            var cam = new GameObject("FallbackCamera");
            cam.transform.position = new(0, 1.6f, -1.0f); cam.transform.LookAt(new Vector3(MPos.x, 1.2f, MPos.z));
            cam.tag = "MainCamera"; var c = cam.AddComponent<Camera>(); c.nearClipPlane = 0.05f; c.fieldOfView = 70;
            cam.AddComponent<AudioListener>();
        }
    }

    // ===================== UI =====================
    static void BuildUI()
    {
        var cv = new GameObject("InstructionUI"); var canvas = cv.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = cv.GetComponent<RectTransform>(); rt.sizeDelta = new(600, 400);
        cv.transform.position = new(-RW/2+0.12f, 1.5f, -0.3f);
        cv.transform.rotation = Quaternion.Euler(0, -90, 0);
        cv.transform.localScale = Vector3.one * 0.001f;
        var bg = new GameObject("BG"); bg.transform.SetParent(cv.transform, false);
        var img = bg.AddComponent<UnityEngine.UI.Image>(); img.color = new(0.15f, 0.15f, 0.15f, 0.85f);
        var bgRt = bg.GetComponent<RectTransform>(); bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
        var tx = new GameObject("Text"); tx.transform.SetParent(cv.transform, false);
        var t = tx.AddComponent<UnityEngine.UI.Text>();
        t.text = "< 조작법 >\n\n[Quest 3]\nX=모드전환  스틱=조작  Y=하강\n\n[키보드 (Simulator)]\n1=모드전환  WASD=조작  2=하강";
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 28; t.color = Color.white; t.alignment = TextAnchor.MiddleCenter;
        var txRt = tx.GetComponent<RectTransform>(); txRt.anchorMin = new(0.05f, 0.05f); txRt.anchorMax = new(0.95f, 0.95f);
        txRt.offsetMin = Vector2.zero; txRt.offsetMax = Vector2.zero;
    }

    // ===================== 유틸 =====================
    static GameObject Box(string n, GameObject p, Vector3 pos, Vector3 sc, Material m, bool s = true)
    {
        var o = GameObject.CreatePrimitive(PrimitiveType.Cube); o.name = n;
        o.transform.SetParent(p.transform); o.transform.localPosition = pos; o.transform.localScale = sc;
        o.GetComponent<Renderer>().sharedMaterial = m; o.isStatic = s; return o;
    }
    static GameObject Cyl(string n, GameObject p, Vector3 pos, Quaternion rot, Vector3 sc, Material m, bool s)
    {
        var o = GameObject.CreatePrimitive(PrimitiveType.Cylinder); o.name = n;
        o.transform.SetParent(p.transform); o.transform.localPosition = pos; o.transform.localRotation = rot;
        o.transform.localScale = sc; o.GetComponent<Renderer>().sharedMaterial = m; o.isStatic = s; return o;
    }
    static Material Mat(string dir, string name, Color c, float smooth)
    {
        string path = $"{dir}/{name}.mat";
        var ex = AssetDatabase.LoadAssetAtPath<Material>(path); if (ex) return ex;
        EnsureDir(dir);
        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(sh); m.name = name; m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth);
        AssetDatabase.CreateAsset(m, path); return m;
    }
    static Material GlassMat(string dir, string name, Color c)
    {
        string path = $"{dir}/{name}.mat";
        var ex = AssetDatabase.LoadAssetAtPath<Material>(path); if (ex) return ex;
        EnsureDir(dir);
        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(sh); m.name = name; m.SetFloat("_Surface", 1); m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.95f); m.renderQueue = 3000;
        m.SetOverrideTag("RenderType", "Transparent"); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        AssetDatabase.CreateAsset(m, path); return m;
    }
    static void Emit(Material m, Color c) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c); }
    static void EnsureDir(string p)
    {
        if (AssetDatabase.IsValidFolder(p)) return;
        var parts = p.Split('/'); string cur = parts[0];
        for (int i = 1; i < parts.Length; i++) { string next = cur+"/"+parts[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]); cur = next; }
    }
    static void AddToBuild(string p)
    {
        var ss = EditorBuildSettings.scenes; foreach (var s in ss) if (s.path == p) return;
        var ns = new EditorBuildSettingsScene[ss.Length+1]; ss.CopyTo(ns, 0);
        ns[ss.Length] = new EditorBuildSettingsScene(p, true); EditorBuildSettings.scenes = ns;
    }
}
