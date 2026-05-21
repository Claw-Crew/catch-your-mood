using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // Changed: TMP_Text로 전환. Why: TextMesh(레거시)는 VR 3m 거리에서 SDF 렌더링 미지원으로 흐릿함.

/// <summary>
/// Scoreboard 배지 시스템 — VRChat 스타일 "실루엣→풀컬러" 슬롯.
/// SceneSetup.BuildScoreboard()에서 에디터 타임에 AddComponent되며,
/// 런타임에 GameResultManager.OnCatch 이벤트를 구독하여 배지를 업데이트한다.
/// </summary>
public class ScoreboardUI : MonoBehaviour
{
    // ==================== 슬롯 참조 (SceneSetup에서 할당) ====================
    // Changed: SceneSetup이 에디터 타임에 각 감정 슬롯 GameObject와 TMP_Text를 직접 할당.
    // Why: 런타임 Find 대신 직접 참조로 안정성 확보.

    [Header("Emotion Slots (set by SceneSetup)")]
    public GameObject[] slotRoots = new GameObject[6]; // Happy, Angry, Sleepy, Sad, Scared, Serene 순서

    [Header("Total Tries Text (set by SceneSetup)")]
    // Changed: TextMesh → TMP_Text. Why: TMP SDF 렌더링으로 VR 원거리 선명도 개선.
    public TMP_Text totalTriesText;

    // Changed: 인형 텍스처 배열 추가 — SceneSetup이 에디터 타임에 감정별 인형 텍스처를 할당.
    // Why: 배지를 Sphere 대신 Quad + 인형 텍스처로 표시하여 디자인 정합성 개선.
    [Header("Doll Textures (set by SceneSetup, indexed by EmotionOrder)")]
    public Texture2D[] dollTextures = new Texture2D[6];

    // ==================== 감정별 색상 ====================
    // Changed: 디자인 문서에 정의된 감정별 파스텔 색상을 상수로 정의.
    private static readonly Dictionary<EmotionType, Color> EmotionColors = new Dictionary<EmotionType, Color>
    {
        { EmotionType.Happy,  HexColor("FFD93D") },  // 밝은 노랑
        { EmotionType.Angry,  HexColor("FF6B6B") },  // 부드러운 빨강
        { EmotionType.Sleepy, HexColor("C3AED6") },  // 연보라
        { EmotionType.Sad,    HexColor("74B9FF") },  // 하늘색
        { EmotionType.Scared, HexColor("A29BFE") },  // 연보라-파랑
        { EmotionType.Serene, HexColor("55EFC4") },  // 민트
    };

    // ==================== 감정 순서 (SceneSetup 슬롯 배열 인덱스와 동일) ====================
    private static readonly EmotionType[] EmotionOrder =
    {
        EmotionType.Happy, EmotionType.Angry, EmotionType.Sleepy,
        EmotionType.Sad,   EmotionType.Scared, EmotionType.Serene
    };

    // ==================== 내부 상태 ====================
    // Changed: 각 슬롯의 런타임 배지/텍스트 참조를 캐시.
    // Why: 매 업데이트마다 Find 호출을 피하기 위함.
    private GameObject[] badges = new GameObject[6];
    // Changed: TextMesh[] → TMP_Text[]. Why: 모든 텍스트를 TMP로 통일하여 VR SDF 렌더링 적용.
    private TMP_Text[] countTexts = new TMP_Text[6];
    private TMP_Text[] nameTexts = new TMP_Text[6];
    private TMP_Text[] placeholderTexts = new TMP_Text[6];
    private Material[] badgeMaterials = new Material[6];

    // ==================== 배지 스케일 파라미터 ====================
    private const float BadgeBaseSize = 0.12f;       // 배지 기본 직경
    private const float BadgeGrowthPerCount = 0.008f; // 카운트당 추가 크기
    private const float BadgeMaxSize = 0.18f;         // 배지 최대 직경
    private const float AnimDuration = 0.3f;          // 스케일 애니메이션 시간(초)

    // ==================== 초기화 ====================
    void Start()
    {
        // Changed: 슬롯 내부의 자식 TMP_Text를 캐시하고, GameResultManager 이벤트 구독.
        // Why: 런타임 성능 최적화 및 이벤트 기반 업데이트 구조.
        CacheSlotReferences();
        SubscribeEvents();
    }

