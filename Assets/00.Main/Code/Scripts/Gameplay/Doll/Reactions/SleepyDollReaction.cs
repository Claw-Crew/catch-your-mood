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
    // Changed: approach 사운드 지원 추가. Why: 집게 접근 시 시각+청각 반응을 동시에 제공하기 위함.
    [SerializeField] private AudioClip approachClip;
    [SerializeField] private float approachVolume = 0.15f;
    [SerializeField] private float approachPitch = 0.90f;
    [SerializeField] private float grabVolume = 0.6f;

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
        // Changed: approach 시 사운드 재생. Why: 집게가 가까워졌을 때 청각 힌트 제공.
        PlayApproachClip();
    }

    public void OnRetreat()
    {
        StopNod();
    }

    public void OnGrabbed()
    {
        StopNod();
        StopYawn();
        // Changed: grab 시 볼륨 파라미터 적용. Why: approach보다 큰 소리로 감정 에스컬레이션.
        PlayClipWithVolume(yawnClip, grabVolume);
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

    // Changed: approach/grab 각각 볼륨/피치를 달리하는 재생 메서드 분리.
    // Why: 접근 시 나른한 숨소리, 잡기 시 풀 하품으로 에스컬레이션.
    private void PlayApproachClip()
    {
        AudioClip clip = approachClip != null ? approachClip : yawnClip;
        if (clip == null || audioSource == null) return;
        audioSource.pitch = approachPitch;
        audioSource.PlayOneShot(clip, approachVolume);
    }

    private void PlayClipWithVolume(AudioClip clip, float volume)
    {
        if (clip == null || audioSource == null) return;
        audioSource.pitch = 1f;
        audioSource.PlayOneShot(clip, volume);
    }
}
