using UnityEngine;

/// <summary>
/// 프로시저럴 앰비언트 배경음 매니저.
/// Changed: 순수 사인파 → 삼각파+핑크노이즈+디튠 조합으로 전면 재작성.
/// Why: 순수 사인파는 테스트 톤/호러 드론 느낌이라 유저 피드백에서 "기괴하다"고 평가됨.
///      삼각파는 배음 구조가 있어 따뜻하고, 핑크노이즈가 공간감을, 디튠이 코러스 효과를 줌.
/// </summary>
public class BackgroundMusicManager : MonoBehaviour
{
    [Header("Volume")]
    [SerializeField] private float volume = 0.03f;

    [Header("Fade")]
    [SerializeField] private float fadeInDuration = 4f;

    private AudioSource audioSource;
    private AudioClip ambientClip;
    private float fadeTimer;
    private bool isFadingIn = true;

    private void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = 0f;
        audioSource.priority = 256;

        GenerateWarmPad();

        audioSource.clip = ambientClip;
        audioSource.Play();
    }

    private void Update()
    {
        if (isFadingIn)
        {
            fadeTimer += Time.deltaTime;
            float t = Mathf.Clamp01(fadeTimer / fadeInDuration);
            audioSource.volume = volume * t;
            if (t >= 1f) isFadingIn = false;
        }
    }

    // Changed: 삼각파+핑크노이즈+디튠으로 따뜻한 앰비언트 패드 생성.
    // Why: 사인파는 배음이 없어 차갑고 기계적이지만, 삼각파는 홀수 배음을 가져 따뜻한 음색.
    //      핑크노이즈는 1/f 스펙트럼으로 자연의 소리(바람, 파도)와 유사.
    //      디튠은 같은 음을 미세하게 다른 주파수로 겹쳐 코러스 효과.
    private void GenerateWarmPad()
    {
        int sampleRate = 44100;
        float duration = 12f;
        int sampleCount = (int)(sampleRate * duration);

        ambientClip = AudioClip.Create("WarmAmbientPad", sampleCount, 1, sampleRate, false);
        float[] samples = new float[sampleCount];

        // C3(130.81) + E3(164.81) + G3(196.00) — 한 옥타브 아래로 이동하여 따뜻한 음역
        float[] baseFreqs = { 130.81f, 164.81f, 196.00f };
        // 디튠 비율 (±1~2Hz 차이로 코러스 효과)
        float[] detuneHz = { 1.2f, -0.8f, 1.5f };
        float[] amps = { 0.30f, 0.25f, 0.20f };

        // 핑크노이즈 생성용 (Voss-McCartney 알고리즘 간략화)
        float pinkState = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float sample = 0f;

            // 삼각파 패드: 각 주파수에 대해 원본 + 디튠 레이어
            for (int f = 0; f < baseFreqs.Length; f++)
            {
                // 원본 삼각파
                sample += TriangleWave(baseFreqs[f], t) * amps[f];
                // 디튠된 삼각파 (코러스 효과)
                sample += TriangleWave(baseFreqs[f] + detuneHz[f], t) * amps[f] * 0.6f;
            }

            // 핑크노이즈 레이어: 공간감과 따뜻함 추가
            float white = Random.Range(-1f, 1f);
            pinkState = pinkState * 0.997f + white * 0.003f;
            sample += pinkState * 0.15f;

            // 느린 LFO (0.08Hz): 숨 쉬는 듯한 자연스러운 볼륨 변동
            float lfo = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.08f * t);
            sample *= lfo;

            // 심리스 루프 크로스페이드 (앞뒤 1초)
            float fadeZone = 1f;
            if (t < fadeZone)
                sample *= t / fadeZone;
            else if (t > duration - fadeZone)
                sample *= (duration - t) / fadeZone;

            // 전체 진폭 제한 (클리핑 방지)
            samples[i] = Mathf.Clamp(sample * 0.25f, -1f, 1f);
        }

        ambientClip.SetData(samples, 0);
    }

    // 삼각파: 홀수 배음(3차, 5차...)을 가져 사인파보다 따뜻한 음색
    private static float TriangleWave(float freq, float t)
    {
        float phase = (freq * t) % 1f;
        return 4f * Mathf.Abs(phase - 0.5f) - 1f;
    }
}
