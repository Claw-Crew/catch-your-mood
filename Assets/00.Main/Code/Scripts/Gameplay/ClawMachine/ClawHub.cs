using UnityEngine;

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
    private Rigidbody grabbedRb;
    private bool grabbedWasKinematic;
    private bool isGrabbed;
    private bool grabEnabled;

    private void Awake()
    {
        // Changed: SceneSetup이 아닌 수동 배치 ClawHub도 Hub/Doll Layer 기본값을 자동 보정.
        // Why: inspector 참조 누락 시 집게-인형 반응이 조용히 실패하는 것을 막기 위함.
        if (claw == null)
        {
            Transform hub = transform.Find("Hub");
            claw = hub != null ? hub : transform;
        }

        if (dollLayer.value == 0)
            dollLayer = LayerMask.GetMask("Doll");
    }

    void Update()
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

            float dist = Vector3.Distance(claw.position, hit.ClosestPoint(claw.position));
            if (dist >= minDist) continue;

            minDist = dist;
            closestReaction = reaction;
            closestRb = hit.attachedRigidbody;
            if (closestRb == null && reaction is MonoBehaviour mono)
                closestRb = mono.GetComponent<Rigidbody>();
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
        }

        if (grabEnabled && minDist < grabDistance)
            StartGrab(closestRb);
    }

    private void StartGrab(Rigidbody targetRb)
    {
        if (currentReaction == null || targetRb == null) return;

        isGrabbed = true;
        grabbedRb = targetRb;
        grabbedWasKinematic = grabbedRb.isKinematic;
        grabbedRb.linearVelocity = Vector3.zero;
        grabbedRb.angularVelocity = Vector3.zero;
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

        grabbedRb.MovePosition(claw.position);
    }

    public void ReleaseGrabbed()
    {
        // Changed: ClawTestController Release 상태에서 명시적으로 집은 인형을 놓을 수 있게 함.
        // Why: ClawHub가 XRI selectExited를 쓰지 않으므로 별도 release 호출이 필요함.
        if (!isGrabbed) return;

        currentReaction?.OnReleased();
        if (grabbedRb != null)
        {
            grabbedRb.isKinematic = grabbedWasKinematic;
            grabbedRb.linearVelocity = Vector3.zero;
            grabbedRb.angularVelocity = Vector3.zero;
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

    private void OnDrawGizmosSelected()
    {
        if (claw == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(claw.position, approachDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(claw.position, grabDistance);
    }
}