    void OnDestroy()
    {
        // Changed: 이벤트 구독 해제로 메모리 누수 방지.
        UnsubscribeEvents();
        // Changed: 런타임 생성 머티리얼 정리.
        for (int i = 0; i < badgeMaterials.Length; i++)
        {
            if (badgeMaterials[i] != null)
                Destroy(badgeMaterials[i]);
        }
    }

    // ==================== 슬롯 내부 참조 캐시 ====================
    private void CacheSlotReferences()
    {
        for (int i = 0; i < 6; i++)
        {
            if (slotRoots[i] == null) continue;

            Transform slot = slotRoots[i].transform;

            // SceneSetup이 생성하는 자식 구조:
            //   "EmotionName" — 감정 이름 TMP_Text (3D World)
            //   "Placeholder" — "?" 표시 TMP_Text (비어있을 때)
            Transform nameTf = slot.Find("EmotionName");
            if (nameTf != null)
                // Changed: GetComponent<TextMesh>() → GetComponent<TMP_Text>(). Why: SceneSetup이 TextMeshPro로 생성.
                nameTexts[i] = nameTf.GetComponent<TMP_Text>();

            Transform phTf = slot.Find("Placeholder");
            if (phTf != null)
                // Changed: GetComponent<TextMesh>() → GetComponent<TMP_Text>(). Why: SceneSetup이 TextMeshPro로 생성.
                placeholderTexts[i] = phTf.GetComponent<TMP_Text>();
        }
    }

    // ==================== 이벤트 구독 ====================
    private void SubscribeEvents()
    {
        if (GameResultManager.Instance != null)
        {
            GameResultManager.Instance.OnCatch += HandleCatch;
            GameResultManager.Instance.OnTry += HandleTry;
        }
        else
        {
            // Changed: GameResultManager가 아직 없을 경우 코루틴으로 대기.
            // Why: 씬 로드 순서에 따라 Instance가 늦게 할당될 수 있음.
            StartCoroutine(WaitForGameResultManager());
        }
    }

    private IEnumerator WaitForGameResultManager()
    {
        while (GameResultManager.Instance == null)
            yield return null;

        GameResultManager.Instance.OnCatch += HandleCatch;
        GameResultManager.Instance.OnTry += HandleTry;
    }

    private void UnsubscribeEvents()
    {
        if (GameResultManager.Instance != null)
        {
            GameResultManager.Instance.OnCatch -= HandleCatch;
            GameResultManager.Instance.OnTry -= HandleTry;
        }
    }

    // ==================== 이벤트 핸들러 ====================
    private void HandleCatch(EmotionType emotion)
    {
        int idx = Array.IndexOf(EmotionOrder, emotion);
        if (idx < 0 || idx >= 6) return;

        int count = GameResultManager.Instance.GetEmotionCount(emotion);
        UpdateSlot(idx, emotion, count);
    }

    private void HandleTry(int totalTries)
    {
        if (totalTriesText != null)
            totalTriesText.text = $"Tries: {totalTries}";
    }

