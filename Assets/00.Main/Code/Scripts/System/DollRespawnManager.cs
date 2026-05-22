using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Changed: CatchDetector가 점수 처리 후 호출하는 런타임 인형 리스폰 매니저 추가.
// Why: SceneSetup에 의존하지 않고 씬에 존재하는 DollInfo 인스턴스의 초기 spawn 상태를 자동 기록하기 위함.
[DefaultExecutionOrder(-100)]
public class DollRespawnManager : MonoBehaviour
{
    private struct DollSpawnState
    {
        public EmotionType Emotion;
        public Transform Parent;
        public Vector3 WorldPosition;
        public Quaternion WorldRotation;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;
        public bool HasRigidbody;
        public bool RigidbodyWasKinematic;
    }

    // Changed: scene 배치 없이도 런타임에 하나의 매니저가 자동 생성되도록 singleton bootstrap 사용.
    // Why: CatchZone 생성 방식이나 SceneSetup 수정 여부와 무관하게 리스폰 기능이 동작해야 하기 때문.
    private static DollRespawnManager instance;

    // Changed: 인스턴스별 spawn 상태와 감정별 인스턴스 목록을 모두 기록.
    // Why: 같은 감정 인형을 다시 공급하면서도 각 DollInfo 인스턴스의 원래 위치/회전/물리 상태를 복원하기 위함.
    private readonly Dictionary<int, DollSpawnState> spawnStatesByInstance = new Dictionary<int, DollSpawnState>();
    private readonly Dictionary<EmotionType, List<int>> spawnInstanceIdsByEmotion = new Dictionary<EmotionType, List<int>>();
    private readonly HashSet<int> pendingRespawns = new HashSet<int>();

    public static DollRespawnManager Instance
    {
        get
        {
            // Changed: 기존 scene 객체를 먼저 재사용하고 없을 때만 런타임 객체 생성.
            // Why: 수동 배치와 자동 bootstrap이 동시에 있어도 중복 매니저를 만들지 않기 위함.
            if (instance != null)
                return instance;

            instance = FindFirstObjectByType<DollRespawnManager>();
            if (instance != null)
                return instance;

            GameObject go = new GameObject(nameof(DollRespawnManager));
            instance = go.AddComponent<DollRespawnManager>();
            return instance;
        }
    }

