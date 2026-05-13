using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using System.Collections.Generic;

/// <summary>
/// 인형뽑기 집게 컨트롤러.
///
/// 입력 읽기 방식:
/// - 모드 전환/하강: "XR Interaction Controller Controls" Asset의 "Primary Button"/"Secondary Button"
///   (이전 디버그에서 동작 확인됨)
/// - 스틱(집게 이동): 집게 모드 전용 LeftHand Primary2DAxis InputAction을 직접 읽음
///   (플레이어 이동 provider를 꺼도 집게 레일 입력은 유지)
///
/// [Quest 3]  왼손 X=모드전환  왼손 스틱=조작  왼손 Y=하강
/// [키보드]   1=모드전환  WASD=조작  2=하강
/// </summary>
public class ClawTestController : MonoBehaviour
{

    public ClawHub clawHub;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Object.FindAnyObjectByType<ClawTestController>() != null) return;
        if (GameObject.Find("ClawMachine") == null) return;
        new GameObject("_ClawController").AddComponent<ClawTestController>();
    }

    Transform machineRoot, railX, carriage, claw, rope;
    ClawHub clawHubLogic;
    Transform[] fingers;
    float xMin, xMax, zMin, zMax, dropY, chuteX, chuteZ;
    float railHomeZ, carriageHomeX, clawHomeY;
    Vector3 ropeScale0; float ropeY0, ropeLen0;

    enum S { Idle, Drop, Grab, Lift, ToChute, Release, Reset }
    S state = S.Idle;
    float fa = 15f;
    bool ok, clawMode;
    MonoBehaviour[] locoComps;
    // savedMoveSpeed는 아래에서 선언됨

    // 입력 액션
    InputAction toggleAction;  // Simulator Primary Button (1키) — 모드 전환
    InputAction dropAction;    // Simulator Secondary Button (2키) — 하강
    // Changed: XR 기기용 직접 바인딩 액션 추가.
    // Why: 시뮬레이터 전용 키 바인딩에 의존하면 Quest X/Y 입력이 안 잡힐 수 있음.
    InputAction toggleActionXR; // Left PrimaryButton (Quest X)
    InputAction dropActionXR;   // Left SecondaryButton (Quest Y)
    // Changed: 집게 모드 전용 왼손 스틱 액션 추가.
    // Why: 첫번째 사례처럼 플레이어 이동 입력과 별개로 레일을 직접 움직이는 입력 경로가 필요함.
    InputAction clawMoveActionXR;
    // Move 입력 보조 경로: 왼손 locomotion action/provider에서만 읽음
    ContinuousMoveProvider moveProviderComp;
    // Changed: 왼손 Locomotion Move 액션을 보조 참조로 유지.
    // Why: 시뮬레이터/실기기 설정 차이로 전용 액션이 0일 때만 fallback 값을 얻기 위함.
    InputAction leftLocomotionMoveAction;
    float savedMoveSpeed;
    XRInteractionSimulator xrSimulator; // XRInteractionSimulator — 집게 모드에서 translation speed만 잠금
    float simulatorTranslateXSpeed0, simulatorTranslateYSpeed0, simulatorTranslateZSpeed0;
    bool simulatorSpeedCached;
    // Changed: 모드 전환 시 locomotion 컴포넌트 상태 복원을 위한 캐시 추가.
    // Why: 집게 모드 해제 시 원래 enable 상태를 정확히 복원하기 위함.
    readonly Dictionary<MonoBehaviour, bool> locoEnabledBeforeClaw = new();

    // Changed: 첫번째 claw-machine 사례의 PuedeControlarse 패턴을 현재 상태머신에 맞게 명시화.
    // Why: Idle일 때만 레일 X/Z를 조작하고, Drop/Grab/Lift/Release 중에는 입력을 잠금.
    bool CanControlClaw => clawMode && state == S.Idle;

    // Changed: 집게 XY(레일 X/Z) 이동 속도 상향.
    // Why: 입력은 들어오는데 이동량이 너무 작아 정지처럼 보이는 체감 문제를 줄이기 위함.
    const float MOVE = 0.9f, DROP_S = 0.6f, LIFT_S = 0.4f, RET = 0.5f;
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
        // Changed: Controller 액션을 명시적으로 Enable.
        // Why: InputActionManager 설정 상태에 따라 Primary/Secondary가 비활성인 경우를 방지.
        if (toggleAction != null && !toggleAction.enabled) toggleAction.Enable();
        if (dropAction != null && !dropAction.enabled) dropAction.Enable();

        // Changed: XRI 기본 액션 에셋에서 왼손 Locomotion Move만 보조로 획득.
        // Why: 집게 모드는 왼손 스틱만 사용해야 하므로 오른손 이동 액션을 입력 후보에서 제외.
        foreach (var asset in Resources.FindObjectsOfTypeAll<InputActionAsset>())
        {
            if (asset.name != "XRI Default Input Actions") continue;
            var leftLoc = asset.FindActionMap("XRI Left Locomotion");
            leftLocomotionMoveAction = leftLoc != null ? leftLoc.FindAction("Move") : null;
            break;
        }

        // Changed: Quest 왼손 X/Y 직접 바인딩 액션을 런타임 생성하고 오른손 바인딩 제거.
        // Why: 사용자가 지정한 Left Primary(X) 모드 전환 규칙과 실제 입력 소유권을 일치시키기 위함.
        toggleActionXR = new InputAction("ClawToggle_Primary", InputActionType.Button);
        toggleActionXR.AddBinding("<XRController>{LeftHand}/{PrimaryButton}");
        toggleActionXR.AddBinding("<OculusTouchController>{LeftHand}/primaryButton");
        toggleActionXR.Enable();

        dropActionXR = new InputAction("ClawDrop_Secondary", InputActionType.Button);
        dropActionXR.AddBinding("<XRController>{LeftHand}/{SecondaryButton}");
        dropActionXR.AddBinding("<OculusTouchController>{LeftHand}/secondaryButton");
        dropActionXR.Enable();

        // Changed: 집게 레일 이동은 왼손 Primary2DAxis/thumbstick을 직접 읽는 전용 액션으로 분리.
        // Why: locomotion 컴포넌트를 끄는 순간 MoveProvider 기반 입력이 0이 되는 구조를 피하기 위함.
        clawMoveActionXR = new InputAction("ClawMove_LeftPrimary2DAxis", InputActionType.Value, expectedControlType: "Vector2");
        clawMoveActionXR.AddBinding("<XRController>{LeftHand}/{Primary2DAxis}").WithProcessor("StickDeadzone");
        clawMoveActionXR.AddBinding("<OculusTouchController>{LeftHand}/thumbstick").WithProcessor("StickDeadzone");
        clawMoveActionXR.Enable();

        // Changed: 직접 참조한 Move 액션 강제 enable.
        // Why: 일부 실행환경에서 액션 비활성 상태로 ReadValue가 0이 되는 경우를 방지.
        if (leftLocomotionMoveAction != null) leftLocomotionMoveAction.Enable();

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

        // XR Interaction Simulator 찾기 — 입력 장치 자체는 집게 모드에서도 유지
        foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (mb.GetType().Name == "XRInteractionSimulator")
            {
                xrSimulator = mb as XRInteractionSimulator;
                Debug.Log($"[Claw] XR Interaction Simulator 찾음: '{mb.gameObject.name}'");
                CacheSimulatorTranslationSpeed();
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
                n.Contains("ContinuousTurn") || n.Contains("DynamicMove") ||
                n.Contains("XRPlayerMover") || n.Contains("XRMoveSpeedAddon"))
                list.Add(mb);
        }
        locoComps = list.ToArray();
        // Changed: locomotion 컴포넌트의 초기 enable 상태 캐시.
        // Why: 집게 모드 종료 시 원래 상태로 복원하기 위함.
        locoEnabledBeforeClaw.Clear();
        foreach (var mb in locoComps)
            if (mb != null)
                locoEnabledBeforeClaw[mb] = mb.enabled;

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
        // Changed: 현재 SceneSetup 계층(ClawMachine/Rails/RailX/Carriage/ClawAssembly)과
        // 이전 계층(ClawMachine/Rail_X/Carriage/ClawAssembly)을 모두 지원.
        // Why: 경로 불일치로 ok=false가 되어 모드만 바뀌고 집게 이동 코드가 실행되지 않는 문제를 해결.
        var m = GameObject.Find("ClawMachine"); if (!m) return;
        machineRoot = m.transform;
        railX = m.transform.Find("Rail_X") ?? m.transform.Find("Rails/RailX") ?? FindDeepChild(m.transform, "RailX");
        if (railX) carriage = railX.Find("Carriage") ?? FindDeepChild(railX, "Carriage");
        if (carriage) claw = carriage.Find("ClawAssembly") ?? FindDeepChild(carriage, "ClawAssembly");
        if (!railX || !carriage || !claw)
        {
            Debug.LogError($"[Claw] 집게 계층 탐색 실패. railX={railX != null} carriage={carriage != null} claw={claw != null}");
            return;
        }
        rope = claw.Find("Rope");
        if (rope) { ropeScale0 = rope.localScale; ropeY0 = rope.localPosition.y; ropeLen0 = ropeScale0.y * 2f; }
        var hub = claw.Find("ClawHub");
        if (hub) { fingers = new Transform[3]; for (int i = 0; i < 3; i++) fingers[i] = hub.Find($"Finger_{i}") ?? hub.Find($"F{i}"); }
        clawHubLogic = claw.GetComponentInChildren<ClawHub>();
        // Changed: dev 브랜치의 public clawHub 참조와 내부 clawHubLogic 참조를 같은 인스턴스로 동기화.
        // Why: inspector에서 할당된 기존 참조와 런타임 자동 탐색 경로가 서로 어긋나지 않게 하기 위함.
        if (clawHub == null) clawHub = clawHubLogic;
        // Changed: 시작 시 ClawHub의 자동 grab 권한을 닫아둠.
        // Why: approach 감지는 유지하되, 하강/집기 시퀀스 전에는 인형이 claw에 붙지 않아야 함.
        clawHubLogic?.SetGrabEnabled(false);
        float h = (0.78f / 2) - 0.08f;
        // Changed: 회전/스케일된 RailX 메쉬의 local 축이 아니라 ClawMachine 루트 기준 좌표를 이동 기준으로 사용.
        // Why: RailX가 원통 메쉬라 localPosition.x/y/z가 실제 기계 X/Z/Y축과 일치하지 않아 집게가 움직이지 않음.
        railHomeZ = GetRootAxis(railX, 2);
        carriageHomeX = GetRootAxis(carriage, 0);
        clawHomeY = GetRootAxis(claw, 1);
        xMin = carriageHomeX - h; xMax = carriageHomeX + h;
        zMin = railHomeZ - h; zMax = railHomeZ + h;
        // Changed: 하강 한계를 기계 고정 치수 대신 PrizeFloor와 현재 집게 bounds 기준으로 계산.
        // Why: 집게 길이/플레이필드 높이가 바뀌어도 바닥을 뚫지 않고 인형 근처에서 멈추게 하기 위함.
        dropY = CalculateDropY();
        chuteX = carriageHomeX + h * 0.8f; chuteZ = railHomeZ - h * 0.8f;
        ok = true;

        // 디버그: 이동 모드에서 Move 액션 값을 확인하기 위해 이동 모드에서도 로그 추가
        Debug.Log($"[Claw] 준비 완료. toggle={toggleAction != null} drop={dropAction != null} moveProvider={moveProviderComp != null}");
    }

    void OnDestroy()
    {
        // Changed: Play Mode 종료/스크립트 재로드 때 시뮬레이터 이동 속도를 원복.
        // Why: 집게 모드에서 종료되면 XR Interaction Simulator translation speed가 0으로 남는 문제를 방지.
        SetSimulatorTranslationLocked(false);
        // Changed: 런타임 생성한 액션만 정리.
        // Why: 공유 InputActionAsset 액션을 임의 Disable하면 다른 시스템 입력이 끊길 수 있음.
        if (toggleActionXR != null) { toggleActionXR.Disable(); toggleActionXR.Dispose(); toggleActionXR = null; }
        if (dropActionXR != null) { dropActionXR.Disable(); dropActionXR.Dispose(); dropActionXR = null; }
        if (clawMoveActionXR != null) { clawMoveActionXR.Disable(); clawMoveActionXR.Dispose(); clawMoveActionXR = null; }
    }

    void SetClawMode(bool c)
    {
        clawMode = c;
        // Changed: XR Interaction Simulator는 끄지 않고 translation speed만 0으로 잠금.
        // Why: 시뮬레이터를 끄면 스틱 장치 값도 끊기지만, 켜둔 채로 두면 FPS translation으로 사용자가 계속 움직임.
        // Changed: 집게 모드에서 locomotion 및 속도 동기화 컴포넌트를 비활성화하고, 해제 시 복원.
        // Why: DynamicMoveProvider와 XRPlayerMover가 사용자 이동/시뮬레이터 속도를 계속 갱신하는 충돌을 제거.
        if (locoComps != null)
        {
            if (c)
            {
                foreach (var mb in locoComps)
                {
                    if (mb == null) continue;
                    locoEnabledBeforeClaw[mb] = mb.enabled;
                    mb.enabled = false;
                }
            }
            else
            {
                foreach (var mb in locoComps)
                {
                    if (mb == null) continue;
                    bool wasEnabled;
                    if (locoEnabledBeforeClaw.TryGetValue(mb, out wasEnabled))
                        mb.enabled = wasEnabled;
                    else
                        mb.enabled = true;
                }
            }
        }

        SetSimulatorTranslationLocked(c);
        if (!c) clawHubLogic?.SetGrabEnabled(false);
        Debug.Log($"[Claw] → {(c ? "집게 모드" : "이동 모드")} locoComps={locoComps?.Length ?? 0} simulator={xrSimulator?.enabled} simSpeed=({GetSimulatorSpeedText()})");
    }

    void Update()
    {
        var kb = Keyboard.current;

        // --- 모드 전환 ---
        // Changed: 모드 전환 입력을 (시뮬레이터 Primary + XR Primary + 키보드 1) 병합 처리.
        // Why: 에디터/실기기 모두에서 모드 전환이 항상 1회성으로 동작하도록 보장.
        // Changed: 키보드 숫자 1(및 numpad1) 직접 처리 추가.
        // Why: XR 액션 경로가 끊겨도 Mac 에디터에서 모드 전환이 반드시 가능해야 함.
        bool togglePressed = (toggleAction != null && toggleAction.WasPressedThisFrame()) ||
                             (toggleActionXR != null && toggleActionXR.WasPressedThisFrame()) ||
                             (kb != null && (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame));
        if (togglePressed) SetClawMode(!clawMode);

        // Changed: 클로 구조 탐색 실패 시에도 모드 전환 검증은 가능하게 유지.
        // Why: 씬 구조명 변경으로 ok=false가 되면 "모든 버튼 무반응"으로 오해되는 문제 방지.
        if (!ok) return;

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
        // Changed: 하강 입력을 (시뮬레이터 Secondary + XR Left Secondary) 병합 처리.
        // Why: 에디터/실기기 모두에서 Y 버튼 하강을 동일하게 동작시키기 위함.
        // Changed: 키보드 숫자 2(및 numpad2) 직접 처리 추가.
        // Why: XR 액션이 미동작이어도 하강 트리거 검증이 가능해야 함.
        bool drop = (dropAction != null && dropAction.WasPressedThisFrame()) ||
                    (dropActionXR != null && dropActionXR.WasPressedThisFrame()) ||
                    (kb != null && (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame));

        // Changed: 첫번째 사례의 "조작 가능 상태에서만 직접 이동" 구조로 입력 계산을 제한.
        // Why: 집게 시퀀스 중 스틱 입력이 레일 위치를 덮어써서 상태 이동과 충돌하지 않게 하기 위함.
        Vector2 clawMoveInput = CanControlClaw ? ReadClawMoveInput(kb) : Vector2.zero;

        // --- 상태머신 ---
        switch (state)
        {
            case S.Idle:
                ApplyClawMove(clawMoveInput);
                if (drop) state = S.Drop;
                break;
            case S.Drop:
                {
                    float y = GetRootAxis(claw, 1) - DROP_S * Time.deltaTime;
                    if (y <= dropY)
                    {
                        y = dropY;
                        // Changed: 집게가 목표 하강 위치에 도달한 뒤에만 ClawHub grab 권한을 엶.
                        // Why: 게임 시작/하강 중 접근 반응만으로 인형이 claw에 즉시 붙는 문제를 막기 위함.
                        clawHubLogic?.SetGrabEnabled(true);
                        state = S.Grab;
                    }
                    SetRootAxis(claw, 1, y);
                }
                break;
            case S.Grab:
                fa = Mathf.MoveTowards(fa, FC, FS * Time.deltaTime); SetFingers();
                if (Mathf.Approximately(fa, FC)) state = S.Lift;
                break;
            case S.Lift:
                {
                    float y = GetRootAxis(claw, 1) + LIFT_S * Time.deltaTime;
                    if (y >= clawHomeY) { y = clawHomeY; state = S.ToChute; }
                    SetRootAxis(claw, 1, y);
                }
                break;
            case S.ToChute:
                if (MoveToRootAxis(railX, 2, chuteZ) && MoveToRootAxis(carriage, 0, chuteX)) state = S.Release;
                break;
            case S.Release:
                fa = Mathf.MoveTowards(fa, FO, FS * Time.deltaTime); SetFingers();
                if (Mathf.Approximately(fa, FO))
                {
                    // Changed: XRI selectExited 대신 ClawHub가 잡은 Rigidbody를 명시적으로 release.
                    // Why: 현재 인형 잡기는 ClawHub 거리 기반이므로 Release 상태에서 직접 놓아야 함.
                    clawHubLogic?.ReleaseGrabbed();
                    clawHubLogic?.SetGrabEnabled(false);
                    state = S.Reset;
                }
                break;
            case S.Reset:
                if (MoveToRootAxis(railX, 2, railHomeZ) && MoveToRootAxis(carriage, 0, carriageHomeX)) state = S.Idle;
                break;
        }
        UpdateRope();
    }

    Vector2 ReadClawMoveInput(Keyboard kb)
    {
        // Changed: 집게 이동 입력을 전용 액션, 키보드, 기존 왼손 Move 액션 순서로 병합.
        // Why: Quest 실기기와 Mac 에디터 검증 경로를 모두 유지하면서 오른손 조작 오염을 피하기 위함.
        Vector2 input = Vector2.zero;

        if (clawMoveActionXR != null)
            input = PickLarger(input, clawMoveActionXR.ReadValue<Vector2>());

        // Changed: XR Interaction Simulator가 생성한 컨트롤러 장치의 primary2DAxis를 직접 읽음.
        // Why: 시뮬레이터 UI의 IJKL/Shift+IJKL 값이 XRI locomotion action에는 들어가도
        // 별도 런타임 InputAction에는 0으로 남는 경우가 있어 집게가 멈추는 문제를 방지.
        foreach (var device in InputSystem.devices)
            if (device is XRSimulatedController simulatedController)
                input = PickLarger(input, simulatedController.primary2DAxis.ReadValue());

        if (kb != null)
        {
            Vector2 keyboardInput = Vector2.zero;
            if (kb.dKey.isPressed) keyboardInput.x += 1f;
            if (kb.aKey.isPressed) keyboardInput.x -= 1f;
            if (kb.wKey.isPressed) keyboardInput.y += 1f;
            if (kb.sKey.isPressed) keyboardInput.y -= 1f;
            input += keyboardInput;
        }

        if (leftLocomotionMoveAction != null)
            input = PickLarger(input, leftLocomotionMoveAction.ReadValue<Vector2>());

        if (moveProviderComp != null)
            input = PickLarger(input, moveProviderComp.leftHandMoveInput.ReadValue());

        if (input.sqrMagnitude > 1f) input.Normalize();
        return input;
    }

    void CacheSimulatorTranslationSpeed()
    {
        // Changed: 시뮬레이터 translation 속도 복원을 위한 초기값 캐시.
        // Why: 집게 모드에서는 사용자 이동을 막고, 이동 모드로 돌아오면 기존 검증 속도를 되살려야 함.
        if (xrSimulator == null || simulatorSpeedCached) return;
        simulatorTranslateXSpeed0 = xrSimulator.translateXSpeed;
        simulatorTranslateYSpeed0 = xrSimulator.translateYSpeed;
        simulatorTranslateZSpeed0 = xrSimulator.translateZSpeed;
        simulatorSpeedCached = true;
    }

    void SetSimulatorTranslationLocked(bool locked)
    {
        // Changed: 집게 모드에서 XR Interaction Simulator의 FPS translation만 잠금.
        // Why: 컨트롤러/버튼/스틱 장치는 유지하면서 사용자 몸 이동만 차단하기 위함.
        if (xrSimulator == null) return;
        CacheSimulatorTranslationSpeed();
        if (locked)
        {
            xrSimulator.translateXSpeed = 0f;
            xrSimulator.translateYSpeed = 0f;
            xrSimulator.translateZSpeed = 0f;
        }
        else if (simulatorSpeedCached)
        {
            xrSimulator.translateXSpeed = simulatorTranslateXSpeed0;
            xrSimulator.translateYSpeed = simulatorTranslateYSpeed0;
            xrSimulator.translateZSpeed = simulatorTranslateZSpeed0;
        }
    }

    string GetSimulatorSpeedText()
    {
        // Changed: 모드 전환 로그에서 실제 시뮬레이터 이동 잠금 상태를 확인.
        // Why: "집게 모드인데 사용자 이동 가능" 문제를 로그로 바로 검증하기 위함.
        if (xrSimulator == null) return "none";
        return $"{xrSimulator.translateXSpeed:0.###},{xrSimulator.translateYSpeed:0.###},{xrSimulator.translateZSpeed:0.###}";
    }

    static Vector2 PickLarger(Vector2 current, Vector2 candidate)
    {
        // Changed: 여러 입력 경로 중 실제로 움직인 값을 선택.
        // Why: 비활성/미연결 액션의 (0,0)이 정상 입력을 덮어쓰지 않게 하기 위함.
        return candidate.sqrMagnitude > current.sqrMagnitude ? candidate : current;
    }

    void ApplyClawMove(Vector2 input)
    {
        // Changed: 첫번째 사례처럼 레일 Transform을 직접 X/Z 한계 안에서 이동.
        // Why: Rigidbody/플레이어 locomotion과 분리된 안정적인 claw-machine 이동 모델을 만들기 위함.
        float dx = Mathf.Clamp(input.x, -1f, 1f);
        float dz = Mathf.Clamp(input.y, -1f, 1f);

        if (railX)
            SetRootAxis(railX, 2, Mathf.Clamp(GetRootAxis(railX, 2) + dz * MOVE * Time.deltaTime, zMin, zMax));

        if (carriage)
            SetRootAxis(carriage, 0, Mathf.Clamp(GetRootAxis(carriage, 0) + dx * MOVE * Time.deltaTime, xMin, xMax));

        // Changed: 집게 모드 입력/레일 위치를 주기적으로 출력.
        // Why: 입력은 들어오는데 Transform 계층/한계값 문제로 이동이 안 되는지 즉시 확인하기 위함.
        if (Time.frameCount % 60 == 0)
        {
            float rz = railX ? GetRootAxis(railX, 2) : 0f;
            float cx = carriage ? GetRootAxis(carriage, 0) : 0f;
            Debug.Log($"[Claw] 집게입력={input} rootRailZ={rz:0.###} rootCarriageX={cx:0.###}");
        }
    }

    float GetRootAxis(Transform t, int axis)
    {
        // Changed: 이동 좌표 읽기를 ClawMachine 루트 기준으로 통일.
        // Why: 회전/스케일된 메쉬 Transform의 local 축을 레일 이동 좌표로 쓰면 실제 이동 축과 어긋남.
        if (t == null || machineRoot == null) return 0f;
        Vector3 rootLocal = machineRoot.InverseTransformPoint(t.position);
        return rootLocal[axis];
    }

    void SetRootAxis(Transform t, int axis, float value)
    {
        // Changed: 이동 좌표 쓰기를 ClawMachine 루트 기준으로 통일.
        // Why: parent가 회전/비균일 스케일을 가져도 월드 배치가 기계 X/Y/Z축을 따라 움직이게 함.
        if (t == null || machineRoot == null) return;
        Vector3 rootLocal = machineRoot.InverseTransformPoint(t.position);
        rootLocal[axis] = value;
        t.position = machineRoot.TransformPoint(rootLocal);
    }

    float CalculateDropY()
    {
        // Changed: PrizeFloor top과 집게 렌더러 최하단 사이의 현재 offset으로 하강 목표를 산출.
        // Why: ClawHub/Rope 길이 조정 뒤에도 하강 깊이가 자동으로 안전하게 맞춰지도록 하기 위함.
        float fallback = clawHomeY - 0.18f;
        Transform prizeFloor = FindDeepChild(machineRoot, "PrizeFloor");
        if (prizeFloor == null || !TryGetLowestRootY(claw.gameObject, out float clawMinY))
            return fallback;

        float floorTopY = GetRootAxis(prizeFloor, 1);
        var floorCollider = prizeFloor.GetComponent<Collider>();
        if (floorCollider != null)
            floorTopY = machineRoot.InverseTransformPoint(floorCollider.bounds.max).y;

        float clawBottomBelowAssembly = Mathf.Max(0f, clawHomeY - clawMinY);
        float target = floorTopY + 0.04f + clawBottomBelowAssembly;
        return Mathf.Min(clawHomeY, target);
    }

    bool TryGetLowestRootY(GameObject go, out float minY)
    {
        // Changed: 집게 시각물 전체 bounds에서 최하단 root-space Y를 계산.
        // Why: Hub/finger 길이가 바뀌어도 하강 한계를 코드 상수로 다시 맞출 필요가 없게 하기 위함.
        minY = float.PositiveInfinity;
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return false;

        foreach (var r in renderers)
            minY = Mathf.Min(minY, machineRoot.InverseTransformPoint(r.bounds.min).y);

        return !float.IsInfinity(minY);
    }

    static Transform FindDeepChild(Transform root, string childName)
    {
        // Changed: SceneSetup/이전 씬 계층 차이를 흡수하는 재귀 탐색 추가.
        // Why: 특정 parent 경로가 바뀌어도 핵심 레일/캐리지/집게 노드를 안정적으로 찾기 위함.
        if (root == null) return null;
        if (root.name == childName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeepChild(root.GetChild(i), childName);
            if (found != null) return found;
        }
        return null;
    }

    void UpdateRope()
    {
        if (!rope || !claw) return;
        // Changed: 로프 길이도 ClawMachine 루트 기준 Y 이동량으로 계산.
        // Why: ClawAssembly 부모가 회전/스케일된 RailX 계층 아래에 있어 localPosition.y가 실제 하강 거리와 다를 수 있음.
        float d = Mathf.Abs(GetRootAxis(claw, 1) - clawHomeY);
        rope.localScale = new Vector3(ropeScale0.x, (ropeLen0 + d) / 2, ropeScale0.z);
        rope.localPosition = new Vector3(0, ropeY0 + d / 2, 0);
    }
    bool MoveToRootAxis(Transform t, int ax, float tgt)
    {
        // Changed: 자동 복귀/투출구 이동도 루트 기준 축 이동으로 교체.
        // Why: localPosition 기반 이동은 RailX/Carriage의 시각 메쉬 회전/스케일에 의해 축이 틀어짐.
        if (!t) return true;
        float current = GetRootAxis(t, ax);
        float next = Mathf.MoveTowards(current, tgt, RET * Time.deltaTime);
        SetRootAxis(t, ax, next);
        return Mathf.Abs(next - tgt) < 0.001f;
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
        // Changed: 런타임 오버레이도 Left Primary/Left Stick 규칙과 일치시킴.
        // Why: 씬 안내판과 Game View 디버그 안내가 서로 다르면 검증 절차가 혼동됨.
        GUI.Label(new Rect(12, 32, 400, 18), "키보드: 1=전환  WASD=조작  2=하강", gsS);
        GUI.Label(new Rect(12, 48, 400, 18), "Quest3: 왼손 X=전환  왼손 스틱=조작  왼손 Y=하강", gsS);
    }
}
