using UnityEngine;

/// <summary>
/// 감정별 프로시저럴 접근(approach) 사운드 생성.
/// Changed: 새 파일 생성. Why: 기존 approach 사운드가 grab 보컬의 피치 변형이라 기괴하게 들림.
///          Slime Rancher 방식처럼 보컬 대신 짧은 음악적 모티프(벨, 팝, 바운스)를 사용.
///
/// 각 감정의 음악적 특성 (Russell's Circumplex + Slime Rancher 원칙):
///   Happy:  밝은 상승 차임 (C5→E5, 0.35s) — 메이저키, 높은 음역, 빠른 어택
///   Sad:    부드러운 하강 톤 (E4→C4, 0.5s) — 마이너키, 낮은 음역, 느린 어택
///   Angry:  낮은 퍼커시브 버스트 (80Hz, 0.25s) — 어그레시브, 짧고 강한
///   Sleepy: 나른한 부드러운 스윕 (A3→F3, 0.6s) — 매우 부드럽고 느린
///   Scared: 빠른 떨림 고음 (B5 tremolo, 0.3s) — 불안하고 빠른
///   Serene: 맑은 벨 톤 (G4 + 배음, 0.5s) — 깨끗하고 여운 있는
/// </summary>
public static class EmotionApproachSFX
{
    private const int SampleRate = 44100;

    public static AudioClip Generate(EmotionType emotion)
    {
        return emotion switch
        {
            EmotionType.Happy  => GenerateHappy(),
            EmotionType.Sad    => GenerateSad(),
            EmotionType.Angry  => GenerateAngry(),
            EmotionType.Sleepy => GenerateSleepy(),
            EmotionType.Scared => GenerateScared(),
            EmotionType.Serene => GenerateSerene(),
            _                  => GenerateHappy()
        };
    }

    // Happy: 밝은 2음 상승 차임 (C5→E5)
    // Why: 메이저 3도 상승은 기쁨/밝음의 보편적 표현 (midi-emotion 연구 기반)
    private static AudioClip GenerateHappy()
    {
        float dur = 0.35f;
        int count = (int)(SampleRate * dur);
        float[] s = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / SampleRate;
            float progress = t / dur;

            // C5(523Hz) → E5(659Hz) 글라이드
            float freq = Mathf.Lerp(523f, 659f, progress);
            float tone = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.6f;
            // 밝은 배음 추가 (2배음)
            tone += Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.2f;

            // 벨 엔벨로프: 빠른 어택, 중간 디케이
            float env = Mathf.Exp(-3f * t) * Mathf.Min(1f, t * 80f);
            s[i] = tone * env;
        }

        return MakeClip("Approach_Happy", s);
    }

    // Sad: 부드러운 하강 톤 (E4→C4)
    // Why: 마이너 3도 하강은 슬픔의 보편적 음악적 표현
    private static AudioClip GenerateSad()
    {
        float dur = 0.5f;
        int count = (int)(SampleRate * dur);
        float[] s = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / SampleRate;
            float progress = t / dur;

            // E4(330Hz) → C4(262Hz) 천천히 하강
            float freq = Mathf.Lerp(330f, 262f, progress * progress);
            float tone = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.7f;

            // 느린 어택, 느린 디케이
            float env = Mathf.Sin(Mathf.PI * progress) * 0.8f;
            s[i] = tone * env;
        }

        return MakeClip("Approach_Sad", s);
    }

    // Angry: 낮은 퍼커시브 버스트
    // Why: 저주파 + 빠른 어택 = 공격적/긴장 표현
    private static AudioClip GenerateAngry()
    {
        float dur = 0.25f;
        int count = (int)(SampleRate * dur);
        float[] s = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / SampleRate;

            // 80Hz 기본 + 160Hz/240Hz 배음 (풍성한 저음)
            float tone = Mathf.Sin(2f * Mathf.PI * 80f * t) * 0.5f;
            tone += Mathf.Sin(2f * Mathf.PI * 160f * t) * 0.3f;
            tone += Mathf.Sin(2f * Mathf.PI * 240f * t) * 0.15f;

            // 즉각 어택, 빠른 디케이
            float env = Mathf.Exp(-8f * t);
            s[i] = tone * env;
        }

        return MakeClip("Approach_Angry", s);
    }

    // Sleepy: 나른한 부드러운 하강 스윕 (A3→F3)
    // Why: 매우 느린 변화 + 낮은 음역 = 졸림/이완
    private static AudioClip GenerateSleepy()
    {
        float dur = 0.6f;
        int count = (int)(SampleRate * dur);
        float[] s = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / SampleRate;
            float progress = t / dur;

            // A3(220Hz) → F3(174Hz) 느린 하강
            float freq = Mathf.Lerp(220f, 174f, progress);
            // 삼각파로 부드러운 음색
            float phase = (freq * t) % 1f;
            float tone = (4f * Mathf.Abs(phase - 0.5f) - 1f) * 0.5f;

            // 매우 부드러운 엔벨로프
            float env = Mathf.Sin(Mathf.PI * progress) * 0.6f;
            s[i] = tone * env;
        }

        return MakeClip("Approach_Sleepy", s);
    }

    // Scared: 빠른 트레몰로 고음 (B5)
    // Why: 빠른 떨림 + 높은 음 = 불안/긴장
    private static AudioClip GenerateScared()
    {
        float dur = 0.3f;
        int count = (int)(SampleRate * dur);
        float[] s = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / SampleRate;

            // B5(988Hz) + 빠른 트레몰로 (20Hz AM)
            float tone = Mathf.Sin(2f * Mathf.PI * 988f * t) * 0.5f;
            float tremolo = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 20f * t);
            tone *= tremolo;

            // 빠른 어택 + 중간 디케이
            float env = Mathf.Exp(-4f * t) * Mathf.Min(1f, t * 100f);
            s[i] = tone * env;
        }

        return MakeClip("Approach_Scared", s);
    }

    // Serene: 맑은 벨 톤 (G4 + 배음 감쇠)
    // Why: 깨끗한 벨 = 평온/고요함 표현
    private static AudioClip GenerateSerene()
    {
        float dur = 0.5f;
        int count = (int)(SampleRate * dur);
        float[] s = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / SampleRate;

            // G4(392Hz) 기본 + 약한 배음들 (벨 스펙트럼)
            float tone = Mathf.Sin(2f * Mathf.PI * 392f * t) * 0.5f;
            tone += Mathf.Sin(2f * Mathf.PI * 784f * t) * 0.15f * Mathf.Exp(-6f * t);
            tone += Mathf.Sin(2f * Mathf.PI * 1176f * t) * 0.08f * Mathf.Exp(-10f * t);

            // 벨 엔벨로프: 빠른 어택, 긴 여운
            float env = Mathf.Exp(-2.5f * t) * Mathf.Min(1f, t * 120f);
            s[i] = tone * env;
        }

        return MakeClip("Approach_Serene", s);
    }

    private static AudioClip MakeClip(string name, float[] samples)
    {
        var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
