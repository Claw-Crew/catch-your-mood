using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

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
    // savedMoveSpeed는 아래에서 선언됨

    // 입력 액션
    InputAction toggleAction;  // Primary Button (X/1키) — 모드 전환
    InputAction dropAction;    // Secondary Button (Y/2키) — 하강
    // Move 입력: ContinuousMoveProvider에서 직접 읽음
    ContinuousMoveProvider moveProviderComp;
    float savedMoveSpeed;
    MonoBehaviour xrSimulator; // XRInteractionSimulator — 집게 모드에서 disable
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

        // --- 모든 MonoBehaviour 중 입력 관련 전부 출력 ---
        Debug.Log("[Claw] ===== 전체 입력 관련 컴포넌트 스캔 시작 =====");

        // 1. 모든 ContinuousMoveProvider
        var allMoveProviders = FindObjectsByType<ContinuousMoveProvider>(FindObjectsSortMode.None);
        Debug.Log($"[Claw] ContinuousMoveProvider 수: {allMoveProviders.Length}");
        foreach (var mp in allMoveProviders)
        {
            var v = mp.leftHandMoveInput.ReadValue();
            var rv = mp.rightHandMoveInput.ReadValue();
            Debug.Log($"[Claw]   MP '{mp.GetType().Name}' obj='{mp.gameObject.name}' speed={mp.moveSpeed} left={v} right={rv} enabled={mp.enabled} activeInHierarchy={mp.gameObject.activeInHierarchy}");
        }

        // 2. 모든 InputActionManager
        var allIAM = FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager>(FindObjectsSortMode.None);
        Debug.Log($"[Claw] InputActionManager 수: {allIAM.Length}");
        foreach (var iam in allIAM)
        {
            Debug.Log($"[Claw]   IAM obj='{iam.gameObject.name}' assets={iam.actionAssets?.Count} enabled={iam.enabled}");
            if (iam.actionAssets != null)
            {
                foreach (var asset in iam.actionAssets)
                {
                    Debug.Log($"[Claw]     Asset: '{asset.name}' maps={asset.actionMaps.Count}");
                    foreach (var map in asset.actionMaps)
                    {
                        if (map.name.Contains("Left Locomotion"))
                        {
                            var moveAct = map.FindAction("Move");
                            if (moveAct != null)
                                Debug.Log($"[Claw]       Move액션: id={moveAct.id} enabled={moveAct.enabled} phase={moveAct.phase} bindings={moveAct.bindings.Count}");
                        }
                    }
                }
            }
        }

        // 3. 모든 InputDevice 상태
        Debug.Log($"[Claw] InputSystem.devices 수: {InputSystem.devices.Count}");
        foreach (var dev in InputSystem.devices)
        {
            if (dev.name.Contains("XR") || dev.name.Contains("Simulated"))
            {
                Debug.Log($"[Claw]   Device: '{dev.name}' type={dev.GetType().Name} enabled={dev.enabled} added={dev.added}");
                // XRSimulatedController이면 primary2DAxis 값 출력
                if (dev is UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRSimulatedController sc)
                {
                    Debug.Log($"[Claw]     stick={sc.primary2DAxis.ReadValue()} primary={sc.primaryButton.ReadValue()} secondary={sc.secondaryButton.ReadValue()}");
                }
            }
        }

        // 4. 모든 활성 InputAction 중 "Move" 포함
        Debug.Log($"[Claw] 활성 InputAction 중 Move 관련:");
        foreach (var act in InputSystem.ListEnabledActions())
        {
            if (act.name.Contains("Move") || act.name.Contains("move"))
                Debug.Log($"[Claw]   Action: '{act.name}' map='{act.actionMap?.name}' asset='{act.actionMap?.asset?.name}' phase={act.phase} value={act.ReadValueAsObject()} id={act.id}");
        }

        Debug.Log("[Claw] ===== 스캔 완료 =====");

        moveProviderComp = allMoveProviders.Length > 0 ? allMoveProviders[0] : null;
        if (moveProviderComp != null)
            savedMoveSpeed = moveProviderComp.moveSpeed;

        // XR Interaction Simulator 찾기 — 집게 모드에서 disable하여 WASD Translate 방지
        foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (mb.GetType().Name == "XRInteractionSimulator")
            {
                xrSimulator = mb;
                Debug.Log($"[Claw] XR Interaction Simulator 찾음: '{mb.gameObject.name}'");
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
        Debug.Log($"[Claw] 준비 완료. toggle={toggleAction != null} drop={dropAction != null} moveProvider={moveProviderComp != null}");
    }

    void SetClawMode(bool c)
    {
        clawMode = c;
        // 집게 모드: XR Simulator를 끔 → WASD가 컨트롤러 위치를 이동시키지 않음
        // 이동 모드: XR Simulator를 켬 → WASD로 캐릭터 이동
        if (xrSimulator != null)
            xrSimulator.enabled = !c;
        Debug.Log($"[Claw] → {(c ? "집게 모드" : "이동 모드")} simulator={xrSimulator?.enabled}");
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
            // 디버그: 이동 모드 — 모든 경로에서 값 확인
            if (Time.frameCount % 120 == 0)
            {
                // MoveProvider
                var mpV = moveProviderComp != null ? moveProviderComp.leftHandMoveInput.ReadValue() : Vector2.zero;
                // XRSimulatedController 직접
                var sc = InputSystem.GetDevice<UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRSimulatedController>();
                var scV = sc != null ? sc.primary2DAxis.ReadValue() : Vector2.zero;
                // 활성 Move 액션
                string actV = "없음";
                foreach (var act in InputSystem.ListEnabledActions())
                {
                    if (act.name == "Move" && act.actionMap?.name?.Contains("Left") == true)
                    {
                        actV = $"{act.ReadValue<Vector2>()} phase={act.phase} id={act.id}";
                        break;
                    }
                }
                Debug.Log($"[Claw] 이동모드 | MP={mpV} | SimCtrl={scV} | MoveAction={actV}");
            }
            UpdateRope(); return;
        }

        // --- 하강 ---
        bool drop = dropAction != null && dropAction.WasPressedThisFrame();

        // --- 집게 이동 입력 ---
        float dx = 0, dz = 0;
        var kb = Keyboard.current;

        // 1) 키보드 직접 읽기 (데스크톱 테스트용)
        if (kb != null)
        {
            if (kb.dKey.isPressed) dx += 1;
            if (kb.aKey.isPressed) dx -= 1;
            if (kb.wKey.isPressed) dz += 1;
            if (kb.sKey.isPressed) dz -= 1;
        }

        // 2) XR 컨트롤러 스틱 (Quest 3 실기기용)
        //    데스크톱에서는 (0,0)이지만 Quest에서는 실제 스틱 값이 들어온다.
        if (moveProviderComp != null)
        {
            var v = moveProviderComp.leftHandMoveInput.ReadValue();
            dx += v.x; dz += v.y;
        }

        dx = Mathf.Clamp(dx, -1, 1);
        dz = Mathf.Clamp(dz, -1, 1);

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
