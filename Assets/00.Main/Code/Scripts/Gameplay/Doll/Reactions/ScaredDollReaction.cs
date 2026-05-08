using System.Collections;
using UnityEngine;

/// <summary>
/// Scared doll: jitters and shrinks slightly while hovered; recoils and screams when grabbed.
/// </summary>
[DisallowMultipleComponent]
public class ScaredDollReaction : MonoBehaviour, IMoodReaction
{
    [Header("Hover jitter")]
    [SerializeField] private float jitterAngleDegrees = 5f;
    [SerializeField] private float jitterFrequencyHz = 22f;
    [SerializeField] private float shrinkScale = 0.92f;
    [SerializeField] private float shrinkTransitionDuration = 0.2f;

    [Header("Grab recoil")]
    [SerializeField] private float recoilAngleDegrees = 14f;
    [SerializeField] private float recoilDuration = 0.35f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip screamClip;

    [Header("Animated transform (default: mesh child)")]
    [SerializeField] private Transform visualRoot;

    private Quaternion baseLocalRotation;
    private Vector3 baseLocalScale;
    private Coroutine jitterCo;
    private Coroutine scaleCo;
    private Coroutine recoilCo;

    private void Awake()
    {
        if (visualRoot == null)
        {
            var mr = GetComponentInChildren<MeshRenderer>();
            visualRoot = (mr != null) ? mr.transform : transform;
        }
        baseLocalRotation = visualRoot.localRotation;
        baseLocalScale = visualRoot.localScale;

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    public void OnApproach()
    {
        StopJitter();
        jitterCo = StartCoroutine(JitterLoop(jitterAngleDegrees));
        StartScale(baseLocalScale * shrinkScale);
    }

    public void OnRetreat()
    {
        StopJitter();
        StartScale(baseLocalScale);
    }

    public void OnGrabbed()
    {
        StopJitter();
        StopRecoil();
        PlayClip(screamClip);
        recoilCo = StartCoroutine(RecoilOnce());
    }

    public void OnReleased()
    {
        StopRecoil();
        StartScale(baseLocalScale);
    }

    private IEnumerator JitterLoop(float amplitude)
    {
        while (true)
        {
            float a = (Random.value * 2f - 1f) * amplitude;
            float b = (Random.value * 2f - 1f) * amplitude;
            visualRoot.localRotation = baseLocalRotation * Quaternion.Euler(a, 0f, b);
            yield return new WaitForSeconds(1f / jitterFrequencyHz);
        }
    }

    private IEnumerator RecoilOnce()
    {
        float t = 0f;
        while (t < recoilDuration)
        {
            t += Time.deltaTime;
            float decay = 1f - (t / recoilDuration);
            float a = (Random.value * 2f - 1f) * recoilAngleDegrees * decay;
            float b = (Random.value * 2f - 1f) * recoilAngleDegrees * decay;
            visualRoot.localRotation = baseLocalRotation * Quaternion.Euler(a, 0f, b);
            yield return null;
        }
        visualRoot.localRotation = baseLocalRotation;
        recoilCo = null;
    }

    private void StartScale(Vector3 target)
    {
        if (scaleCo != null) StopCoroutine(scaleCo);
        scaleCo = StartCoroutine(ScaleTo(target));
    }

    private IEnumerator ScaleTo(Vector3 target)
    {
        Vector3 start = visualRoot.localScale;
        float t = 0f;
        while (t < shrinkTransitionDuration)
        {
            t += Time.deltaTime;
            visualRoot.localScale = Vector3.Lerp(start, target, t / shrinkTransitionDuration);
            yield return null;
        }
        visualRoot.localScale = target;
        scaleCo = null;
    }

    private void StopJitter()
    {
        if (jitterCo != null) { StopCoroutine(jitterCo); jitterCo = null; }
        visualRoot.localRotation = baseLocalRotation;
    }

    private void StopRecoil()
    {
        if (recoilCo != null) { StopCoroutine(recoilCo); recoilCo = null; }
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }
}
