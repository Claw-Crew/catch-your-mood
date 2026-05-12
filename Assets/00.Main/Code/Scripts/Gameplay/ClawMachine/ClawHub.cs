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

    private bool isGrabbed = false;

    void Update()
    {
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
        Collider[] hits = Physics.OverlapSphere(
            claw.position,
            approachDistance,
            dollLayer
        );

        IMoodReaction closestReaction = null;
        Rigidbody closestRb = null;

        float minDist = float.MaxValue;

        foreach (var hit in hits)
        {
            IMoodReaction reaction = hit.GetComponent<IMoodReaction>();

            if (reaction == null)
                reaction = hit.GetComponentInParent<IMoodReaction>();

            if (reaction == null)
                continue;

            float dist = Vector3.Distance(
                claw.position,
                hit.transform.position
            );

            if (dist < minDist)
            {
                minDist = dist;
                closestReaction = reaction;

                if (reaction is MonoBehaviour mono)
                {
                    closestRb = mono.GetComponent<Rigidbody>();
                }
            }
        }

        if (closestReaction != null)
        {
            if (closestReaction != currentReaction)
            {
                currentReaction?.OnRetreat();

                currentReaction = closestReaction;

                currentReaction.OnApproach();
            }

            if (minDist < grabDistance)
            {
                StartGrab(closestRb);
            }
        }
        else
        {
            ClearCurrentReaction();
        }
    }

    private void StartGrab(Rigidbody targetRb)
    {
        if (currentReaction == null || targetRb == null)
            return;

        isGrabbed = true;
        grabbedRb = targetRb;

        grabbedRb.linearVelocity = Vector3.zero;
        grabbedRb.angularVelocity = Vector3.zero;

        currentReaction.OnRetreat();
        currentReaction.OnGrabbed();
    }

    private void HandleGrabbedUpdate()
    {
        if (grabbedRb == null)
            return;

        grabbedRb.MovePosition(claw.position);
    }

    public void ReleaseGrabbed()
    {
        if (!isGrabbed)
            return;

        currentReaction?.OnReleased();

        isGrabbed = false;
        grabbedRb = null;

        ClearCurrentReaction();
    }

    private void ClearCurrentReaction()
    {
        if (currentReaction != null)
        {
            currentReaction.OnRetreat();
            currentReaction = null;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (claw == null)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(claw.position, approachDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(claw.position, grabDistance);
    }
}