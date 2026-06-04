using UnityEngine;

/// <summary>
/// Gaze-driven mood dispatcher. Mirrors ClawHub but uses the player's head gaze
/// instead of claw proximity. When the player looks at a doll for `dwellToTrigger`
/// seconds, that doll's IMoodReaction.OnApproach() fires; OnRetreat() fires when the
/// gaze leaves it.
///
/// Coordination with ClawHub (claw has priority):
///   - Never starts a gaze reaction on the doll ClawHub is currently handling.
///   - If the claw takes over a doll the gaze was driving, the gaze relinquishes it
///     silently (no OnRetreat) so the claw keeps full control of that doll.
///   - Never calls OnGrabbed/OnReleased — only the claw grabs.
///
/// Setup:
///   - Attach to the player's head (the active VR camera under XR Origin), or leave
///     headTransform empty to auto-bind Camera.main.
///   - Dolls must be on the "Doll" layer with a Collider (same requirement as ClawHub).
/// </summary>
[DisallowMultipleComponent]
public class GazeHub : MonoBehaviour
{
    [Header("References (auto-found if null)")]
    [SerializeField] private Transform headTransform;    // VR 카메라(머리). null이면 Camera.main 자동 바인딩
    [SerializeField] private ClawHub clawHub;             // claw 우선권 판정용

    [Header("Detection")]
    [SerializeField] private LayerMask dollLayer;         // null이면 "Doll" 레이어 자동
    [SerializeField] private float maxGazeDistance = 3f;  // 시선이 닿는 최대 거리(m)
    [SerializeField] private float dwellToTrigger = 0.4f; // 이 시간 이상 응시해야 반응 시작(흘끗 방지)

    private IMoodReaction currentGazeReaction;  // 현재 시선이 운전 중인 반응
    private IMoodReaction pendingReaction;      // dwell 측정 중인 후보
    private float dwellTimer;

    private void Awake()
    {
        // headTransform 미지정 시 메인 카메라(=VR 머리) 자동 사용
        if (headTransform == null && Camera.main != null)
            headTransform = Camera.main.transform;

        // ClawHub 자동 탐색 (Unity 6 API)
        if (clawHub == null)
            clawHub = FindFirstObjectByType<ClawHub>();

        // dollLayer 기본값 보정 (ClawHub와 동일하게 "Doll" 레이어)
        if (dollLayer.value == 0)
            dollLayer = LayerMask.GetMask("Doll");
    }

    private void Update()
    {
        if (headTransform == null) return;

        IMoodReaction gazed = RaycastDoll();

        // claw 우선권: claw가 현재 hover/grab 중인 인형은 시선이 건드리지 않음
        if (gazed != null && clawHub != null && ReferenceEquals(gazed, clawHub.CurrentReaction))
            gazed = null;

        // claw가 우리가 운전하던 인형을 가져갔으면 OnRetreat 없이 조용히 양보
        if (currentGazeReaction != null && clawHub != null
            && ReferenceEquals(currentGazeReaction, clawHub.CurrentReaction))
        {
            currentGazeReaction = null;
            pendingReaction = null;
            dwellTimer = 0f;
        }

        // 이미 같은 대상을 운전 중(혹은 둘 다 null)이면 dwell 리셋만 하고 종료
        if (ReferenceEquals(gazed, currentGazeReaction))
        {
            pendingReaction = null;
            dwellTimer = 0f;
            return;
        }

        // 시선이 아무 인형도 안 보면 → 현재 반응 종료
        if (gazed == null)
        {
            currentGazeReaction?.OnRetreat();
            currentGazeReaction = null;
            pendingReaction = null;
            dwellTimer = 0f;
            return;
        }

        // 새 인형 후보 → dwell 누적
        if (ReferenceEquals(gazed, pendingReaction))
        {
            dwellTimer += Time.deltaTime;
        }
        else
        {
            pendingReaction = gazed;
            dwellTimer = 0f;
        }

        // dwell 충족 → 이전 반응 종료 후 새 반응 시작
        if (dwellTimer >= dwellToTrigger)
        {
            currentGazeReaction?.OnRetreat();
            currentGazeReaction = gazed;
            currentGazeReaction.OnApproach();
            pendingReaction = null;
            dwellTimer = 0f;
        }
    }

    /// <summary>머리 정면 레이로 dollLayer를 쏴서 응시 중인 IMoodReaction을 반환(없으면 null).</summary>
    private IMoodReaction RaycastDoll()
    {
        if (Physics.Raycast(headTransform.position, headTransform.forward,
                            out RaycastHit hit, maxGazeDistance, dollLayer))
        {
            return hit.collider.GetComponent<IMoodReaction>()
                   ?? hit.collider.GetComponentInParent<IMoodReaction>();
        }
        return null;
    }

    private void OnDisable()
    {
        // 비활성화 시 진행 중인 시선 반응 정리 (떨림 등이 멈춘 채 남지 않도록)
        currentGazeReaction?.OnRetreat();
        currentGazeReaction = null;
        pendingReaction = null;
        dwellTimer = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        if (headTransform == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(headTransform.position,
                        headTransform.position + headTransform.forward * maxGazeDistance);
    }
}
