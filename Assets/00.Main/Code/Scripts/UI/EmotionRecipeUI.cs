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
/// [한국어 폰트 처리]
/// Changed: SceneSetup.Build Main Scene에서 NotoSansKR 기반 TMP Font Asset을 생성/로드한 뒤
///          결과 Canvas의 TMP_Text와 koreanFontAsset 필드에 직접 할당한다.
/// Why: 결과지는 영어 fallback 없이 한국어를 기본 출력하고, 폰트 누락은 Warning으로 노출해야 한다.
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

    // Changed: 결과지 전용 NotoSansKR TMP FontAsset 참조를 직렬화 필드로 추가.
    // Why: SceneSetup.Build Main Scene에서 할당한 폰트 참조가 Quest 빌드 씬에도 남아야 한글 글리프를 렌더링할 수 있음.
    [Header("한국어 폰트")]
    public TMP_FontAsset koreanFontAsset;

    // Changed: 결과지가 HMD 정면으로 따라오지 않고 씬의 고정 월드 앵커를 우선 사용하도록 참조를 추가.
    // Why: 결과 카드가 현재 시야 앞의 기계/인형/소품과 겹치지 않고 안정적인 위치에서 읽히게 하기 위함.
    [Header("결과지 월드 배치")]
    public Transform resultCardAnchor;
    private const string ResultCardAnchorName = "ResultCardAnchor";
    private static readonly Vector3 FallbackResultCardPosition = new(1.08f, 1.45f, -0.46f);
    private static readonly Vector3 FallbackResultCardViewerPosition = new(0f, 1.45f, -1.05f);

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

    // Changed: 결과지 영어 fallback 데이터를 활성 코드 경로에서 제거.
    // Why: 한국어 결과 텍스트는 폰트 상태와 무관하게 한국어 기본 출력을 유지해야 함.
    private static readonly Dictionary<EmotionType, string> Messages = new()
    {
        { EmotionType.Happy,  "오늘 당신의 하루는 반짝반짝 빛나고 있어요!" },
        { EmotionType.Sad,    "가끔은 눈물도 좋은 양념이 되어요. 괜찮아요." },
        { EmotionType.Angry,  "뜨거운 에너지가 가득한 하루! 그 열정을 응원해요." },
        { EmotionType.Sleepy, "포근한 꿈에 빠질 시간. 오늘도 수고했어요." },
        { EmotionType.Scared, "용기는 두려움을 넘는 거예요. 당신은 이미 충분히 용감해요." },
        { EmotionType.Serene, "평온한 당신의 하루가 주변을 따뜻하게 해요." }
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

    public void AssignKoreanFontAsset(TMP_FontAsset fontAsset)
    {
        // Changed: SceneSetup이 만든 TMP_FontAsset을 EmotionRecipeUI와 모든 결과 TMP_Text에 동시에 적용.
        // Why: 씬 생성 시점의 직렬화 참조와 런타임 텍스트 컴포넌트의 실제 font 참조를 일치시키기 위함.
        koreanFontAsset = fontAsset;
        ApplyKoreanFontAsset();
    }

    private bool ApplyKoreanFontAsset()
    {
        // Changed: 결과지 텍스트 컴포넌트에 NotoSansKR TMP_FontAsset을 직접 할당.
        // Why: TMP Settings fallback에 의존하지 않고 한국어 결과 텍스트가 기본 폰트 경로에서 렌더링되도록 하기 위함.
        if (koreanFontAsset == null)
        {
            Debug.LogWarning("[CatchYourMood] EmotionRecipeUI.koreanFontAsset이 비어 있습니다. Build Main Scene에서 Assets/Fonts/NotoSansKR-Regular.ttf 기반 TMP Font Asset을 생성/할당해야 합니다. 한국어 문구는 유지되지만 글리프가 □로 보일 수 있습니다.", this);
            return false;
        }

        AssignFont(titleText);
        AssignFont(recipeNameText);
        AssignFont(ingredientsText);
        AssignFont(messageText);
        AssignFont(countText);

        bool canRenderKorean = koreanFontAsset.HasCharacter('한', false, true);
        if (!canRenderKorean)
        {
            Debug.LogWarning($"[CatchYourMood] 할당된 TMP Font Asset이 한국어 글리프를 추가하지 못했습니다. Asset: {koreanFontAsset.name}. 한국어 문구는 유지되지만 글리프가 □로 보일 수 있습니다.", this);
        }

        return canRenderKorean;
    }

    private void AssignFont(TMP_Text textComponent)
    {
        // Changed: null이 아닌 결과 TMP_Text에만 한국어 폰트를 할당.
        // Why: SceneSetup 또는 수동 씬 편집 중 일부 텍스트 참조가 비어 있어도 나머지 텍스트는 정상 처리해야 함.
        if (textComponent != null)
            textComponent.font = koreanFontAsset;
    }

    private void PrepareKoreanFontForResult(params string[] resultTexts)
    {
        // Changed: 실제 표시할 결과 문자열 전체에 대해 글리프 추가 가능 여부를 사전 확인.
        // Why: 한글을 영어 fallback으로 바꾸지 않고, 누락 글리프를 명확한 Warning으로 드러내기 위함.
        if (!ApplyKoreanFontAsset() || koreanFontAsset == null) return;

        var combined = new StringBuilder();
        foreach (string resultText in resultTexts)
        {
            if (!string.IsNullOrEmpty(resultText))
                combined.Append(resultText);
        }

        if (combined.Length == 0) return;

        bool hasCharacters = koreanFontAsset.HasCharacters(combined.ToString(), out uint[] missingCharacters, false, true);
        if (!hasCharacters)
        {
            Debug.LogWarning($"[CatchYourMood] 한국어 결과지 글리프 일부가 TMP Font Asset에 없습니다. Missing: {FormatMissingCharacters(missingCharacters)} / Asset: {koreanFontAsset.name}", this);
        }
    }

    private static string FormatMissingCharacters(uint[] missingCharacters)
    {
        // Changed: 누락 글리프 목록을 Warning에 넣을 짧은 문자열로 변환.
        // Why: 폰트 에셋 생성/동적 추가 실패 시 어떤 문자가 문제인지 바로 확인하기 위함.
        if (missingCharacters == null || missingCharacters.Length == 0)
            return "(none)";

        const int maxCharacters = 24;
        var builder = new StringBuilder();
        int count = missingCharacters.Length < maxCharacters ? missingCharacters.Length : maxCharacters;
        for (int i = 0; i < count; i++)
        {
            uint unicode = missingCharacters[i];
            if (unicode <= char.MaxValue)
                builder.Append((char)unicode);
            else
                builder.Append("U+").Append(unicode.ToString("X"));
        }

        if (missingCharacters.Length > maxCharacters)
            builder.Append($" (+{missingCharacters.Length - maxCharacters} more)");

        return builder.ToString();
    }

    /// <summary>
    /// TMP Font Asset을 결과 TMP_Text에 적용하고 Warning 경로를 실행.
    /// Changed: 반환값은 언어 선택에 쓰지 않고 항상 true로 유지.
    /// Why: 폰트 상태가 나빠도 결과 텍스트는 영어로 전환하지 않아야 함.
    /// </summary>
    private bool HasKoreanFontSupport()
    {
        // Changed: 이 함수는 영어 fallback 선택이 아니라 한국어 폰트 적용/Warning 경로만 담당.
        // Why: 결과 텍스트는 항상 한국어를 사용하고, 폰트 문제는 숨기지 않고 로그로 노출해야 함.
        ApplyKoreanFontAsset();
        return true;
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

        // Changed: 폰트 상태와 무관하게 결과지는 항상 한국어 문구를 생성.
        // Why: 한글 깨짐을 영어 fallback으로 숨기지 않고 TMP Font Asset 경로의 문제로 드러내기 위함.
        HasKoreanFontSupport();

        // 레시피 이름
        // Changed: 레시피 이름 선택에서 영어 fallback 분기를 제거.
        // Why: 한국어 결과지는 폰트가 없더라도 한국어를 기본 출력해야 함.
        string recipeName = RecipeNames.ContainsKey(dominant) ? RecipeNames[dominant] : "오늘의 특별한 음료";

        // 재료 목록 생성: "기쁨 3스푼 + 슬픔 한 꼬집 + ..."
        // Changed: 재료 목록 생성에서 영어 fallback 분기를 제거.
        // Why: 결과지의 감정명/단위는 항상 한국어로 표시되어야 함.
        string ingredients;
        if (sorted.Count == 0)
        {
            ingredients = "아직 재료가 없어요...";
        }
        else
        {
            var parts = new List<string>();
            foreach (var kv in sorted)
            {
                string emotionName = EmotionKoreanNames.ContainsKey(kv.Key) ? EmotionKoreanNames[kv.Key] : kv.Key.ToString();
                parts.Add($"{emotionName} {GetAmountUnit(kv.Value)}");
            }
            ingredients = string.Join(" + ", parts);
        }

        // 한 줄 메시지
        // Changed: 메시지 선택에서 영어 fallback 분기를 제거.
        // Why: 한국어 결과지는 폰트가 없더라도 한국어를 기본 출력해야 함.
        string message = Messages.ContainsKey(dominant) ? Messages[dominant] : "당신의 감정은 소중해요.";

        // 총 뽑기 횟수
        int totalCaught = sorted.Sum(kv => kv.Value);
        // Changed: 카운트 문구를 코루틴 밖에서 한국어로 확정하고 글리프 검증에 재사용.
        // Why: 실제 표시 문자열과 TMP FontAsset 사전 확인 대상이 일치해야 함.
        string countLine = $"시도 {tryCount}회 / 성공 {totalCaught}마리";
        PrepareKoreanFontForResult("오늘의 감정 레시피", recipeName, ingredients, message, countLine);

        // 연출 코루틴 시작
        StartCoroutine(ShowResultCoroutine(recipeName, ingredients, message, countLine));
    }

    private Transform ResolveResultCardAnchor()
    {
        // Changed: SceneSetup이 직렬화한 앵커가 없으면 이름 기반으로 한 번 더 찾는다.
        // Why: 기존 씬을 재생성하지 않아도 사용자가 직접 추가한 ResultCardAnchor를 런타임에서 활용하기 위함.
        if (resultCardAnchor != null)
            return resultCardAnchor;

        GameObject anchorGo = GameObject.Find(ResultCardAnchorName);
        if (anchorGo == null)
            return null;

        resultCardAnchor = anchorGo.transform;
        return resultCardAnchor;
    }

    private void ApplyResultCardPlacement()
    {
        // Changed: 카메라 현재 시선 기준 배치를 제거하고 고정 월드 앵커/fallback 포즈를 적용.
        // Why: HMD 방향에 따라 결과지가 주변 오브젝트 위로 뜨는 문제를 막고 매번 같은 읽기 위치를 보장하기 위함.
        Transform anchor = ResolveResultCardAnchor();
        if (anchor != null)
        {
            transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            return;
        }

        transform.SetPositionAndRotation(
            FallbackResultCardPosition,
            GetUprightLookRotation(FallbackResultCardPosition, FallbackResultCardViewerPosition));
        Debug.LogWarning("[CatchYourMood] ResultCardAnchor가 없어 고정 fallback 위치에 결과지를 배치합니다. Build Main Scene을 다시 실행하면 앵커가 생성/할당됩니다.", this);
    }

    private static Quaternion GetUprightLookRotation(Vector3 cardPosition, Vector3 viewerPosition)
    {
        // Changed: 결과 카드가 수직으로 선 상태에서 기준 플레이어 위치를 향하도록 yaw 회전만 계산.
        // Why: HMD pitch/roll을 따라가지 않아 카드가 기울거나 가까운 물체와 겹쳐 보이는 일을 피하기 위함.
        Vector3 forward = cardPosition - viewerPosition;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    /// <summary>
    /// 결과 표시 연출 코루틴.
    /// 1) 대기 → 2) Canvas 페이드인 → 3) 텍스트 타이핑 효과
    /// </summary>
    // Changed: countLine을 이미 확정된 한국어 문자열로 전달.
    // Why: 코루틴 내부에서 폰트 상태에 따른 영어 fallback 분기가 다시 생기지 않게 하기 위함.
    private IEnumerator ShowResultCoroutine(string recipeName, string ingredients, string message, string countLine)
    {
        // Changed: Canvas를 활성화하되 알파 0에서 시작하여 서서히 나타나게 함.
        // Why: 타이머 종료 → 갑작스러운 UI 출현 대신 부드러운 전환 연출을 위함.

        // Changed: 결과 Canvas를 카메라 정면이 아니라 ResultCardAnchor/fallback 고정 월드 포즈에 배치.
        // Why: 결과지가 시야 앞 오브젝트와 겹치지 않고 항상 같은 위치에서 읽히게 하기 위함.
        ApplyResultCardPlacement();

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
        // Changed: 타이틀 텍스트를 항상 한국어로 표시.
        // Why: 결과지는 폰트 상태와 무관하게 한국어 기본 출력이어야 함.
        string titleStr = "오늘의 감정 레시피";
        yield return TypeText(titleText, titleStr);
        yield return new WaitForSeconds(0.3f);

        yield return TypeText(recipeNameText, recipeName);
        yield return new WaitForSeconds(0.3f);

        yield return TypeText(ingredientsText, ingredients);
        yield return new WaitForSeconds(0.3f);

        yield return TypeText(messageText, message);
        yield return new WaitForSeconds(0.2f);

        // 뽑기 횟수는 타이핑 없이 즉시 표시
        // Changed: 카운트 텍스트를 항상 한국어로 표시.
        // Why: 결과지는 폰트 상태와 무관하게 한국어 기본 출력이어야 함.
        if (countText != null)
        {
            countText.text = countLine;
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
