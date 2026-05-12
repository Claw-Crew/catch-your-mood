using System.Collections;
using UnityEngine;

/// <summary>
/// Sleepy doll: slow head-tilt nods while hovered; yawns when grabbed.
/// </summary>
[DisallowMultipleComponent]
public class SleepyDollReaction : MonoBehaviour, IMoodReaction
{
    [Header("Hover nod")]
    [SerializeField] private float nodAngleDegrees = 8f;
    [SerializeField] private float nodFrequencyHz = 0.6f;

    [Header("Grab yawn")]
    [SerializeField] private float yawnStretchScale = 1.08f;
    [SerializeField] private float yawnDuration = 0.8f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip yawnClip;

    [Header("Animated transform (default: mesh child)")]
    [SerializeField] private Transform visualRoot;

    private Quaternion baseLocalRotation;
    private Vector3 baseLocalScale;
    private Coroutine nodCo;
    private Coroutine yawnCo;

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
        StopNod();
        nodCo = StartCoroutine(NodLoop());
    }

    public void OnRetreat()
    {
        StopNod();
    }

    public void OnGrabbed()
    {
        StopNod();
        StopYawn();
        PlayClip(yawnClip);
        yawnCo = StartCoroutine(YawnOnce());
    }

    public void OnReleased()
    {
        StopYawn();
    }

    private IEnumerator NodLoop()
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime;
            float a = Mathf.Sin(t * nodFrequencyHz * 2f * Mathf.PI) * nodAngleDegrees;
            visualRoot.localRotation = baseLocalRotation * Quaternion.Euler(a, 0f, 0f);
            yield return null;
        }
    }

    private IEnumerator YawnOnce()
    {
        float t = 0f;
        float half = yawnDuration * 0.5f;
        while (t < half)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(1f, yawnStretchScale, t / half);
            visualRoot.localScale = baseLocalScale * s;
            yield return null;
        }
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(yawnStretchScale, 1f, t / half);
            visualRoot.localScale = baseLocalScale * s;
            yield return null;
        }
        visualRoot.localScale = baseLocalScale;
        yawnCo = null;
    }

    private void StopNod()
    {
        if (nodCo != null) { StopCoroutine(nodCo); nodCo = null; }
        visualRoot.localRotation = baseLocalRotation;
    }

    private void StopYawn()
    {
        if (yawnCo != null) { StopCoroutine(yawnCo); yawnCo = null; }
        visualRoot.localScale = baseLocalScale;
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }
}
