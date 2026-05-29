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
    // Changed: approach 사운드를 프로시저럴 음악적 모티프로 교체.
    // Why: grab 보컬의 피치 변형이 기괴하게 들려서, 감정별 고유 톤(벨/팝/버스트)으로 대체.
    [SerializeField] private float approachVolume = 0.5f;
    [SerializeField] private float grabVolume = 0.8f;
    private AudioClip proceduralApproachClip;

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
        proceduralApproachClip = EmotionApproachSFX.Generate(EmotionType.Happy);
    }

    public void OnApproach()
    {
        StopBounce();
        bounceCo = StartCoroutine(BounceLoop());
        // Changed: approach 시 사운드 재생. Why: 집게가 가까워졌을 때 청각 힌트 제공.
        PlayApproachClip();
    }

    public void OnRetreat()
    {
        StopBounce();
    }

    public void OnGrabbed()
    {
        StopBounce();
        StopSpin();
        // Changed: grab 시 볼륨 파라미터 적용. Why: approach보다 큰 소리로 감정 에스컬레이션.
        PlayClipWithVolume(giggleClip, grabVolume);
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

    // Changed: approach = 프로시저럴 음악적 모티프, grab = 원본 보컬 클립.
    // Why: 보컬 피치 변형의 기괴함을 제거하고, 짧은 톤으로 감정 힌트만 전달.
    private void PlayApproachClip()
    {
        if (proceduralApproachClip == null || audioSource == null) return;
        audioSource.pitch = 1f;
        audioSource.PlayOneShot(proceduralApproachClip, approachVolume);
    }

    private void PlayClipWithVolume(AudioClip clip, float volume)
    {
        if (clip == null || audioSource == null) return;
        audioSource.pitch = 1f;
        audioSource.PlayOneShot(clip, volume);
    }
}
