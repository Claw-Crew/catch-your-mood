using UnityEngine;

/// <summary>
/// 배경 음악 매니저 — 실제 음악 파일 재생.
/// Changed: 프로시저럴 생성(사인파/삼각파/핑크노이즈) → 실제 CC0 앰비언트 트랙 재생으로 전면 교체.
/// Why: 프로시저럴 생성은 어떤 방식이든 기괴하거나 거슬림.
///      실제 VR 치료/명상 프로젝트들(Agora-VR, zen-garden, mindful)은 전부 실제 음악 파일 사용.
///      "Calm Ambient 1 (Synthwave 4k)" by The Cynic Project — CC0, OpenGameArt.org
///      느린 패드와 부드러운 코드 진행으로 "편안한 심리검사실" 분위기에 적합.
/// </summary>
public class BackgroundMusicManager : MonoBehaviour
{
    [Header("Music Clip")]
    [Tooltip("배경 음악 AudioClip. 비어있으면 Resources/BGM_CalmAmbient를 자동 로드.")]
    [SerializeField] private AudioClip musicClip;

    [Header("Volume")]
    // Changed: 0.02 → 0.001 (20배 감소). Why: 사용자 요청 — 거의 들리지 않는 수준 (~-60dB).
    [Tooltip("배경 볼륨. ~-60dB 수준. Sleepy approach(0.4)의 1/400.")]
    [SerializeField] private float volume = 0.001f;

    [Header("Fade")]
    [SerializeField] private float fadeInDuration = 4f;

    private AudioSource audioSource;
    private float fadeTimer;
    private bool isFadingIn = true;

    private void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;      // 2D — 위치 무관 배경음
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = 0f;
        audioSource.priority = 256;         // 가장 낮은 우선순위 (인형 SFX 우선)

        if (musicClip != null)
        {
            audioSource.clip = musicClip;
            audioSource.Play();
        }
        else
        {
            Debug.LogWarning("[BackgroundMusicManager] musicClip이 할당되지 않음. Inspector에서 AudioClip을 할당하세요.");
        }
    }

    private void Update()
    {
        if (!isFadingIn) return;

        fadeTimer += Time.deltaTime;
        float t = Mathf.Clamp01(fadeTimer / fadeInDuration);
        audioSource.volume = volume * t;
        if (t >= 1f) isFadingIn = false;
    }
}
