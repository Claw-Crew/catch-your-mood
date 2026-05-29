using System.Collections;
using UnityEngine;

/// <summary>
/// Angry doll: trembles while the claw hovers; gives a hard shake and shouts when grabbed.
/// </summary>
[DisallowMultipleComponent]
public class AngryDollReaction : MonoBehaviour, IMoodReaction
{
    [Header("Hover tremble")]
    [SerializeField] private float trembleAngleDegrees = 3f;
    [SerializeField] private float trembleFrequencyHz = 18f;

    [Header("Grab shake")]
    [SerializeField] private float shakeAngleDegrees = 12f;
    [SerializeField] private float shakeDuration = 0.4f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip shoutClip;
    // Changed: approach 사운드 지원 추가. Why: 집게 접근 시 시각+청각 반응을 동시에 제공하기 위함.
    [SerializeField] private AudioClip approachClip;
    [SerializeField] private float approachVolume = 0.20f;
    [SerializeField] private float approachPitch = 0.70f;
    [SerializeField] private float grabVolume = 0.9f;

    [Header("Animated transform (default: mesh child)")]
    [SerializeField] private Transform visualRoot;

    private Quaternion baseLocalRotation;
    private Coroutine trembleCo;
    private Coroutine shakeCo;

    private void Awake()
    {
        if (visualRoot == null)
        {
            var mr = GetComponentInChildren<MeshRenderer>();
            visualRoot = (mr != null) ? mr.transform : transform;
        }
        baseLocalRotation = visualRoot.localRotation;

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    public void OnApproach()
    {
        StopTremble();
        trembleCo = StartCoroutine(TrembleLoop());
        // Changed: approach 시 사운드 재생. Why: 집게가 가까워졌을 때 청각 힌트 제공.
        PlayApproachClip();
    }

    public void OnRetreat()
    {
        StopTremble();
    }

    public void OnGrabbed()
    {
        StopTremble();
        StopShake();
        // Changed: grab 시 볼륨 파라미터 적용. Why: approach보다 큰 소리로 감정 에스컬레이션.
        PlayClipWithVolume(shoutClip, grabVolume);
        shakeCo = StartCoroutine(ShakeOnce());
    }

    public void OnReleased()
    {
        StopShake();
    }

    private IEnumerator TrembleLoop()
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime;
            float a = Mathf.Sin(t * trembleFrequencyHz * 2f * Mathf.PI) * trembleAngleDegrees;
            visualRoot.localRotation = baseLocalRotation * Quaternion.Euler(0f, 0f, a);
            yield return null;
        }
    }

    private IEnumerator ShakeOnce()
    {
        float t = 0f;
        while (t < shakeDuration)
        {
            t += Time.deltaTime;
            float decay = 1f - (t / shakeDuration);
            float a = (Random.value * 2f - 1f) * shakeAngleDegrees * decay;
            float b = (Random.value * 2f - 1f) * shakeAngleDegrees * decay;
            visualRoot.localRotation = baseLocalRotation * Quaternion.Euler(a, 0f, b);
            yield return null;
        }
        visualRoot.localRotation = baseLocalRotation;
        shakeCo = null;
    }

    private void StopTremble()
    {
        if (trembleCo != null) { StopCoroutine(trembleCo); trembleCo = null; }
        visualRoot.localRotation = baseLocalRotation;
    }

    private void StopShake()
    {
        if (shakeCo != null) { StopCoroutine(shakeCo); shakeCo = null; }
    }

    // Changed: approach/grab 각각 볼륨/피치를 달리하는 재생 메서드 분리.
    // Why: 접근 시 낮은 으르렁 소리, 잡기 시 폭발적 분노 소리로 에스컬레이션.
    private void PlayApproachClip()
    {
        AudioClip clip = approachClip != null ? approachClip : shoutClip;
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