    // ==================== 슬롯 업데이트 ====================
    private void UpdateSlot(int idx, EmotionType emotion, int count)
    {
        if (slotRoots[idx] == null) return;

        Transform slot = slotRoots[idx].transform;
        Color emotionColor = EmotionColors[emotion];
        bool isFirstCatch = (badges[idx] == null);

        // ---- Placeholder 숨기기 ----
        // Changed: 첫 뽑기 시 "?" placeholder를 비활성화.
        if (placeholderTexts[idx] != null)
            placeholderTexts[idx].gameObject.SetActive(false);

        // ---- 감정 이름 색상 변경 (회색 → 감정 색상) ----
        if (nameTexts[idx] != null)
            nameTexts[idx].color = emotionColor;

        // ---- 배지 생성 또는 업데이트 ----
        if (isFirstCatch)
        {
            // Changed: 첫 뽑기 시 Sphere primitive로 배지를 생성하고 감정 색상 머티리얼 적용.
            CreateBadge(idx, slot, emotionColor);
            // Changed: 카운트 TMP_Text를 배지 앞면에 생성.
            CreateCountText(idx, slot);
        }

        // 카운트 텍스트 업데이트
        if (countTexts[idx] != null)
            countTexts[idx].text = count.ToString();

        // Changed: 미니어처 인형 배지인지 확인하여 스케일 기준을 분기.
        // Why: 미니어처 인형은 SceneSetup이 지정한 displayScale 기반이고, primitive 배지는 BadgeBaseSize 기반.
        bool isMiniDoll = badges[idx] != null && badges[idx].name.StartsWith("MiniDoll_");

        Vector3 targetScale;
        if (isMiniDoll)
        {
            // 미니어처 인형: SceneSetup displayScale(0.08) 기반으로 카운트에 비례 성장
            float baseScale = 0.08f;
            float growth = Mathf.Min(baseScale + BadgeGrowthPerCount * (count - 1), baseScale * 2f);
            targetScale = Vector3.one * growth;
        }
        else
        {
            // Primitive 배지: 기존 BadgeBaseSize 기반
            float targetSize = Mathf.Min(BadgeBaseSize + BadgeGrowthPerCount * (count - 1), BadgeMaxSize);
            targetScale = Vector3.one * targetSize;
        }

        // Changed: Emission 강도를 카운트에 비례하여 증가.
        // Why: 많이 뽑은 감정일수록 배지가 더 밝게 빛나는 시각적 피드백.
        if (badgeMaterials[idx] != null)
        {
            float emissionIntensity = Mathf.Min(0.3f + 0.15f * (count - 1), 1.5f);
            Color emissionColor = emotionColor * emissionIntensity;
            badgeMaterials[idx].EnableKeyword("_EMISSION");
            badgeMaterials[idx].SetColor("_EmissionColor", emissionColor);
        }

        // 스케일 애니메이션 (0→target 또는 current→target)
        if (isFirstCatch)
        {
            // Changed: 첫 출현 시 스케일 0→target EaseOutBack 애니메이션.
            badges[idx].transform.localScale = Vector3.zero;
            StartCoroutine(AnimateScale(badges[idx].transform, Vector3.zero, targetScale, AnimDuration));
        }
        else
        {
            // Changed: 추가 뽑기 시 현재→새 크기로 부드러운 전환.
            Vector3 currentScale = badges[idx].transform.localScale;
            StartCoroutine(AnimateScale(badges[idx].transform, currentScale, targetScale, AnimDuration));
        }
    }

