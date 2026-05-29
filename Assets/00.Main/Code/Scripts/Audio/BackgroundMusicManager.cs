using UnityEngine;

/// <summary>
/// 프로시저럴 앰비언트 배경음 매니저.
/// C major triad (C4+E4+G4) 기반의 부드러운 패드 사운드를 루프 재생.
/// "편안한 심리검사" 컨셉에 맞춰 매우 낮은 볼륨으로 재생하여 인형 SFX를 방해하지 않음.
///
/// Changed: 새 파일 생성. Why: 배경 음악 시스템이 전혀 없어서 힐링 VR 컨셉에 맞는 앰비언트 추가.
/// </summary>
public class BackgroundMusicManager : MonoBehaviour
{
    [Header("Volume")]
    [Tooltip("배경 볼륨 (0.08~0.12 권장, SFX의 약 15%)")]
    // Changed: 0.10 → 0.03. Why: 유저 피드백 — BGM이 너무 크다.
    [SerializeField] private float volume = 0.03f;

    [Header("Pad Settings")]
    [Tooltip("패드 기본 주파수들 (Hz). 기본값: C4=261.63, E4=329.63, G4=392.00")]
    [SerializeField] private float[] frequencies = { 261.63f, 329.63f, 392.00f };

    [Tooltip("각 주파수의 진폭 비율 (합이 1.0이 되도록)")]
    [SerializeField] private float[] amplitudes = { 0.4f, 0.35f, 0.25f };

    [Tooltip("LFO로 볼륨을 느리게 흔드는 주파수 (Hz)")]
    [SerializeField] private float lfoFrequency = 0.15f;

    [Tooltip("LFO 깊이 (0~1). 0이면 일정, 1이면 최대 변동")]
    [SerializeField] private float lfoDepth = 0.3f;

    [Header("Fade")]
    [SerializeField] private float fadeInDuration = 3f;

    private AudioSource audioSource;
    private AudioClip ambientClip;
    private float fadeTimer;
    private bool isFadingIn = true;

    private void Awake()
    {
        // Changed: AudioSource를 2D(spatialBlend=0)로 설정하여 위치 무관 배경음.
        // Why: 배경 패드는 공간적 위치가 없는 전체 분위기 사운드.
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = 0f;
        audioSource.priority = 256; // 가장 낮은 우선순위 (인형 SFX가 우선)

        GenerateAmbientClip();

        audioSource.clip = ambientClip;
        audioSource.Play();
    }

    private void Update()
    {
        // Changed: 부드러운 페이드인으로 갑작스러운 배경음 등장 방지.
        // Why: 씬 시작 시 자연스러운 볼륨 증가.
        if (isFadingIn)
        {
            fadeTimer += Time.deltaTime;
            float t = Mathf.Clamp01(fadeTimer / fadeInDuration);
            audioSource.volume = volume * t;
            if (t >= 1f) isFadingIn = false;
        }
    }

    private void GenerateAmbientClip()
    {
        // Changed: 프로시저럴 AudioClip 생성 (C major triad sine wave blend + LFO).
        // Why: 외부 파일 없이 부드러운 앰비언트 패드를 생성하기 위함.
        int sampleRate = 44100;
        float duration = 10f; // 10초 루프
        int sampleCount = (int)(sampleRate * duration);

        ambientClip = AudioClip.Create("ProceduralAmbient", sampleCount, 1, sampleRate, false);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float sample = 0f;

            // 각 주파수의 사인파를 진폭 비율에 따라 합산
            for (int f = 0; f < frequencies.Length && f < amplitudes.Length; f++)
            {
                sample += Mathf.Sin(2f * Mathf.PI * frequencies[f] * t) * amplitudes[f];
            }

            // LFO: 느린 볼륨 변동으로 살아있는 느낌
            float lfo = 1f - lfoDepth * 0.5f * (1f + Mathf.Sin(2f * Mathf.PI * lfoFrequency * t));
            sample *= lfo;

            // 부드러운 루프 연결: 마지막 0.5초 크로스페이드
            float fadeZone = 0.5f;
            if (t > duration - fadeZone)
            {
                float fadeOut = (duration - t) / fadeZone;
                sample *= fadeOut;
            }
            else if (t < fadeZone)
            {
                float fadeIn = t / fadeZone;
                sample *= fadeIn;
            }

            samples[i] = sample * 0.3f; // 전체 진폭을 낮게 유지
        }

        ambientClip.SetData(samples, 0);
    }
}
