using System;
using System.Collections.Generic;
using UnityEngine;

public class GameResultManager : MonoBehaviour
{
    public static GameResultManager Instance;

    private Dictionary<EmotionType, int> emotionCounts = new Dictionary<EmotionType, int>();

    private int tryCount = 0;

    // Changed: 감정 뽑기 이벤트 추가. ScoreboardUI가 구독하여 실시간 배지 업데이트에 사용.
    // Why: RegisterCatch() 호출 시 Scoreboard를 즉시 갱신하기 위해 Observer 패턴 도입.
    public event Action<EmotionType> OnCatch;

    // Changed: 시도 횟수 이벤트 추가. ScoreboardUI가 총 시도 횟수를 표시하기 위해 사용.
    public event Action<int> OnTry;

    void Awake()
    {
        Instance = this;
    }

    public void RegisterCatch(EmotionType emotion)
    {
        if (!emotionCounts.ContainsKey(emotion))
            emotionCounts[emotion] = 0;

        emotionCounts[emotion]++;

        // Changed: 뽑기 성공 시 이벤트 발행.
        OnCatch?.Invoke(emotion);
    }

    public void RegisterTry()
    {
        tryCount++;

        // Changed: 시도 시 이벤트 발행.
        OnTry?.Invoke(tryCount);
    }

    public Dictionary<EmotionType, int> GetResults()
    {
        return emotionCounts;
    }

    public int GetTryCount()
    {
        return tryCount;
    }

    // Changed: 특정 감정의 현재 카운트를 반환하는 헬퍼 추가.
    // Why: ScoreboardUI가 개별 슬롯 업데이트 시 해당 감정 카운트만 필요.
    public int GetEmotionCount(EmotionType emotion)
    {
        return emotionCounts.ContainsKey(emotion) ? emotionCounts[emotion] : 0;
    }
}