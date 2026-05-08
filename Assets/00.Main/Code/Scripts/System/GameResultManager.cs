using System.Collections.Generic;
using UnityEngine;

public class GameResultManager : MonoBehaviour
{
    public static GameResultManager Instance;

    private Dictionary<EmotionType, int> emotionCounts = new Dictionary<EmotionType, int>();
    
    private int tryCount = 0;

    void Awake()
    {
        Instance = this;
    }

    public void RegisterCatch(EmotionType emotion)
    {
        if (!emotionCounts.ContainsKey(emotion))
            emotionCounts[emotion] = 0;

        emotionCounts[emotion]++;
    }

    public void RegisterTry()
    {
        tryCount++;
    }

    public Dictionary<EmotionType, int> GetResults()
    {
        return emotionCounts;
    }

    public int GetTryCount()
    {
        return tryCount;
    }
}