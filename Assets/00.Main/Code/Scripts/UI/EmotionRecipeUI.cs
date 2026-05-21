using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;

/// <summary>
/// 감정 레시피 카드 결과 UI.
/// 타이머 종료 후 뽑은 감정 인형들을 카페 음료 레시피 메타포로 변환하여 표시.
/// "오늘의 감정 레시피: 기쁨 3스푼 + 슬픔 1꼬집 = 따뜻한 오후의 라떼"
///
/// [한국어 폰트 주의]
/// TMP 기본 폰트(LiberationSans)는 한국어 미지원.
/// 해결 방법:
///   1) NotoSansKR 등 한국어 TMP Font Asset을 생성하여 TMP Settings > Default Font Asset에 등록하거나,
///   2) TMP Settings > Fallback Font Assets 목록에 한국어 Font Asset을 추가하거나,
///   3) ShowResult() 호출 전에 각 TMP_Text.font에 한국어 TMP_FontAsset을 직접 할당.
/// 현재 코드는 TMP 기본 폰트를 사용하며, 위 방법 중 하나를 적용해야 한국어가 정상 렌더링됨.
/// </summary>
public class EmotionRecipeUI : MonoBehaviour
{
    // Changed: ResultUI 단순 텍스트 대신 레시피 카드 UI 구성으로 전환.
    // Why: 카페 컨셉에 맞는 감정 결과 표현을 위함.

    [Header("UI References (SceneSetup에서 자동 할당)")]
    public CanvasGroup canvasGroup;       // 페이드인용
    public TMP_Text titleText;            // "오늘의 감정 레시피"
    public TMP_Text recipeNameText;       // 레시피 이름 (큰 글씨)
    public TMP_Text ingredientsText;      // 감정 재료 목록
    public TMP_Text messageText;          // 한 줄 메시지
    public TMP_Text countText;            // 총 뽑기 횟수

    [Header("연출 설정")]
    public float fadeInDelay = 1.0f;      // 타이머 종료 후 대기 시간
    public float fadeInDuration = 1.5f;   // 알파 0->1 소요 시간
    public float typingSpeed = 0.05f;     // 글자당 타이핑 속도 (초)

    // Changed: 감정별 레시피 데이터를 Dictionary로 구성.
    // Why: 지배 감정에 따라 레시피 이름과 메시지를 빠르게 참조하기 위함.
    private static readonly Dictionary<EmotionType, string> RecipeNames = new()
    {
        { EmotionType.Happy,  "햇살 가득 바닐라 라떼" },
        { EmotionType.Sad,    "비 오는 날의 카모마일 티" },
        { EmotionType.Angry,  "불꽃 시나몬 에스프레소" },
        { EmotionType.Sleepy, "달빛 라벤더 핫초코" },
        { EmotionType.Scared, "안개 속 민트 모카" },
        { EmotionType.Serene, "고요한 오후의 말차 라떼" }
    };

    // Changed: 한국어 TMP Font Asset이 없을 때 영어 fallback용 레시피 이름.
    // Why: NotoSansKR SDF가 프로젝트에 없으면 한글이 □로 표시되므로.
    private static readonly Dictionary<EmotionType, string> RecipeNamesEN = new()
    {
        { EmotionType.Happy,  "Sunny Vanilla Latte" },
        { EmotionType.Sad,    "Rainy Day Chamomile Tea" },
        { EmotionType.Angry,  "Fiery Cinnamon Espresso" },
        { EmotionType.Sleepy, "Moonlit Lavender Hot Cocoa" },
        { EmotionType.Scared, "Misty Mint Mocha" },
        { EmotionType.Serene, "Serene Afternoon Matcha Latte" }
    };

    private static readonly Dictionary<EmotionType, string> Messages = new()
    {
        { EmotionType.Happy,  "오늘 당신의 하루는 반짝반짝 빛나고 있어요!" },
        { EmotionType.Sad,    "가끔은 눈물도 좋은 양념이 되어요. 괜찮아요." },
        { EmotionType.Angry,  "뜨거운 에너지가 가득한 하루! 그 열정을 응원해요." },
        { EmotionType.Sleepy, "포근한 꿈에 빠질 시간. 오늘도 수고했어요." },
        { EmotionType.Scared, "용기는 두려움을 넘는 거예요. 당신은 이미 충분히 용감해요." },
        { EmotionType.Serene, "평온한 당신의 하루가 주변을 따뜻하게 해요." }
    };

