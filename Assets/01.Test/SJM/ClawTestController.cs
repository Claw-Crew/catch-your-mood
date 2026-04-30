using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 인형뽑기 집게 컨트롤러.
///
/// 모든 입력은 XR Interaction Simulator가 키보드를 가상 컨트롤러로 변환한 값을 읽는다.
/// XRSimulatedController 디바이스에서 직접 읽는다.
///
/// [Quest 3]  X=모드전환  스틱=조작  Y=하강
/// [키보드]   1=모드전환  WASD=조작  2=하강 (Simulator 경유)
///
/// 실제 인형뽑기 동작:
/// 이동 → 하강 → 잡기 → 상승 → 투출구 이동 → 놓기 → 복귀
/// </summary>
public class ClawTestController : MonoBehaviour
{
    // 씬 시작 시 자동 생성
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Object.FindAnyObjectByType<ClawTestController>() != null) return;
        if (GameObject.Find("ClawMachine") == null) return;
        new GameObject("_ClawController").AddComponent<ClawTestController>();
    }

    // --- 참조 ---
    Transform railX, carriage, claw, rope;
    Transform[] fingers;
    float xMin, xMax, zMin, zMax, dropY, chuteX, chuteZ;
    Vector3 ropeScale0; float ropeY0, ropeLen0;

    // --- 상태 ---
    enum S { Idle, Drop, Grab, Lift, ToChute, Release, Reset }
    S state = S.Idle;
    float fa = 15f;
    bool ok, clawMode;
    MonoBehaviour[] locoComps;

    // --- XRSimulatedController 참조 ---
    // 변경 이유: InputAction이나 InputDevices API 대신 XRSimulatedController 디바이스를 직접 참조.
    // XR Interaction Simulator가 이 디바이스에 값을 쓰므로 확실하게 읽을 수 있다.
    UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRSimulatedController leftCtrl, rightCtrl;
    bool prevPrimary;

    const float MOVE = 0.4f, DROP_S = 0.6f, LIFT_S = 0.4f, RET = 0.5f;
    const float FO = 15f, FC = -5f, FS = 100f;

    void Start()
    {
        // Locomotion 컴포넌트 캐시 (모드 전환 시 토글)
        var list = new System.Collections.Generic.List<MonoBehaviour>();
        foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (mb == null || mb == this) continue;
            var n = mb.GetType().Name;
            if (n.Contains("MoveProvider") || n.Contains("TurnProvider") ||
                n.Contains("SnapTurn") || n.Contains("ContinuousMove") ||
                n.Contains("ContinuousTurn") || n.Contains("DynamicMove"))
                list.Add(mb);
        }
        locoComps = list.ToArray();

        // Jump 비활성화 — Both 모드에서 1키가 양쪽 Primary를 누르므로 오른쪽 A=Jump 발동 방지
        foreach (var asset in Resources.FindObjectsOfTypeAll<InputActionAsset>())
        {
            var map = asset.FindActionMap("XRI Right Locomotion");
            if (map != null) { var j = map.FindAction("Jump"); if (j != null) j.Disable(); }
        }

        // 집게 찾기
        var m = GameObject.Find("ClawMachine"); if (!m) return;
        railX = m.transform.Find("Rail_X"); if (railX) carriage = railX.Find("Carriage");
        if (carriage) claw = carriage.Find("ClawAssembly"); if (!claw) return;
        rope = claw.Find("Rope");
        if (rope) { ropeScale0 = rope.localScale; ropeY0 = rope.localPosition.y; ropeLen0 = ropeScale0.y * 2f; }
        var hub = claw.Find("ClawHub");
        if (hub) { fingers = new Transform[3]; for (int i = 0; i < 3; i++) fingers[i] = hub.Find($"Finger_{i}"); }
        float h = (0.78f/2)-0.08f;
        xMin = -h; xMax = h; zMin = -h; zMax = h;
        dropY = -(2f-0.15f-0.25f-0.8f-0.15f);
        chuteX = h*0.8f; chuteZ = -h*0.8f;
        ok = true;
    }

    void SetClawMode(bool c)
    {
        clawMode = c;
        if (locoComps != null)
            foreach (var lc in locoComps) if (lc) lc.enabled = !c;
    }

    void Update()
    {
        if (!ok) return;

        // XRSimulatedController 찾기 (매 프레임 시도 — Simulator가 나중에 생성할 수 있으므로)
        if (leftCtrl == null || rightCtrl == null)
        {
            foreach (var dev in InputSystem.devices)
            {
                if (dev is UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRSimulatedController sc)
                {
                    if (dev.name == "XRSimulatedController") leftCtrl = sc;
                    else if (dev.name == "XRSimulatedController1") rightCtrl = sc;
                }
            }
        }

        // --- 모드 전환: 왼쪽 Primary (X/1키) ---
        bool primary = leftCtrl != null && leftCtrl.primaryButton.isPressed;
        if (primary && !prevPrimary) SetClawMode(!clawMode);
        prevPrimary = primary;

        if (!clawMode) { UpdateRope(); return; }

        // --- 하강: 왼쪽 Secondary (Y/2키) ---
        bool drop = leftCtrl != null && leftCtrl.secondaryButton.wasPressedThisFrame;

        // --- 스틱: 왼쪽 Primary2DAxis (WASD) ---
        Vector2 stick = leftCtrl != null ? leftCtrl.primary2DAxis.ReadValue() : Vector2.zero;
        float dx = stick.x, dz = stick.y;

        // --- 상태머신 ---
        switch (state)
        {
            case S.Idle:
                if (railX) { var rp = railX.localPosition; rp.z = Mathf.Clamp(rp.z+dz*MOVE*Time.deltaTime, zMin, zMax); railX.localPosition = rp; }
                if (carriage) { var cp = carriage.localPosition; cp.x = Mathf.Clamp(cp.x+dx*MOVE*Time.deltaTime, xMin, xMax); carriage.localPosition = cp; }
                if (drop) state = S.Drop;
                break;
            case S.Drop:
                var p1 = claw.localPosition; p1.y -= DROP_S*Time.deltaTime;
                if (p1.y <= dropY) { p1.y = dropY; state = S.Grab; } claw.localPosition = p1;
                break;
            case S.Grab:
                fa = Mathf.MoveTowards(fa, FC, FS*Time.deltaTime); SetFingers();
                if (Mathf.Approximately(fa, FC)) state = S.Lift;
                break;
            case S.Lift:
                var p2 = claw.localPosition; p2.y += LIFT_S*Time.deltaTime;
                if (p2.y >= 0) { p2.y = 0; state = S.ToChute; } claw.localPosition = p2;
                break;
            case S.ToChute:
                if (MoveTo(railX, 2, chuteZ) && MoveTo(carriage, 0, chuteX)) state = S.Release;
                break;
            case S.Release:
                fa = Mathf.MoveTowards(fa, FO, FS*Time.deltaTime); SetFingers();
                if (Mathf.Approximately(fa, FO)) state = S.Reset;
                break;
            case S.Reset:
                if (MoveTo(railX, 2, 0) && MoveTo(carriage, 0, 0)) state = S.Idle;
                break;
        }
        UpdateRope();
    }

    void UpdateRope()
    {
        if (!rope || !claw) return;
        float d = Mathf.Abs(claw.localPosition.y);
        rope.localScale = new(ropeScale0.x, (ropeLen0+d)/2, ropeScale0.z);
        rope.localPosition = new(0, ropeY0+d/2, 0);
    }
    bool MoveTo(Transform t, int ax, float tgt)
    {
        if (!t) return true; var p = t.localPosition;
        p[ax] = Mathf.MoveTowards(p[ax], tgt, RET*Time.deltaTime);
        t.localPosition = p; return Mathf.Abs(p[ax]-tgt) < 0.001f;
    }
    void SetFingers()
    {
        if (fingers == null) return;
        for (int i = 0; i < fingers.Length; i++)
            if (fingers[i]) fingers[i].localRotation = Quaternion.Euler(0, i*120, fa);
    }

    // --- UI ---
    GUIStyle gs, gsS;
    void OnGUI()
    {
        if (gs == null) { gs = new(GUI.skin.label) { fontSize=15, fontStyle=FontStyle.Bold }; gs.normal.textColor = Color.white; gsS = new(GUI.skin.label) { fontSize=12 }; gsS.normal.textColor = new(0.8f,0.8f,0.8f); }
        string mode = clawMode ? "집게 모드" : "이동 모드";
        string s = clawMode ? state switch { S.Drop=>" [하강]", S.Grab=>" [잡기]", S.Lift=>" [상승]", S.ToChute=>" [투출구]", S.Release=>" [놓기]", S.Reset=>" [복귀]", _=>"" } : "";
        GUI.color = new(0,0,0,0.75f); GUI.DrawTexture(new Rect(8,8,310,78), Texture2D.whiteTexture); GUI.color = Color.white;
        GUI.Label(new Rect(12,12,400,22), $"{mode}{s}", gs);
        GUI.Label(new Rect(12,32,400,18), "키보드: 1=전환  WASD=조작  2=하강", gsS);
        GUI.Label(new Rect(12,48,400,18), "Quest3: X=전환  스틱=조작  Y=하강", gsS);
    }
}
