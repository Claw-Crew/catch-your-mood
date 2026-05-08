using System.Collections;
using UnityEngine;

/// <summary>
/// Happy doll: bounces in place while the claw hovers; spins once and giggles when grabbed.
/// </summary>
[DisallowMultipleComponent]
public class HappyDollReaction : MonoBehaviour, IMoodReaction
{
    [Header("Hover bounce")]
    [SerializeField] private float bounceHeight = 0.05f;
    [SerializeField] private float bounceFrequencyHz = 4f;

    [Header("Grab spin")]
    [SerializeField] private float spinDuration = 0.6f;
    [SerializeField] private float spinDegrees = 360f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip giggleClip;

    [Header("Animated transform (default: mesh child)")]
    [SerializeField] private Transform visualRoot;

    private Vector3 baseLocalPosition;
    private Quaternion baseLocalRotation;
    private Coroutine bounceCo;
    private Coroutine spinCo;

    private void Awake()
    {
        if (visualRoot == null)
        {
            var mr = GetComponentInChildren<MeshRenderer>();
            visualRoot = (mr != null) ? mr.transform : transform;
        }
        baseLocalPosition = visualRoot.localPosition;
        baseLocalRotation = visualRoot.localRotation;

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    public void OnApproach()
    {
        StopBounce();
        bounceCo = StartCoroutine(BounceLoop());
    }

    public void OnRetreat()
    {
        StopBounce();
    }

    public void OnGrabbed()
    {
        StopBounce();
        StopSpin();
        PlayClip(giggleClip);
        spinCo = StartCoroutine(SpinOnce());
    }

    public void OnReleased()
    {
        StopSpin();
    }

    private IEnumerator BounceLoop()
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime;
            float y = Mathf.Sin(t * bounceFrequencyHz * 2f * Mathf.PI) * bounceHeight;
            visualRoot.localPosition = baseLocalPosition + new Vector3(0f, y, 0f);
            yield return null;
        }
    }

    private IEnumerator SpinOnce()
    {
        float t = 0f;
        while (t < spinDuration)
        {
            t += Time.deltaTime;
            float angle = Mathf.Lerp(0f, spinDegrees, t / spinDuration);
            visualRoot.localRotation = baseLocalRotation * Quaternion.Euler(0f, angle, 0f);
            yield return null;
        }
        visualRoot.localRotation = baseLocalRotation;
        spinCo = null;
    }

    private void StopBounce()
    {
        if (bounceCo != null) { StopCoroutine(bounceCo); bounceCo = null; }
        visualRoot.localPosition = baseLocalPosition;
    }

    private void StopSpin()
    {
        if (spinCo != null) { StopCoroutine(spinCo); spinCo = null; }
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }
}
