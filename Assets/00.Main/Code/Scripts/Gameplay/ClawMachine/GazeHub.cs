using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gaze-driven mood dispatcher + outline-glow indicator.
///
/// When the player looks at a doll for `dwellToTrigger` seconds:
///   - that doll's IMoodReaction.OnApproach() fires (delegated, same as before),
///   - and its body Renderers receive an emission boost colored by the doll's emotion
///     (Angry=red, Sad=blue, Happy=yellow, Scared=purple, Sleepy=teal, Serene=soft gold).
///
/// When the gaze leaves:
///   - reaction.OnRetreat() fires, and the original emission of each renderer is restored
///     (no Serene-style emission state is permanently overwritten).
///
/// Coordination with ClawHub (claw has priority):
///   - Never starts on the doll ClawHub is currently handling.
///   - If the claw takes over the doll the gaze was driving, silently relinquishes
///     (reaction stays in claw's hands, glow is restored).
///   - Never calls OnGrabbed/OnReleased — only the claw grabs.
///
/// Setup:
///   - Attach to the VR head camera. headTransform auto-binds to Camera.main.
///   - Dolls must have a DollInfo (with EmotionType) + Collider on "Doll" layer (same as ClawHub).
///   - Whole-body glow is applied via MaterialPropertyBlock on every Renderer in
///     the doll's children (no material assets are mutated).
/// </summary>
[DisallowMultipleComponent]
public class GazeHub : MonoBehaviour
{
    [Header("References (auto-found if null)")]
    [SerializeField] private Transform headTransform;
    [SerializeField] private ClawHub clawHub;

    [Header("Detection")]
    [SerializeField] private LayerMask dollLayer;
    [SerializeField] private float maxGazeDistance = 3f;
    [SerializeField] private float dwellToTrigger = 0.4f;

    [Header("Outline Glow")]
    [SerializeField] private bool glowEnabled = true;
    [Tooltip("Multiplier on the emotion color before writing to _EmissionColor (HDR).")]
    [SerializeField] private float glowIntensity = 0.8f;
    [Tooltip("Skip these emotions — their reaction already controls emission (e.g., Serene). Prevents double-write blow-out.")]
    [SerializeField] private EmotionType[] skipEmotions = new[] { EmotionType.Serene };
    [Tooltip("Per-emotion outline glow colors. Edit in Inspector to tune.")]
    [SerializeField] private EmotionGlowSetting[] emotionGlowSettings = new[]
    {
        new EmotionGlowSetting { emotion = EmotionType.Angry,  color = new Color(1.00f, 0.20f, 0.20f) },
        new EmotionGlowSetting { emotion = EmotionType.Happy,  color = new Color(1.00f, 0.85f, 0.20f) },
        new EmotionGlowSetting { emotion = EmotionType.Sad,    color = new Color(0.30f, 0.55f, 1.00f) },
        new EmotionGlowSetting { emotion = EmotionType.Scared, color = new Color(0.70f, 0.30f, 1.00f) },
        new EmotionGlowSetting { emotion = EmotionType.Sleepy, color = new Color(0.30f, 0.85f, 0.85f) },
        new EmotionGlowSetting { emotion = EmotionType.Serene, color = new Color(1.00f, 0.95f, 0.70f) },
    };

    [Serializable]
    public struct EmotionGlowSetting
    {
        public EmotionType emotion;
        public Color color;
    }

    // --- gaze state ---
    private IMoodReaction currentGazeReaction;
    private IMoodReaction pendingReaction;
    private float dwellTimer;

    // --- glow state ---
    // Snapshot of each affected renderer's emission BEFORE we overrode it,
    // so we can restore exactly what was there (incl. other scripts' emission like Serene).
    private readonly Dictionary<Renderer, Color> glowSnapshots = new();
    private MaterialPropertyBlock glowBlock;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        if (headTransform == null && Camera.main != null)
            headTransform = Camera.main.transform;

        if (clawHub == null)
            clawHub = FindFirstObjectByType<ClawHub>();

        if (dollLayer.value == 0)
            dollLayer = LayerMask.GetMask("Doll");

        glowBlock = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (headTransform == null) return;

        IMoodReaction gazed = RaycastDoll();

        // claw 우선권: claw가 hover/grab 중인 인형은 시선이 안 건드림
        if (gazed != null && clawHub != null && ReferenceEquals(gazed, clawHub.CurrentReaction))
            gazed = null;

        // claw가 우리가 운전하던 인형을 가져갔으면 조용히 양보 (OnRetreat 호출 X)
        if (currentGazeReaction != null && clawHub != null
            && ReferenceEquals(currentGazeReaction, clawHub.CurrentReaction))
        {
            // Glow는 우리가 켰으니 우리가 끔. 반응 호출은 claw 쪽이 이어받음.
            StopGlow();
            currentGazeReaction = null;
            pendingReaction = null;
            dwellTimer = 0f;
        }

        // 같은 대상 운전 중(혹은 둘 다 null) → dwell 리셋만
        if (ReferenceEquals(gazed, currentGazeReaction))
        {
            pendingReaction = null;
            dwellTimer = 0f;
            return;
        }