    // ==================== 배지 생성 ====================
    private void CreateBadge(int idx, Transform slot, Color color)
    {
        // Changed: SceneSetup이 에디터 타임에 배치한 MiniDoll_* 자식을 우선 활성화.
        // Why: Sphere/Quad primitive 대신 실제 인형 FBX 축소판을 스코어보드 배지로 표시하여 디자인 정합성 개선.
        Transform miniDoll = slot.Find($"MiniDoll_{EmotionOrder[idx]}");
        if (miniDoll != null)
        {
            // ---- 미니어처 인형 배지: SceneSetup이 배치한 FBX 축소판 활성화 ----
            miniDoll.gameObject.SetActive(true);
            badges[idx] = miniDoll.gameObject;

            // Changed: 미니어처 인형의 렌더러에 Emission을 추가하여 감정 색상 글로우 효과.
            // Why: 뽑힌 감정 인형이 빛나면서 시각적 피드백 제공.
            var renderers = miniDoll.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                // Changed: 첫 번째 렌더러의 머티리얼을 인스턴스화하여 Emission 추가.
                // Why: sharedMaterial을 직접 수정하면 다른 슬롯의 같은 머티리얼도 영향 받으므로 인스턴스 사용.
                Material mat = renderers[0].material; // 인스턴스화됨
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 0.3f);
                badgeMaterials[idx] = mat;
            }
            return;
        }

        // ---- Fallback: MiniDoll이 없으면 기존 방식 (Quad + 인형 텍스처 또는 Sphere) ----
        bool hasDollTexture = dollTextures != null && idx < dollTextures.Length && dollTextures[idx] != null;

        if (hasDollTexture)
        {
            // ---- Quad 배지: 인형 텍스처 표시 ----
            GameObject badge = GameObject.CreatePrimitive(PrimitiveType.Quad);
            badge.name = "Badge";
            badge.transform.SetParent(slot, false);
            badge.transform.localPosition = Vector3.zero;
            badge.transform.localScale = Vector3.zero; // Changed: 애니메이션 시작 크기

            // Collider 제거 — 배지는 시각 전용
            Collider col = badge.GetComponent<Collider>();
            if (col != null)
                Destroy(col);

            // Changed: URP Lit + Alpha Clipping으로 인형 텍스처를 배지에 적용.
            // Why: 인형 텍스처 배경이 투명(알파)이면 자연스럽게 잘려 보이고, 불투명이면 사각형 전체 표시.
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.shader.name == "Hidden/InternalErrorShader")
            {
                mat = new Material(Shader.Find("Standard"));
            }
            mat.SetTexture("_BaseMap", dollTextures[idx]);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.3f);
            // Changed: Alpha Clipping 활성화로 텍스처 투명 영역 제거.
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            // Changed: 감정 색상 Emission으로 미세한 글로우 효과 추가.
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 0.2f);

            badge.GetComponent<Renderer>().material = mat;
            badgeMaterials[idx] = mat;
            badges[idx] = badge;
        }
        else
        {
            // ---- Fallback: 기존 Sphere 배지 (텍스처 없을 때) ----
            GameObject badge = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            badge.name = "Badge";
            badge.transform.SetParent(slot, false);
            badge.transform.localPosition = Vector3.zero;
            badge.transform.localScale = Vector3.zero;

            Collider col = badge.GetComponent<Collider>();
            if (col != null)
                Destroy(col);

            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat.shader.name == "Hidden/InternalErrorShader")
            {
                mat = new Material(Shader.Find("Standard"));
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.6f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 0.3f);

            badge.GetComponent<Renderer>().material = mat;
            badgeMaterials[idx] = mat;
            badges[idx] = badge;
        }
    }

    // ==================== 카운트 텍스트 생성 ====================
    private void CreateCountText(int idx, Transform slot)
    {
        // Changed: Microsoft MR 공식 권장 TMP 3D 스케일 패턴 적용.
        // Why: localScale 0.005 + fontSize 48로 SDF 품질을 유지하면서 월드 크기를 올바르게 제어.
        // 출처: https://learn.microsoft.com/en-us/windows/mixed-reality/develop/unity/text-in-unity
        GameObject countGo = new GameObject("CountText");
        countGo.transform.SetParent(slot, false);
        // Z 오프셋: 배지 앞면에 표시 (Scoreboard는 -Z 방향을 바라보므로 -Z가 앞)
        countGo.transform.localPosition = new Vector3(0f, 0f, -0.07f);
        // Changed: Y=180 회전으로 부모 Scoreboard_Main의 Y=180을 상쇄.
        // Why: TMP 3D는 -Z 방향 렌더링. identity면 벽 쪽으로 보여 플레이어에게 안 보임.
        countGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        countGo.transform.localScale = new Vector3(0.005f, 0.005f, 0.005f); // Changed: Microsoft MR 공식 권장 TMP 3D 스케일.

        var tmp = countGo.AddComponent<TextMeshPro>();
        tmp.text = "1";
        tmp.alignment = TextAlignmentOptions.Center;
        // Changed: fontSize 48. 0.005 스케일 적용 후 약 1.7cm 높이.
        tmp.fontSize = 48;
        tmp.color = new Color(0.15f, 0.15f, 0.15f);
        tmp.fontStyle = FontStyles.Bold;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow; // Changed: scale로 제어하므로 Overflow OK.
        var rt = countGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(40, 25); // Changed: 0.005 적용 후 0.2m × 0.125m.

        countTexts[idx] = tmp;
    }

    // ==================== 스케일 애니메이션 (EaseOutBack) ====================
    private IEnumerator AnimateScale(Transform target, Vector3 from, Vector3 to, float duration)
    {
        if (target == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutBack(t);
            target.localScale = Vector3.LerpUnclamped(from, to, eased);
            yield return null;
        }
        if (target != null)
            target.localScale = to;
    }

    // Changed: EaseOutBack 이징 — 끝에서 살짝 튕기는 효과.
    // Why: I Expect You To Die 스타일 "팝" 느낌의 출현 애니메이션.
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    // ==================== 유틸 ====================
    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out var c);
        return c;
    }
}