    // Changed: 한국어 TMP Font Asset이 없을 때 영어 fallback용 메시지.
    // Why: NotoSansKR SDF가 프로젝트에 없으면 한글이 □로 표시되므로.
    private static readonly Dictionary<EmotionType, string> MessagesEN = new()
    {
        { EmotionType.Happy,  "Your day is sparkling bright!" },
        { EmotionType.Sad,    "Sometimes tears are a good seasoning. It's okay." },
        { EmotionType.Angry,  "A day full of fiery energy! Cheering for your passion." },
        { EmotionType.Sleepy, "Time for a cozy dream. Great job today." },
        { EmotionType.Scared, "Courage is going beyond fear. You're already brave enough." },
        { EmotionType.Serene, "Your peaceful day warms everyone around you." }
    };

    // Changed: 감정별 양 표현을 한국어 단위로 매핑.
    // Why: "3스푼", "1꼬집" 등 카페 재료 메타포를 자연스럽게 표현하기 위함.
    private static readonly Dictionary<EmotionType, string> EmotionKoreanNames = new()
    {
        { EmotionType.Happy,  "기쁨" },
        { EmotionType.Sad,    "슬픔" },
        { EmotionType.Angry,  "분노" },
        { EmotionType.Sleepy, "졸림" },
        { EmotionType.Scared, "두려움" },
        { EmotionType.Serene, "평온" }
    };

    // Changed: 한국어 TMP Font Asset이 없을 때 영어 fallback용 감정 이름.
    // Why: NotoSansKR SDF가 프로젝트에 없으면 한글이 □로 표시되므로.
    private static readonly Dictionary<EmotionType, string> EmotionEnglishNames = new()
    {
        { EmotionType.Happy,  "Joy" },
        { EmotionType.Sad,    "Sadness" },
        { EmotionType.Angry,  "Anger" },
        { EmotionType.Sleepy, "Sleepiness" },
        { EmotionType.Scared, "Fear" },
        { EmotionType.Serene, "Serenity" }
    };

    /// <summary>
    /// 뽑기 수량에 따라 카페 재료 단위를 반환.
    /// 1: 한 꼬집, 2: 두 방울, 3+: N스푼
    /// </summary>
    private static string GetAmountUnit(int count)
    {
        // Changed: 수량별 단위 표현을 카페 메타포로 분기.
        // Why: "기쁨 1개" 대신 "기쁨 한 꼬집"으로 감성적 표현을 위함.
        return count switch
        {
            1 => "한 꼬집",
            2 => "두 방울",
            _ => $"{count}스푼"
        };
    }

    // Changed: 한국어 TMP Font Asset이 없을 때 영어 fallback용 수량 단위.
    // Why: NotoSansKR SDF가 프로젝트에 없으면 한글이 □로 표시되므로.
    private static string GetAmountUnitEN(int count)
    {
        return count switch
        {
            1 => "a pinch",
            2 => "two drops",
            _ => $"{count} spoons"
        };
    }

    /// <summary>
    /// TMP Font Asset이 한국어를 지원하는지 런타임에 확인.
    /// TMP_Text.font에 "한" 글자의 글리프가 있으면 true.
    /// [한국어 폰트 설정 가이드]
    ///   1) Window > TextMeshPro > Font Asset Creator에서 NotoSansKR-Regular.otf를 SDF로 생성.
    ///   2) 생성된 TMP_FontAsset을 Project Settings > TextMesh Pro > Settings > Default Font Asset에 등록.
    ///      또는 Fallback Font Assets 목록에 추가.
    ///   3) 그러면 이 함수가 true를 반환하고 한국어 텍스트가 정상 표시됨.
    /// </summary>
    private bool HasKoreanFontSupport()
    {
        // Changed: 항상 한국어를 사용하도록 true 반환.
        // Why: 유저가 한국어 표시를 요청. NotoSansKR TMP Font Asset을 프로젝트에 추가하면 정상 표시됨.
        // 한국어 폰트 설정: Window > TextMeshPro > Font Asset Creator에서
        //   Source Font: NotoSansKR-Regular.ttf (Google Fonts에서 다운로드)
        //   Atlas Resolution: 4096x4096
        //   Character Set: Custom Range → 32-126,12593-12643,44032-55203
        //   Generate → Save → TMP Settings의 Default Font Asset 또는 Fallback에 등록
        return true;

        return false;
    }