    // Changed: Enter Play Mode에서 domain reload가 꺼져도 static singleton을 초기화.
    // Why: 이전 플레이 세션의 instance 참조가 다음 세션 리스폰 기록을 오염시키지 않도록 하기 위함.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    // Changed: 씬 로드 직후 매니저를 자동 생성.
    // Why: 첫 catch가 발생하기 전에 현재 씬의 인형 초기 위치를 기록해야 하기 때문.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAfterSceneLoad()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject(nameof(DollRespawnManager));
        go.AddComponent<DollRespawnManager>();
    }

    private void Awake()
    {
        // Changed: singleton 중복을 정리하고 Awake 시점에 1차 spawn snapshot을 수집.
        // Why: CatchDetector가 첫 physics trigger를 받기 전에 초기 DollInfo transform/Rigidbody 상태를 보존하기 위함.
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        RecordExistingDolls();
    }

    private void Start()
    {
        // Changed: Start 시점에 2차 snapshot을 수행.
        // Why: 다른 Awake에서 늦게 생성/활성화된 DollInfo도 초기 spawn 기록에 포함하기 위함.
        RecordExistingDolls();
    }

    public bool IsRespawnPending(DollInfo doll)
    {
        // Changed: CatchDetector가 pending 인형을 점수 처리 전에 거를 수 있는 조회 함수 제공.
        // Why: 리스폰 대기 중인 같은 인형이 CatchZone에서 다시 감지되어 중복 카운트되는 것을 막기 위함.
        return doll != null && pendingRespawns.Contains(doll.GetInstanceID());
    }

    public void RespawnAfterDelay(DollInfo doll, float delaySeconds)
    {
        // Changed: CatchDetector에서 호출하는 지연 리셋 진입점 추가.
        // Why: 점수/파티클 처리가 끝난 뒤 같은 DollInfo 인스턴스를 원래 spawn 상태로 되돌리기 위함.
        if (doll == null)
            return;

        RecordDollIfMissing(doll);

        int instanceId = doll.GetInstanceID();
        if (pendingRespawns.Contains(instanceId))
            return;

        StartCoroutine(RespawnRoutine(doll, instanceId, Mathf.Max(0f, delaySeconds)));
    }

    private void RecordExistingDolls()
    {
        // Changed: 런타임에 로드된 모든 DollInfo를 찾아 초기 상태를 자동 기록.
        // Why: SceneSetup이 별도 spawn 정보를 넘기지 않아도 감정별/인스턴스별 리스폰 기준점을 확보하기 위함.
        DollInfo[] dolls = FindObjectsByType<DollInfo>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (DollInfo doll in dolls)
        {
            RecordDollIfMissing(doll);
        }
    }

    private void RecordDollIfMissing(DollInfo doll)
    {
        // Changed: DollInfo 인스턴스마다 최초 1회만 spawn snapshot을 저장.
        // Why: 잡힌 뒤 CatchZone 위치에서 다시 기록되어 원래 플레이필드 위치를 잃지 않도록 하기 위함.
        if (doll == null)
            return;

        int instanceId = doll.GetInstanceID();
        if (spawnStatesByInstance.ContainsKey(instanceId))
            return;

        Rigidbody rb = doll.GetComponent<Rigidbody>();
        DollSpawnState state = new DollSpawnState
        {
            Emotion = doll.emotionType,
            Parent = doll.transform.parent,
            WorldPosition = doll.transform.position,
            WorldRotation = doll.transform.rotation,
            LocalPosition = doll.transform.localPosition,
            LocalRotation = doll.transform.localRotation,
            LocalScale = doll.transform.localScale,
            HasRigidbody = rb != null,
            RigidbodyWasKinematic = rb != null && rb.isKinematic
        };

        spawnStatesByInstance.Add(instanceId, state);

        if (!spawnInstanceIdsByEmotion.TryGetValue(state.Emotion, out List<int> ids))
        {
            ids = new List<int>();
            spawnInstanceIdsByEmotion.Add(state.Emotion, ids);
        }

        ids.Add(instanceId);
    }

    private IEnumerator RespawnRoutine(DollInfo doll, int instanceId, float delaySeconds)
    {
        // Changed: pending 상태를 코루틴 전체에 걸쳐 유지하고 claw 제어권과 물리 이동을 즉시 중지.
        // Why: 리셋 대기 중 같은 인형이 중복 점수 처리되거나 ClawHub/physics에 의해 계속 이동하지 않게 하기 위함.
        pendingRespawns.Add(instanceId);
        ReleaseDollFromClaws(doll);
        FreezeDoll(doll);

        if (delaySeconds > 0f)
            yield return new WaitForSeconds(delaySeconds);
        else
            yield return null;

        if (doll != null && TryGetSpawnState(doll, out DollSpawnState state))
        {
            ResetDoll(doll, state);
        }

        pendingRespawns.Remove(instanceId);
    }

    private void FreezeDoll(DollInfo doll)
    {
        // Changed: 리스폰 대기 중 non-kinematic Rigidbody 속도만 정리하고 임시 kinematic 상태로 전환.
        // Why: Unity kinematic velocity warning을 피하면서 CatchZone 안의 추가 충돌/낙하를 막기 위함.
        if (doll == null)
            return;

        Rigidbody rb = doll.GetComponent<Rigidbody>();
        if (rb == null)
            return;

        ClearVelocitiesIfDynamic(rb);
        rb.isKinematic = true;
    }

    private void ReleaseDollFromClaws(DollInfo doll)
    {
        // Changed: respawn 진입 시 해당 Rigidbody를 잡고 있는 ClawHub만 targeted release 처리.
        // Why: ClawHub.Update의 MovePosition이 리스폰 reset 위치를 다시 덮어쓰는 충돌을 막기 위함.
        if (doll == null)
            return;

        Rigidbody rb = doll.GetComponent<Rigidbody>();
        if (rb == null)
            return;

        ClawHub[] clawHubs = FindObjectsByType<ClawHub>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (ClawHub clawHub in clawHubs)
        {
            clawHub.ReleaseGrabbedIfMatches(rb);
        }
    }

    private bool TryGetSpawnState(DollInfo doll, out DollSpawnState state)
    {
        // Changed: 인스턴스 snapshot을 우선 사용하고, 없으면 같은 감정의 기록을 fallback으로 사용.
        // Why: 런타임 생성 인형이라도 가능한 한 같은 감정 spawn 기준으로 복원하기 위함.
        int instanceId = doll.GetInstanceID();
        if (spawnStatesByInstance.TryGetValue(instanceId, out state))
            return true;

        if (spawnInstanceIdsByEmotion.TryGetValue(doll.emotionType, out List<int> ids))
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (spawnStatesByInstance.TryGetValue(ids[i], out state))
                    return true;
            }
        }

        state = default;
        return false;
    }

    private void ResetDoll(DollInfo doll, DollSpawnState state)
    {
        // Changed: transform 위치/회전/scale과 Rigidbody kinematic 상태를 warning 없이 복원.
        // Why: 인형이 잡히기 전 플레이필드 spawn 상태로 돌아가되 kinematic body에는 velocity를 쓰지 않기 위함.
        Rigidbody rb = doll.GetComponent<Rigidbody>();
        if (rb != null)
        {
            ClearVelocitiesIfDynamic(rb);
            rb.isKinematic = true;
        }

        Transform dollTransform = doll.transform;
        if (state.Parent != null)
        {
            dollTransform.SetParent(state.Parent, false);
            dollTransform.localPosition = state.LocalPosition;
            dollTransform.localRotation = state.LocalRotation;
        }
        else
        {
            dollTransform.SetParent(null, false);
            dollTransform.position = state.WorldPosition;
            dollTransform.rotation = state.WorldRotation;
        }

        dollTransform.localScale = state.LocalScale;

        if (rb == null)
            return;

        rb.position = dollTransform.position;
        rb.rotation = dollTransform.rotation;
        rb.isKinematic = state.HasRigidbody && state.RigidbodyWasKinematic;
        ClearVelocitiesIfDynamic(rb);

        if (!rb.isKinematic)
            rb.WakeUp();
    }

    private static void ClearVelocitiesIfDynamic(Rigidbody rb)
    {
        // Changed: Rigidbody velocity writes are centralized behind an isKinematic guard.
        // Why: Unity does not support setting linear/angular velocity on kinematic bodies.
        if (rb == null || rb.isKinematic)
            return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
}
