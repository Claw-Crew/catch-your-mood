using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TMPro; // Changed: TMP_Text로 전환. Why: TextMesh(레거시)는 VR 3m 거리에서 SDF 렌더링 미지원으로 흐릿함.

/// <summary>
/// Scoreboard_Main의 TMP_Text를 "결과 모드"로 전환하여
/// GAME OVER -> YOUR MOOD -> 대표 감정 + 통계를 아케이드 엔딩 스타일로 연출.
///
/// SceneSetup.BuildScoreboard()가 생성한 오브젝트 구조에 의존:
///   Scoreboard_Main / Title          (TextMeshPro "Catch your mood!")
///   Scoreboard_Main / TotalTries     (TextMeshPro "Tries: 0")
///   Scoreboard_Main / Slot_Happy..   (6개 감정 배지 슬롯 — ScoreboardUI가 관리)
///   Scoreboard_Main / ResultDetail   (TextMeshPro, 초기 비활성 — 대표 감정 + 메시지)
///   Scoreboard_Main / ResultStats    (TextMeshPro, 초기 비활성 — 성공률 통계)
///
/// 데이터 흐름: GameManager.EndGame() → ResultPanelUI.ShowResult() → 코루틴 연출.
/// 의존성: GameResultManager.cs (GetResults/GetTryCount)
/// 다른 모듈 영향: ScoreboardUI의 배지 슬롯은 그대로 유지됨 (결과의 일부로 활용).
/// </summary>
public class ResultPanelUI : MonoBehaviour
{
    // Changed: TextMesh → TMP_Text. Why: TMP SDF 렌더링으로 VR 원거리 선명도 개선.
    // Find 대신 직렬화된 참조를 사용해 런타임 검색 비용과 오류 가능성을 줄이기 위함.
    [Header("Scoreboard TMP References")]
    [Tooltip("Scoreboard_Main/Title TextMeshPro ('Catch your mood!')")]
    public TMP_Text titleText;

    [Tooltip("Scoreboard_Main/TotalTries TextMeshPro ('Tries: 0')")]
    public TMP_Text totalTriesText;

    [Tooltip("Scoreboard_Main/ResultDetail TextMeshPro (대표 감정 + 메시지, 초기 비활성)")]
    public TMP_Text resultDetailText;

    [Tooltip("Scoreboard_Main/ResultStats TextMeshPro (시도/성공 통계, 초기 비활성)")]
    public TMP_Text resultStatsText;

    // Changed: 감정별 표시 색상을 ScoreboardUI와 동일하게 정의.
    // Why: 배지 색상과 Title 색상이 일치해야 시각적 통일감이 유지됨.
    private static readonly Dictionary<EmotionType, Color> EmotionColors = new()
    {
        { EmotionType.Happy,  HexColor("FFD93D") },  // 밝은 노랑
        { EmotionType.Angry,  HexColor("FF6B6B") },  // 부드러운 빨강
        { EmotionType.Sleepy, HexColor("C3AED6") },  // 연보라
        { EmotionType.Sad,    HexColor("74B9FF") },  // 하늘색
        { EmotionType.Scared, HexColor("A29BFE") },  // 연보라-파랑
        { EmotionType.Serene, HexColor("55EFC4") },  // 민트
    };

    // Changed: 감정별 한 줄 메시지를 정의.
    // Why: 대표 감정에 따라 개인화된 엔딩 메시지를 보여주기 위함.
    private static readonly Dictionary<EmotionType, string> EmotionMessages = new()
    {
        { EmotionType.Happy,  "You chose joy today!" },
        { EmotionType.Angry,  "Passion runs deep in you." },
        { EmotionType.Sleepy, "Rest is what your heart needs." },
        { EmotionType.Sad,    "It's okay to feel blue." },
        { EmotionType.Scared, "Courage is facing fear." },
        { EmotionType.Serene, "Peace lives within you." },
    };

    /// <summary>
    /// GameManager.EndGame()에서 호출. 결과 연출 코루틴을 시작.
    /// </summary>
    public void ShowResult()
    {
        StartCoroutine(ResultSequence());
    }

