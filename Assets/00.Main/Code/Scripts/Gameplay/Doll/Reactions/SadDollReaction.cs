using System.Collections;
using UnityEngine;

/// <summary>
/// Sad doll: droops downward while hovered; emits a tear particle and sighs when grabbed.
/// </summary>
[DisallowMultipleComponent]
public class SadDollReaction : MonoBehaviour, IMoodReaction
{
    [Header("Hover droop")]
    [SerializeField] private float droopHeight = 0.02f;
    [SerializeField] private float droopTransitionDuration = 0.25f;

    [Header("Grab tears")]
    [SerializeField] private ParticleSystem tearParticles;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sighClip;
    // Changed: approach 사운드 지원 추가. Why: 집게 접근 시 시각+청각 반응을 동시에 제공하기 위함.
    [SerializeField] private AudioClip approachClip;
    [SerializeField] private float approachVolume = 0.20f;
    [SerializeField] private float approachPitch = 0.85f;
    [SerializeField] private float grabVolume = 0.7f;

    [Header("Animated transform (default: mesh child)")]
    [SerializeField] private Transform visualRoot;

    private Vector3 baseLocalPosition;
    private Coroutine moveCo;

    private void Awake()
    {
        if (visualRoot == null)
        {
            var mr = GetComponentInChildren<MeshRenderer>();
            visualRoot = (mr != null) ? mr.transform : transform;
        }
        baseLocalPosition = visualRoot.localPosition;

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    public void OnApproach()
    {
        StartMove(baseLocalPosition + Vector3.down * droopHeight);
        // Changed: approach 시 사운드 재생. Why: 집게가 가까워졌을 때 청각 힌트 제공.
        PlayApproachClip();
    }

    public void OnRetreat()
    {
        StartMove(baseLocalPosition);
    }

    public void OnGrabbed()
    {
        // Changed: grab 시 볼륨 파라미터 적용. Why: approach보다 큰 소리로 감정 에스컬레이션.
        PlayClipWithVolume(sighClip, grabVolume);
        if (tearParticles != null) tearParticles.Play();
    }

    public void OnReleased()
    {
        if (tearParticles != null) tearParticles.Stop();
        StartMove(baseLocalPosition);
    }

    private void StartMove(Vector3 target)
    {
        if (moveCo != null) StopCoroutine(moveCo);
        moveCo = StartCoroutine(MoveTo(target));
    }

    private IEnumerator MoveTo(Vector3 target)
    {
        Vector3 start = visualRoot.localPosition;
        float t = 0f;
        while (t < droopTransitionDuration)
        {
            t += Time.deltaTime;
            visualRoot.localPosition = Vector3.Lerp(start, target, t / droopTransitionDuration);
            yield return null;
        }
        visualRoot.localPosition = target;
        moveCo = null;
    }

    // Changed: approach/grab 각각 볼륨/피치를 달리하는 재생 메서드 분리.
    // Why: 접근 시 약한 흐느낌, 잡기 시 온전한 슬픔 소리로 에스컬레이션.
    private void PlayApproachClip()
    {
        AudioClip clip = approachClip != null ? approachClip : sighClip;
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
