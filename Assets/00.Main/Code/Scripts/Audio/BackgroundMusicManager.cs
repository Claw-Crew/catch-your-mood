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
    // Changed: 0.03 → 0.015. Why: 유저 피드백 — 배경음이 거슬림.
    [SerializeField] private float volume = 0.015f;

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

        // Changed: 톤 성분을 거의 제거하고 핑크노이즈(부드러운 공기/방 소리) 중심으로 교체.
        // Why: 삼각파 톤이 여전히 귀에 걸린다는 유저 피드백. 핑크노이즈만으로
        //       "조용한 방의 공기" 느낌을 만들면 가장 거슬리지 않는 배경이 됨.

        // 핑크노이즈 (1/f 스펙트럼) — 자연의 바람/파도와 같은 주파수 분포
        float b0 = 0f, b1 = 0f, b2 = 0f, b3 = 0f, b4 = 0f, b5 = 0f, b6 = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;

            // Paul Kellet의 핑크노이즈 필터 (정확한 1/f 스펙트럼)
            float white = Random.Range(-1f, 1f);
            b0 = 0.99886f * b0 + white * 0.0555179f;
            b1 = 0.99332f * b1 + white * 0.0750759f;
            b2 = 0.96900f * b2 + white * 0.1538520f;
            b3 = 0.86650f * b3 + white * 0.3104856f;
            b4 = 0.55000f * b4 + white * 0.5329522f;
            b5 = -0.7616f * b5 - white * 0.0168980f;
            float pink = (b0 + b1 + b2 + b3 + b4 + b5 + b6 + white * 0.5362f) * 0.11f;
            b6 = white * 0.115926f;

            // 아주 미미한 저음 톤 (거의 안 들리지만 공간감 부여)
            float subtleTone = Mathf.Sin(2f * Mathf.PI * 65.41f * t) * 0.03f; // C2, 거의 서브베이스

            float sample = pink + subtleTone;

            // 느린 LFO (0.06Hz): 숨 쉬는 듯한 볼륨 변동
            float lfo = 0.90f + 0.10f * Mathf.Sin(2f * Mathf.PI * 0.06f * t);
            sample *= lfo;

            // 심리스 루프 크로스페이드 (앞뒤 1.5초)
            float fadeZone = 1.5f;
            if (t < fadeZone)
                sample *= t / fadeZone;
            else if (t > duration - fadeZone)
                sample *= (duration - t) / fadeZone;

            samples[i] = Mathf.Clamp(sample, -1f, 1f);
        }

        ambientClip.SetData(samples, 0);
    }

}