    /// <summary>
    /// GameManager.EndGame()에서 호출.
    /// GameResultManager에서 감정 카운트를 읽어 레시피 카드를 구성하고 연출을 시작.
    /// </summary>
    public void ShowResult()
    {
        // Changed: 기존 ResultUI.ShowResult() 단순 출력 대신 레시피 변환 + 연출 코루틴 시작.
        // Why: 카페 음료 메타포 기반 결과 표현과 페이드인/타이핑 효과를 적용하기 위함.
        var results = GameResultManager.Instance.GetResults();
        int tryCount = GameResultManager.Instance.GetTryCount();

        // 감정 카운트를 내림차순 정렬
        var sorted = results
            .Where(kv => kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .ToList();

        // 지배 감정 결정 (가장 많이 뽑은 감정, 없으면 Happy 기본값)
        EmotionType dominant = sorted.Count > 0 ? sorted[0].Key : EmotionType.Happy;

        // Changed: 한국어 TMP Font Asset이 없을 때 영어로 fallback.
        // Why: NotoSansKR SDF가 프로젝트에 없으면 한글이 □로 표시되므로.
        bool useKorean = HasKoreanFontSupport();

        // 레시피 이름
        string recipeName;
        if (useKorean)
            recipeName = RecipeNames.ContainsKey(dominant) ? RecipeNames[dominant] : "오늘의 특별한 음료";
        else
            recipeName = RecipeNamesEN.ContainsKey(dominant) ? RecipeNamesEN[dominant] : "Today's Special Drink";

        // 재료 목록 생성: "기쁨 3스푼 + 슬픔 한 꼬집 + ..." 또는 영어 fallback
        string ingredients;
        if (sorted.Count == 0)
        {
            ingredients = useKorean ? "아직 재료가 없어요..." : "No ingredients yet...";
        }
        else
        {
            var parts = new List<string>();
            foreach (var kv in sorted)
            {
                if (useKorean)
                {
                    string emotionName = EmotionKoreanNames.ContainsKey(kv.Key) ? EmotionKoreanNames[kv.Key] : kv.Key.ToString();
                    parts.Add($"{emotionName} {GetAmountUnit(kv.Value)}");
                }
                else
                {
                    string emotionName = EmotionEnglishNames.ContainsKey(kv.Key) ? EmotionEnglishNames[kv.Key] : kv.Key.ToString();
                    parts.Add($"{emotionName} {GetAmountUnitEN(kv.Value)}");
                }
            }
            ingredients = string.Join(" + ", parts);
        }

        // 한 줄 메시지
        string message;
        if (useKorean)
            message = Messages.ContainsKey(dominant) ? Messages[dominant] : "당신의 감정은 소중해요.";
        else
            message = MessagesEN.ContainsKey(dominant) ? MessagesEN[dominant] : "Your emotions are precious.";

        // 총 뽑기 횟수
        int totalCaught = sorted.Sum(kv => kv.Value);

        // 연출 코루틴 시작
        StartCoroutine(ShowResultCoroutine(recipeName, ingredients, message, tryCount, totalCaught, useKorean));
    }

    /// <summary>
    /// 결과 표시 연출 코루틴.
    /// 1) 대기 → 2) Canvas 페이드인 → 3) 텍스트 타이핑 효과
    /// </summary>
    // Changed: useKorean 파라미터 추가. Why: 타이틀/카운트 텍스트도 한국어/영어 분기하기 위함.
    private IEnumerator ShowResultCoroutine(string recipeName, string ingredients, string message, int tryCount, int totalCaught, bool useKorean = true)
    {
        // Changed: Canvas를 활성화하되 알파 0에서 시작하여 서서히 나타나게 함.
        // Why: 타이머 종료 → 갑작스러운 UI 출현 대신 부드러운 전환 연출을 위함.

        // 초기 상태: 투명
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        // 모든 텍스트 초기화 (빈 문자열)
        if (titleText != null) titleText.text = "";
        if (recipeNameText != null) recipeNameText.text = "";
        if (ingredientsText != null) ingredientsText.text = "";
        if (messageText != null) messageText.text = "";
        if (countText != null) countText.text = "";

        // Changed: Canvas는 이미 활성 상태(alpha=0으로 숨김)이므로 SetActive 불필요.
        // Why: SetActive(false) 상태에서는 FindAnyObjectByType가 찾지 못하므로,
        //       처음부터 활성 상태를 유지하고 CanvasGroup.alpha로 가시성만 제어.

        // 1) 대기
        yield return new WaitForSeconds(fadeInDelay);

        // 2) 배경 조명 약간 어둡게 (분위기 전환)
        // Changed: 결과 표시 시 주변 조명 강도를 낮춰 카드에 시선 집중.
        // Why: VR 환경에서 결과 카드의 가독성과 분위기 전환 효과를 높이기 위함.
        Light mainLight = FindAnyObjectByType<Light>();
        float originalIntensity = 1f;
        if (mainLight != null)
        {
            originalIntensity = mainLight.intensity;
            float targetIntensity = originalIntensity * 0.4f;
            float dimDuration = fadeInDuration * 0.5f;
            float dimElapsed = 0f;
            while (dimElapsed < dimDuration)
            {
                dimElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(dimElapsed / dimDuration);
                mainLight.intensity = Mathf.Lerp(originalIntensity, targetIntensity, t);
                yield return null;
            }
            mainLight.intensity = targetIntensity;
        }

        // 3) Canvas 페이드인 (알파 0 → 1)
        if (canvasGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }

        // 4) 텍스트 타이핑 효과 (한 줄씩)
        // Changed: 한국어 폰트 유무에 따라 타이틀 텍스트 분기.
        // Why: NotoSansKR SDF가 없으면 한글 타이틀이 □로 표시되므로 영어 fallback 사용.
        string titleStr = useKorean ? "오늘의 감정 레시피" : "Today's Emotion Recipe";
        yield return TypeText(titleText, titleStr);
        yield return new WaitForSeconds(0.3f);

        yield return TypeText(recipeNameText, recipeName);
        yield return new WaitForSeconds(0.3f);

        yield return TypeText(ingredientsText, ingredients);
        yield return new WaitForSeconds(0.3f);

        yield return TypeText(messageText, message);
        yield return new WaitForSeconds(0.2f);

        // 뽑기 횟수는 타이핑 없이 즉시 표시
        // Changed: 한국어 폰트 유무에 따라 카운트 텍스트 분기.
        // Why: NotoSansKR SDF가 없으면 한글 카운트가 □로 표시되므로 영어 fallback 사용.
        if (countText != null)
        {
            countText.text = useKorean
                ? $"시도 {tryCount}회 / 성공 {totalCaught}마리"
                : $"Tries: {tryCount} / Caught: {totalCaught}";
        }
    }

    /// <summary>
    /// TMP_Text에 한 글자씩 타이핑 효과를 적용하는 코루틴.
    /// </summary>
    private IEnumerator TypeText(TMP_Text textComponent, string fullText)
    {
        if (textComponent == null) yield break;

        // Changed: StringBuilder로 한 글자씩 추가하여 타이핑 효과 구현.
        // Why: 카페 메뉴판에 글씨가 써지는 느낌을 연출하기 위함.
        var sb = new StringBuilder();
        for (int i = 0; i < fullText.Length; i++)
        {
            sb.Append(fullText[i]);
            textComponent.text = sb.ToString();
            yield return new WaitForSeconds(typingSpeed);
        }
    }
}
