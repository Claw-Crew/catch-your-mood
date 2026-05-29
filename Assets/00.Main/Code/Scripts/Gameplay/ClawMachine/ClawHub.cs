using UnityEngine;
using System.Collections.Generic;

public class ClawHub : MonoBehaviour
{
    [Header("Claw")]
    public Transform claw;

    [Header("Distance Settings")]
    public float approachDistance = 0.3f;
    public float grabDistance = 0.1f;

    [Header("Detection")]
    public LayerMask dollLayer;

    private IMoodReaction currentReaction;
    // Changed: 시선 디스패처(GazeHub)가 claw 우선권을 판정할 수 있도록 읽기 전용 노출.
    // Why: GazeHub가 claw이 현재 hover/grab 중인 인형을 건드리지 않게 하기 위함.
    public IMoodReaction CurrentReaction => currentReaction;
    private Rigidbody grabbedRb;
    private bool grabbedWasKinematic;
    private bool isGrabbed;
    private bool grabEnabled;

    private HashSet<IMoodReaction> triedReactions = new();

    private void Awake()
    {
        // Changed: 수동 배치/기존 씬에서도 Hub와 Doll Layer 기본값을 자동 보정.
        // Why: inspector 참조 누락 시 집게-인형 반응이 조용히 실패하는 것을 막기 위함.
        if (claw == null)
        {
            Transform hub = transform.Find("Hub");
            claw = hub != null ? hub : transform;
        }

        if (dollLayer.value == 0)
            dollLayer = LayerMask.GetMask("Doll");
    }

    private void Update()
    {
        // Changed: Hub 위치와 Doll Layer만 사용해 인형 반응을 판정.
        // Why: XRGrabInteractable 없이 실제 집게 끝 기준으로 approach/grab을 처리하기 위함.
        if (claw == null) return;

        if (isGrabbed)
        {
            HandleGrabbedUpdate();
            return;
        }

        DetectClosestDoll();
    }

    private void DetectClosestDoll()
    {
        Collider[] hits = Physics.OverlapSphere(claw.position, approachDistance, dollLayer);

        IMoodReaction closestReaction = null;
        Rigidbody closestRb = null;
        float minDist = float.MaxValue;

        foreach (var hit in hits)
        {
            IMoodReaction reaction = hit.GetComponent<IMoodReaction>() ?? hit.GetComponentInParent<IMoodReaction>();
            if (reaction == null) continue;

            // Changed: pending-respawn dolls are ignored by proximity grabbing.
            // Why: a doll released for respawn must not be reacquired before DollRespawnManager resets it.
            Rigidbody hitRb = hit.attachedRigidbody;
            if (hitRb == null && reaction is MonoBehaviour reactionMono)
                hitRb = reactionMono.GetComponent<Rigidbody>();
            if (IsRespawnPending(hitRb))
                continue;

            float dist = Vector3.Distance(claw.position, hit.ClosestPoint(claw.position));
            if (dist >= minDist) continue;

            minDist = dist;
            closestReaction = reaction;
            closestRb = hitRb;
        }

        if (closestReaction == null)
        {
            ClearCurrentReaction();
            return;
        }

        if (closestReaction != currentReaction)
        {
            currentReaction?.OnRetreat();
            currentReaction = closestReaction;
            currentReaction.OnApproach();

            if (!triedReactions.Contains(currentReaction))
            {
                triedReactions.Add(currentReaction);

                GameResultManager.Instance.RegisterTry();

                Debug.Log("Try Registered");
            }
        }

        if (grabEnabled && minDist < grabDistance)
            StartGrab(closestRb);
    }

    private void StartGrab(Rigidbody targetRb)
    {
        if (currentReaction == null || targetRb == null) return;

        // Changed: velocity is cleared only while the target Rigidbody is non-kinematic, before claw ownership switches it to kinematic.
        // Why: grabbing an already-kinematic doll must not emit Unity velocity warnings.
        isGrabbed = true;
        grabbedRb = targetRb;
        grabbedWasKinematic = grabbedRb.isKinematic;
        ClearVelocitiesIfDynamic(grabbedRb);
        grabbedRb.isKinematic = true;

        currentReaction.OnRetreat();
        currentReaction.OnGrabbed();
    }

    private void HandleGrabbedUpdate()
    {
        if (grabbedRb == null)
        {
            isGrabbed = false;
            ClearCurrentReaction();
            return;
        }

        // Changed: pending-respawn ownership wins over claw movement.
        // Why: once CatchDetector schedules respawn, ClawHub must stop MovePosition for that Rigidbody.
        if (IsRespawnPending(grabbedRb))
        {
            ReleaseGrabbedInternal(false);
            return;
        }

        grabbedRb.MovePosition(claw.position);
    }

    public void ReleaseGrabbed()
    {
        // Changed: ClawTestController Release 상태에서 명시적으로 집은 인형을 놓을 수 있게 함.
        // Why: ClawHub가 XRI selectExited를 쓰지 않으므로 별도 release 호출이 필요함.
        if (!isGrabbed) return;

        ReleaseGrabbedInternal(true);
    }

    public bool ReleaseGrabbedIfMatches(Rigidbody targetRb)
    {
        // Changed: DollRespawnManager가 특정 Rigidbody에 대한 claw 소유권만 해제할 수 있는 targeted release 추가.
        // Why: 리스폰 중인 인형을 ClawHub가 계속 MovePosition으로 이동시키는 충돌을 막기 위함.
        if (!isGrabbed || grabbedRb == null || grabbedRb != targetRb)
            return false;

        ReleaseGrabbedInternal(false);
        return true;
    }

    private void ReleaseGrabbedInternal(bool restoreRigidbodyState)
    {
        // Changed: release 정리 절차를 일반 release와 respawn release가 공유하되 Rigidbody 복원 여부를 분리.
        // Why: respawn manager가 곧 freeze/reset할 Rigidbody를 ClawHub가 다시 만지지 않게 하기 위함.
        currentReaction?.OnReleased();
        if (grabbedRb != null && restoreRigidbodyState)
        {
            ClearVelocitiesIfDynamic(grabbedRb);
            grabbedRb.isKinematic = grabbedWasKinematic;
            ClearVelocitiesIfDynamic(grabbedRb);
        }

        isGrabbed = false;
        grabbedRb = null;
        ClearCurrentReaction();
    }

    public void SetGrabEnabled(bool enabled)
    {
        // Changed: 거리 기반 approach와 실제 grab 권한을 분리.
        // Why: 게임 시작 시 claw가 인형 근처에 있거나 collider와 겹쳐도 하강/집기 시퀀스 전에는 인형이 달라붙지 않게 하기 위함.
        grabEnabled = enabled;
    }

    private void ClearCurrentReaction()
    {
        if (currentReaction == null) return;
        currentReaction.OnRetreat();
        currentReaction = null;
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

    private static bool IsRespawnPending(Rigidbody rb)
    {
        // Changed: ClawHub consults DollRespawnManager before starting or continuing a grab.
        // Why: respawn-pending dolls are owned by the reset flow, not by claw MovePosition.
        if (rb == null)
            return false;

        DollInfo doll = rb.GetComponent<DollInfo>();
        if (doll == null)
            doll = rb.GetComponentInParent<DollInfo>();

        return doll != null && DollRespawnManager.Instance.IsRespawnPending(doll);
    }

    private void OnDrawGizmosSelected()
    {
        if (claw == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(claw.position, approachDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(claw.position, grabDistance);
    }
}