    /// <summary>
    /// 아케이드 엔딩 스타일 연출 코루틴.
    /// 1) "Catch your mood!" -> "GAME OVER" (페이드인 0.5초, 유지 1.5초)
    /// 2) "GAME OVER" -> "YOUR MOOD" (페이드 0.5초)
    /// 3) TotalTries를 대표 감정 이름 + 비율로 전환
    /// 4) ResultDetail에 대표 감정 메시지 표시
    /// 5) ResultStats에 총 시도/성공/성공률 표시
    /// </summary>
    private IEnumerator ResultSequence()
    {
        // --- 결과 데이터 수집 ---
        var results = GameResultManager.Instance.GetResults();
        int tryCount = GameResultManager.Instance.GetTryCount();
        int totalCaught = 0;
        foreach (var pair in results)
            totalCaught += pair.Value;

        // Changed: 감정 비율 내림차순 정렬.
        // Why: 가장 많이 뽑은 감정이 대표 감정으로 결정됨.
        var sorted = results.OrderByDescending(p => p.Value).ToList();

        // Changed: 대표 감정 결정 (가장 많이 뽑은 감정, 동률 시 첫 번째).
        EmotionType dominant = sorted.Count > 0 ? sorted[0].Key : EmotionType.Happy;
        float dominantPct = (sorted.Count > 0 && totalCaught > 0)
            ? (sorted[0].Value / (float)totalCaught) * 100f
            : 0f;

        // --- Phase 1: "Catch your mood!" -> "GAME OVER" (페이드인 0.5초) ---
        // Changed: TotalTries 텍스트를 즉시 숨기고, Title만 전환.
        // Why: GAME OVER가 단독으로 보여야 아케이드 엔딩 분위기를 살릴 수 있음.
        if (totalTriesText != null)
            totalTriesText.text = "";
        yield return StartCoroutine(FadeTMPText(titleText, 1f, 0f, 0.25f));
        titleText.text = "GAME OVER";
        titleText.color = HexColor("FF6B6B");
        // Changed: TextMesh fontSize+characterSize → TMP fontSize. Why: TMP는 characterSize 없이 fontSize만으로 크기 결정.
        titleText.fontSize = 7;
        yield return StartCoroutine(FadeTMPText(titleText, 0f, 1f, 0.5f));

        // --- Phase 2: GAME OVER 유지 1.5초 ---
        yield return new WaitForSeconds(1.5f);

        // --- Phase 3: "GAME OVER" -> "YOUR MOOD" (페이드 전환 0.5초) ---
        yield return StartCoroutine(FadeTMPText(titleText, 1f, 0f, 0.25f));
        titleText.text = "YOUR MOOD";
        // Changed: TextMesh fontSize+characterSize → TMP fontSize. Why: TMP는 characterSize 없이 fontSize만으로 크기 결정.
        titleText.fontSize = 7;
        // Changed: 대표 감정 색상으로 Title을 표시.
        // Why: 전체 결과의 감정 톤을 Title 색상으로 즉시 전달.
        Color domColor = EmotionColors.ContainsKey(dominant) ? EmotionColors[dominant] : HexColor("FFE8C8");
        titleText.color = domColor;
        yield return StartCoroutine(FadeTMPText(titleText, 0f, 1f, 0.5f));

        // --- Phase 4: TotalTries 텍스트를 대표 감정 이름 + 비율로 전환 ---
        // Changed: 기존 "Tries: N" 텍스트를 대표 감정 강조 텍스트로 교체.
        // Why: 배지 슬롯이 이미 개별 감정 카운트를 보여주므로, 하단에는 대표 감정 요약만 표시.
        if (totalTriesText != null)
        {
            totalTriesText.text = $"{dominant} ({dominantPct:F0}%)";
            // Changed: TextMesh fontSize+characterSize → TMP fontSize. Why: TMP는 characterSize 없이 fontSize만으로 크기 결정.
            totalTriesText.fontSize = 5;
            totalTriesText.color = new Color(domColor.r, domColor.g, domColor.b, 0f);
            yield return StartCoroutine(FadeTMPText(totalTriesText, 0f, 1f, 0.5f));
        }

        // --- Phase 5: ResultDetail에 대표 감정 메시지 표시 ---
        if (resultDetailText != null)
        {
            resultDetailText.gameObject.SetActive(true);
            string message = EmotionMessages.ContainsKey(dominant) ? EmotionMessages[dominant] : "";
            resultDetailText.text = "";
            resultDetailText.color = new Color(1f, 1f, 1f, 0f);

            // Changed: 타이핑 효과로 한 글자씩 표시.
            // Why: 아케이드 엔딩 메시지의 분위기를 살리기 위함.
            StringBuilder sb = new StringBuilder();
            resultDetailText.color = HexColor("F4E9D9");
            for (int i = 0; i < message.Length; i++)
            {
                sb.Append(message[i]);
                resultDetailText.text = sb.ToString();
                yield return new WaitForSeconds(0.04f);
            }
        }

        yield return new WaitForSeconds(0.3f);

        // --- Phase 6: ResultStats에 총 시도/성공/성공률 표시 ---
        if (resultStatsText != null)
        {
            resultStatsText.gameObject.SetActive(true);
            resultStatsText.text = "";
            resultStatsText.color = HexColor("CCCCCC");
            yield return new WaitForSeconds(0.2f);
            // Changed: 시도/성공을 분리 표시.
            // Why: 성공률을 명시하여 게임 플레이 성과를 한눈에 파악.
            float successRate = tryCount > 0 ? (totalCaught / (float)tryCount) * 100f : 0f;
            resultStatsText.text = $"Tries: {tryCount}  |  Caught: {totalCaught}  |  Rate: {successRate:F0}%";
            yield return StartCoroutine(FadeTMPText(resultStatsText, 0f, 1f, 0.5f));
        }
    }

    /// <summary>
    /// Changed: FadeTextMesh → FadeTMPText. Why: TextMesh → TMP_Text 전환에 따른 메서드명/파라미터 타입 변경.
    /// TMP_Text의 알파값을 fromAlpha에서 toAlpha로 duration초 동안 선형 보간.
    /// </summary>
    private IEnumerator FadeTMPText(TMP_Text tmp, float fromAlpha, float toAlpha, float duration)
    {
        Color c = tmp.color;
        float elapsed = 0f;
        c.a = fromAlpha;
        tmp.color = c;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            c.a = Mathf.Lerp(fromAlpha, toAlpha, t);
            tmp.color = c;
            yield return null;
        }

        c.a = toAlpha;
        tmp.color = c;
    }

    /// <summary>
    /// 헥스 문자열을 Color로 변환하는 유틸.
    /// </summary>
    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out var c);
        return c;
    }
}