        // 시선이 아무 인형도 안 봄 → 현재 반응·glow 종료
        if (gazed == null)
        {
            currentGazeReaction?.OnRetreat();
            StopGlow();
            currentGazeReaction = null;
            pendingReaction = null;
            dwellTimer = 0f;
            return;
        }

        // 새 인형 후보 → dwell 누적
        if (ReferenceEquals(gazed, pendingReaction))
        {
            dwellTimer += Time.deltaTime;
        }
        else
        {
            pendingReaction = gazed;
            dwellTimer = 0f;
        }

        // dwell 충족 → 이전 반응 종료 후 새 반응 시작 (glow 포함)
        if (dwellTimer >= dwellToTrigger)
        {
            currentGazeReaction?.OnRetreat();
            StopGlow();

            currentGazeReaction = gazed;
            currentGazeReaction.OnApproach();
            StartGlowOn(gazed);

            pendingReaction = null;
            dwellTimer = 0f;
        }
    }

    /// <summary>머리 정면 레이로 dollLayer를 쏴서 응시 중인 IMoodReaction을 반환(없으면 null).</summary>
    private IMoodReaction RaycastDoll()
    {
        if (Physics.Raycast(headTransform.position, headTransform.forward,
                            out RaycastHit hit, maxGazeDistance, dollLayer))
        {
            return hit.collider.GetComponent<IMoodReaction>()
                   ?? hit.collider.GetComponentInParent<IMoodReaction>();
        }
        return null;
    }

    private void OnDisable()
    {
        // 비활성화 시 진행 중인 시선 반응·glow 정리
        currentGazeReaction?.OnRetreat();
        StopGlow();
        currentGazeReaction = null;
        pendingReaction = null;
        dwellTimer = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        if (headTransform == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(headTransform.position,
                        headTransform.position + headTransform.forward * maxGazeDistance);
    }

    // ===========================================================================
    //                              Outline Glow
    // ===========================================================================

    /// <summary>
    /// 응시 시작 — 인형의 모든 Renderer에 감정 컬러 emission을 입힘.
    /// 각 렌더러의 이전 emission을 스냅샷에 저장해두고 StopGlow에서 그대로 복원.
    /// </summary>
    private void StartGlowOn(IMoodReaction target)
    {
        if (!glowEnabled) return;
        if (target is not MonoBehaviour mb) return;

        // 인형의 감정 정보 조회
        var info = mb.GetComponent<DollInfo>() ?? mb.GetComponentInParent<DollInfo>();
        if (info == null) return;

        // 자체 emission 효과를 갖는 감정(예: Serene)은 건너뜀 — 이중 적용 시 HDR + Bloom 화이트아웃 방지
        if (skipEmotions != null)
        {
            for (int i = 0; i < skipEmotions.Length; i++)
            {
                if (skipEmotions[i] == info.emotionType) return;
            }
        }

        // 인형 전체 Renderer 수집 (자식 포함)
        // DollInfo가 인형 루트에 있을 가능성이 크니 거기서부터 children 탐색
        var rootGo = (info != null) ? info.gameObject : mb.gameObject;
        var rends = rootGo.GetComponentsInChildren<Renderer>(includeInactive: false);
        if (rends == null || rends.Length == 0) return;

        Color glowColor = ResolveGlowColor(info.emotionType) * glowIntensity;

        // 이전 emission 스냅샷 + 새 emission 적용
        glowSnapshots.Clear();
        foreach (var r in rends)
        {
            if (r == null) continue;

            // Safety: 머티리얼에 _EMISSION 키워드 강제 활성 (Inspector 체크 누락·셰이더 캐시 대응)
            // .mat 파일에 이미 활성돼 있으면 무해 — 단지 런타임에 한 번 더 확실히 켜는 것.
            var mats = r.sharedMaterials;
            if (mats != null)
            {
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && !mats[i].IsKeywordEnabled("_EMISSION"))
                        mats[i].EnableKeyword("_EMISSION");
                }
            }

            glowSnapshots[r] = GetEmission(r);
            SetEmission(r, glowColor);
        }
    }

    /// <summary>응시 종료 — 각 렌더러의 이전 emission을 그대로 복원.</summary>
    private void StopGlow()
    {
        if (glowSnapshots.Count == 0) return;
        foreach (var kv in glowSnapshots)
        {
            if (kv.Key == null) continue;
            SetEmission(kv.Key, kv.Value);
        }
        glowSnapshots.Clear();
    }

    private Color ResolveGlowColor(EmotionType emotion)
    {
        if (emotionGlowSettings != null)
        {
            for (int i = 0; i < emotionGlowSettings.Length; i++)
            {
                if (emotionGlowSettings[i].emotion == emotion)
                    return emotionGlowSettings[i].color;
            }
        }
        return Color.white;
    }

    private Color GetEmission(Renderer r)
    {
        if (r == null) return Color.black;
        r.GetPropertyBlock(glowBlock);
        return glowBlock.GetColor(EmissionColorId);
    }

    private void SetEmission(Renderer r, Color c)
    {
        if (r == null) return;
        r.GetPropertyBlock(glowBlock);
        glowBlock.SetColor(EmissionColorId, c);
        r.SetPropertyBlock(glowBlock);
    }
}
