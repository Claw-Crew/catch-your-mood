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
    // Changed: approach 사운드 지원 추가. Why: 집게 접근 시 시각+청각 반응을 동시에 제공하기 위함.
    [SerializeField] private AudioClip approachClip;
    [SerializeField] private float approachVolume = 0.25f;
    [SerializeField] private float approachPitch = 1.15f;
    [SerializeField] private float grabVolume = 0.8f;

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

    // Changed: approach/grab 각각 볼륨/피치를 달리하는 재생 메서드 분리.
    // Why: 접근 시 작고 변형된 소리, 잡기 시 크고 원본 소리로 감정 에스컬레이션 표현.
    // Changed: pitch를 같은 프레임에서 리셋하지 않음. PlayOneShot은 AudioSource.pitch를 참조하므로
    // 즉시 리셋하면 재생 중인 소리의 피치도 변경됨. grab 시 명시적으로 1f로 설정.
    private void PlayApproachClip()
    {
        AudioClip clip = approachClip != null ? approachClip : giggleClip;
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
