using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 인형뽑기 집게 컨트롤러.
///
/// 입력 읽기 방식:
/// - 모드 전환/하강: "XR Interaction Controller Controls" Asset의 "Primary Button"/"Secondary Button"
///   (이전 디버그에서 동작 확인됨)
/// - 스틱(집게 이동): Locomotion의 MoveProvider가 참조하는 InputAction을 런타임에 찾아서 읽음
///   (이동이 되므로 이 액션에는 값이 들어오고 있음)
///
/// [Quest 3]  X=모드전환  스틱=조작  Y=하강
/// [키보드]   1=모드전환  WASD=조작  2=하강
/// </summary>
public class ClawTestController : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Object.FindAnyObjectByType<ClawTestController>() != null) return;
        if (GameObject.Find("ClawMachine") == null) return;
        new GameObject("_ClawController").AddComponent<ClawTestController>();
    }

    Transform railX, carriage, claw, rope;
    Transform[] fingers;
    float xMin, xMax, zMin, zMax, dropY, chuteX, chuteZ;
    Vector3 ropeScale0; float ropeY0, ropeLen0;

    enum S { Idle, Drop, Grab, Lift, ToChute, Release, Reset }
    S state = S.Idle;
    float fa = 15f;
    bool ok, clawMode;
    MonoBehaviour[] locoComps;
    float savedMoveSpeed = 2.5f; // MoveProvider의 원래 속도 저장

    // 입력 액션
    InputAction toggleAction;  // Primary Button (X/1키) — 모드 전환
    InputAction dropAction;    // Secondary Button (Y/2키) — 하강
    // Move 입력: MoveProvider의 leftHandMoveInput (XRInputValueReader)에서 직접 읽음.
    // InputAction으로는 값을 읽을 수 없었음 (다른 인스턴스 문제).
    MonoBehaviour moveProvider; // ContinuousMoveProvider 참조
    System.Reflection.MethodInfo readMoveMethod; // leftHandMoveInput.ReadValue() 호출용
    object leftHandMoveInput; // XRInputValueReader<Vector2> 인스턴스
    bool prevToggle;

    const float MOVE = 0.4f, DROP_S = 0.6f, LIFT_S = 0.4f, RET = 0.5f;
    const float FO = 15f, FC = -5f, FS = 100f;

    void Start()
    {
        // Game view 포커스가 없어도 입력을 받도록 설정
        // Console/Inspector 클릭 시 포커스가 빠져서 입력이 안 들어오는 문제 해결
        Application.runInBackground = true;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

        // --- 버튼 액션: "XR Interaction Controller Controls" Asset에서 찾기 ---
        // 이전 디버그에서 이 방식으로 모드 전환이 동작하는 것을 확인했다.
        foreach (var asset in Resources.FindObjectsOfTypeAll<InputActionAsset>())
        {
            if (asset.name != "XR Interaction Controller Controls") continue;
            var map = asset.FindActionMap("Controller");
            if (map == null) continue;
            toggleAction = map.FindAction("Primary Button");
            dropAction = map.FindAction("Secondary Button");
            break;
        }

        // --- 스틱 입력: MoveProvider의 leftHandMoveInput에서 직접 읽기 ---
        // InputAction으로는 값을 읽을 수 없었음 (이동 모드에서도 (0,0)).
        // MoveProvider는 XRInputValueReader<Vector2>를 통해 내부적으로 값을 읽으므로,
        // 그 Reader에서 직접 ReadValue()를 호출해야 한다.
        foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (mb == null) continue;
            var t = mb.GetType();
            if (!t.Name.Contains("MoveProvider")) continue;

            var prop = t.GetProperty("leftHandMoveInput");
            if (prop == null) continue;

            leftHandMoveInput = prop.GetValue(mb);
            if (leftHandMoveInput == null) continue;

            // XRInputValueReader<Vector2>.ReadValue() 메서드 참조
            readMoveMethod = leftHandMoveInput.GetType().GetMethod("ReadValue", System.Type.EmptyTypes);
            if (readMoveMethod != null)
            {
                moveProvider = mb;
                Debug.Log($"[Claw] MoveProvider '{t.Name}'에서 leftHandMoveInput 참조 완료.");
                break;
            }
        }

        // --- Locomotion 캐시 ---
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

        // --- Jump 비활성화 ---
        foreach (var mgr in FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager>(FindObjectsSortMode.None))
        {
            foreach (var asset in mgr.actionAssets)
            {
                var map = asset.FindActionMap("XRI Right Locomotion");
                if (map != null) { var j = map.FindAction("Jump"); if (j != null) j.Disable(); }
            }
        }

        // --- 집게 찾기 ---
        var m = GameObject.Find("ClawMachine"); if (!m) return;
        railX = m.transform.Find("Rail_X"); if (railX) carriage = railX.Find("Carriage");
        if (carriage) claw = carriage.Find("ClawAssembly"); if (!claw) return;
        rope = claw.Find("Rope");
        if (rope) { ropeScale0 = rope.localScale; ropeY0 = rope.localPosition.y; ropeLen0 = ropeScale0.y * 2f; }
        var hub = claw.Find("ClawHub");
        if (hub) { fingers = new Transform[3]; for (int i = 0; i < 3; i++) fingers[i] = hub.Find($"Finger_{i}"); }
        float h = (0.78f / 2) - 0.08f;
        xMin = -h; xMax = h; zMin = -h; zMax = h;
        dropY = -(2f - 0.15f - 0.25f - 0.8f - 0.15f);
        chuteX = h * 0.8f; chuteZ = -h * 0.8f;
        ok = true;

        // 디버그: 이동 모드에서 Move 액션 값을 확인하기 위해 이동 모드에서도 로그 추가
        Debug.Log($"[Claw] 준비 완료. toggle={toggleAction != null} drop={dropAction != null} moveReader={readMoveMethod != null}");
    }

    void SetClawMode(bool c)
    {
        clawMode = c;
        // Locomotion을 disable하면 Simulator가 스틱 변환을 멈추므로,
        // disable 대신 MoveProvider의 moveSpeed를 0으로 설정한다.
        // 스틱 값은 계속 들어오지만 캐릭터가 안 움직인다.
        foreach (var lc in locoComps)
        {
            if (lc == null) continue;
            var t = lc.GetType();
            var prop = t.GetProperty("moveSpeed");
            if (prop != null && prop.PropertyType == typeof(float))
            {
                if (c)
                {
                    // 집게 모드: 원래 속도 저장 후 0으로
                    savedMoveSpeed = (float)prop.GetValue(lc);
                    prop.SetValue(lc, 0f);
                }
                else
                {
                    // 이동 모드: 원래 속도 복원
                    prop.SetValue(lc, savedMoveSpeed);
                }
            }
            // TurnProvider도 비슷하게 처리
            var turnProp = t.GetProperty("turnSpeed");
            if (turnProp != null && turnProp.PropertyType == typeof(float))
            {
                if (c) turnProp.SetValue(lc, 0f);
                else turnProp.SetValue(lc, 75f); // 기본값
            }
        }
        Debug.Log($"[Claw] → {(c ? "집게 모드" : "이동 모드")}");
    }

    void Update()
    {
        if (!ok) return;

        // --- 모드 전환 ---
        if (toggleAction != null)
        {
            bool t = toggleAction.IsPressed();
            if (t && !prevToggle) SetClawMode(!clawMode);
            prevToggle = t;
        }

        if (!clawMode)
        {
            // 디버그: 이동 모드에서도 MoveProvider 값 확인 (문제 해결 후 삭제)
            if (readMoveMethod != null && leftHandMoveInput != null && Time.frameCount % 60 == 0)
            {
                var v = (Vector2)readMoveMethod.Invoke(leftHandMoveInput, null);
                Debug.Log($"[Claw] 이동모드 move값: ({v.x:F2},{v.y:F2})");
            }
            UpdateRope(); return;
        }

        // --- 하강 ---
        bool drop = dropAction != null && dropAction.WasPressedThisFrame();

        // --- 스틱 (MoveProvider의 leftHandMoveInput에서 직접 읽기) ---
        float dx = 0, dz = 0;
        if (readMoveMethod != null && leftHandMoveInput != null)
        {
            var v = (Vector2)readMoveMethod.Invoke(leftHandMoveInput, null);
            dx = v.x; dz = v.y;
            if (Time.frameCount % 60 == 0)
                Debug.Log($"[Claw] move값: ({dx:F2},{dz:F2})");
        }

        // --- 상태머신 ---
        switch (state)
        {
            case S.Idle:
                if (railX) { var rp = railX.localPosition; rp.z = Mathf.Clamp(rp.z + dz * MOVE * Time.deltaTime, zMin, zMax); railX.localPosition = rp; }
                if (carriage) { var cp = carriage.localPosition; cp.x = Mathf.Clamp(cp.x + dx * MOVE * Time.deltaTime, xMin, xMax); carriage.localPosition = cp; }
                if (drop) state = S.Drop;
                break;
            case S.Drop:
                var p1 = claw.localPosition; p1.y -= DROP_S * Time.deltaTime;
                if (p1.y <= dropY) { p1.y = dropY; state = S.Grab; } claw.localPosition = p1;
                break;
            case S.Grab:
                fa = Mathf.MoveTowards(fa, FC, FS * Time.deltaTime); SetFingers();
                if (Mathf.Approximately(fa, FC)) state = S.Lift;
                break;
            case S.Lift:
                var p2 = claw.localPosition; p2.y += LIFT_S * Time.deltaTime;
                if (p2.y >= 0) { p2.y = 0; state = S.ToChute; } claw.localPosition = p2;
                break;
            case S.ToChute:
                if (MoveTo(railX, 2, chuteZ) && MoveTo(carriage, 0, chuteX)) state = S.Release;
                break;
            case S.Release:
                fa = Mathf.MoveTowards(fa, FO, FS * Time.deltaTime); SetFingers();
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
        rope.localScale = new Vector3(ropeScale0.x, (ropeLen0 + d) / 2, ropeScale0.z);
        rope.localPosition = new Vector3(0, ropeY0 + d / 2, 0);
    }
    bool MoveTo(Transform t, int ax, float tgt)
    {
        if (!t) return true; var p = t.localPosition;
        p[ax] = Mathf.MoveTowards(p[ax], tgt, RET * Time.deltaTime);
        t.localPosition = p; return Mathf.Abs(p[ax] - tgt) < 0.001f;
    }
    void SetFingers()
    {
        if (fingers == null) return;
        for (int i = 0; i < fingers.Length; i++)
            if (fingers[i]) fingers[i].localRotation = Quaternion.Euler(0, i * 120, fa);
    }

    GUIStyle gs, gsS;
    void OnGUI()
    {
        if (gs == null) { gs = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold }; gs.normal.textColor = Color.white; gsS = new GUIStyle(GUI.skin.label) { fontSize = 12 }; gsS.normal.textColor = new Color(0.8f, 0.8f, 0.8f); }
        string mode = clawMode ? "집게 모드" : "이동 모드";
        string s = clawMode ? state switch { S.Drop => " [하강]", S.Grab => " [잡기]", S.Lift => " [상승]", S.ToChute => " [투출구]", S.Release => " [놓기]", S.Reset => " [복귀]", _ => "" } : "";
        GUI.color = new Color(0, 0, 0, 0.75f); GUI.DrawTexture(new Rect(8, 8, 310, 78), Texture2D.whiteTexture); GUI.color = Color.white;
        GUI.Label(new Rect(12, 12, 400, 22), $"{mode}{s}", gs);
        GUI.Label(new Rect(12, 32, 400, 18), "키보드: 1=전환  WASD=조작  2=하강", gsS);
        GUI.Label(new Rect(12, 48, 400, 18), "Quest3: X=전환  스틱=조작  Y=하강", gsS);
    }
}
