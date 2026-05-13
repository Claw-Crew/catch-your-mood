using System.Collections;
using UnityEngine;

public abstract class DollReactionBase : MonoBehaviour, IMoodReaction
{
    protected Transform visualRoot;
    private Vector3 baseLocalPosition;
    private Quaternion baseLocalRotation;
    private Vector3 baseLocalScale;
    private Coroutine motionCo;

    protected virtual Vector3 HoverPositionOffset => Vector3.zero;
    protected virtual Vector3 HoverEulerAmplitude => Vector3.zero;
    protected virtual float HoverFrequencyHz => 4f;
    protected virtual Vector3 GrabEuler => Vector3.zero;
    protected virtual Vector3 GrabScaleMultiplier => Vector3.one;

    protected virtual void Awake()
    {
        // Changed: 반응 애니메이션은 Rigidbody 루트가 아니라 첫 MeshRenderer transform에만 적용.
        // Why: 물리 루트 이동과 시각 반응이 충돌해 인형이 튀는 일을 줄이기 위함.
        var renderer = GetComponentInChildren<Renderer>();
        visualRoot = renderer != null ? renderer.transform : transform;
        baseLocalPosition = visualRoot.localPosition;
        baseLocalRotation = visualRoot.localRotation;
        baseLocalScale = visualRoot.localScale;
    }

    public virtual void OnApproach()
    {
        StopMotion();
        motionCo = StartCoroutine(HoverLoop());
    }

    public virtual void OnRetreat()
    {
        StopMotion();
        ResetVisual();
    }

    public virtual void OnGrabbed()
    {
        StopMotion();
        visualRoot.localRotation = baseLocalRotation * Quaternion.Euler(GrabEuler);
        visualRoot.localScale = Vector3.Scale(baseLocalScale, GrabScaleMultiplier);
    }

    public virtual void OnReleased()
    {
        StopMotion();
        ResetVisual();
    }

    private IEnumerator HoverLoop()
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime;
            float s = Mathf.Sin(t * HoverFrequencyHz * 2f * Mathf.PI);
            visualRoot.localPosition = baseLocalPosition + HoverPositionOffset * s;
            visualRoot.localRotation = baseLocalRotation * Quaternion.Euler(HoverEulerAmplitude * s);
            yield return null;
        }
    }

    private void StopMotion()
    {
        if (motionCo == null) return;
        StopCoroutine(motionCo);
        motionCo = null;
    }

    private void ResetVisual()
    {
        visualRoot.localPosition = baseLocalPosition;
        visualRoot.localRotation = baseLocalRotation;
        visualRoot.localScale = baseLocalScale;
    }
}
