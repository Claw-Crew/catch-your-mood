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
    // Changed: approach 사운드를 프로시저럴 음악적 모티프로 교체.
    // Why: grab 보컬의 피치 변형이 기괴하게 들려서, 감정별 고유 톤으로 대체.
    [SerializeField] private float approachVolume = 0.5f;
    [SerializeField] private float grabVolume = 0.9f;
    private AudioClip proceduralApproachClip;

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
        proceduralApproachClip = EmotionApproachSFX.Generate(EmotionType.Angry);
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
