using System.Collections.Generic;
using UnityEngine;

public class CatchDetector : MonoBehaviour
{
    // NOTE: 향후 Kenney Particle Pack (CC0) 텍스처를 사용할 경우
    //       Assets/00.Main/Art/VFX/Textures/heart.png 를 Resources 폴더로 이동하고 로드.

    // Changed: 감정별 파스텔 색상 — 파티클 방출 시 감정에 맞는 색상 사용.
    private static readonly Dictionary<EmotionType, Color> EmotionParticleColors = new Dictionary<EmotionType, Color>
    {
        { EmotionType.Happy,  HexColor("FFD93D") },
        { EmotionType.Angry,  HexColor("FF6B6B") },
        { EmotionType.Sleepy, HexColor("C3AED6") },
        { EmotionType.Sad,    HexColor("74B9FF") },
        { EmotionType.Scared, HexColor("A29BFE") },
        { EmotionType.Serene, HexColor("55EFC4") },
    };

    [Header("Catch Respawn")]
    // Changed: 같은 인형이 CatchZone 안에서 짧은 시간 여러 번 감지되어도 한 번만 처리.
    // Why: Trigger 재진입/복수 collider 이벤트로 Scoreboard 카운트가 중복 증가하는 것을 막기 위함.
    [SerializeField] private float duplicateSuppressSeconds = 0.5f;

    // Changed: 획득 연출 후 인형을 원래 spawn 위치로 되돌리는 지연 시간.
    // Why: 점수/파티클 피드백이 보인 뒤 플레이필드에 같은 감정 인형을 다시 공급하기 위함.
    [SerializeField] private float respawnDelaySeconds = 1.0f;

    // Changed: 인형 인스턴스별 마지막 catch 처리 시각 기록.
    // Why: 같은 DollInfo가 짧은 시간 안에 여러 OnTriggerEnter를 발생시켜도 데이터 흐름은 한 번만 타도록 함.
    private readonly Dictionary<int, float> recentCatchTimes = new Dictionary<int, float>();

    private void OnTriggerEnter(Collider other)
    {
        // Changed: Doll tag와 DollInfo를 먼저 확인하고, 이후 기존 RegisterCatch 흐름을 유지.
        // Why: CatchDetector -> DollInfo.emotionType -> GameResultManager.RegisterCatch -> Scoreboard 업데이트 순서를 깨지 않기 위함.
        if (!TryGetDollInfo(other, out DollInfo info))
            return;

        DollRespawnManager respawnManager = DollRespawnManager.Instance;
        if (respawnManager.IsRespawnPending(info) || ShouldSuppressDuplicateCatch(info))
            return;

        if (GameResultManager.Instance != null)
        {
            GameResultManager.Instance.RegisterCatch(info.emotionType);
        }

        // Changed: 인형 획득 시 파티클 이펙트 생성.
        // Why: 짧은 축하 연출로 뽑기 성공 체감 강화.
        SpawnCatchParticle(info.transform.position, info.emotionType);

        // Changed: catch 처리 후 리스폰 매니저에 같은 인형의 지연 리셋을 예약.
        // Why: 점수/Scoreboard 업데이트 이후 동일 감정 인형을 다시 플레이필드 위로 공급하기 위함.
        respawnManager.RespawnAfterDelay(info, respawnDelaySeconds);
    }

    // Changed: root collider, attached Rigidbody, parent DollInfo 경로에서 DollInfo를 찾도록 보강.
    // Why: 현재 root collider 구조를 유지하면서도 하위 collider 이벤트가 들어와도 같은 DollInfo 흐름으로 처리하기 위함.
    private static bool TryGetDollInfo(Collider other, out DollInfo info)
    {
        info = null;
        if (other == null) return false;

        Rigidbody attachedRigidbody = other.attachedRigidbody;
        DollInfo parentInfo = other.GetComponentInParent<DollInfo>();
        bool isDollTagged = other.CompareTag("Doll") ||
            (attachedRigidbody != null && attachedRigidbody.CompareTag("Doll")) ||
            (parentInfo != null && parentInfo.CompareTag("Doll"));

        if (!isDollTagged)
            return false;

        info = other.GetComponent<DollInfo>();
        if (info == null && attachedRigidbody != null)
            info = attachedRigidbody.GetComponent<DollInfo>();
        if (info == null)
            info = parentInfo;

        return info != null;
    }

    // Changed: accepted catch만 timestamp를 갱신하는 중복 방지 게이트 추가.
    // Why: 연속 trigger 이벤트가 Scoreboard와 리스폰 큐에 중복 반영되지 않도록 하기 위함.
    private bool ShouldSuppressDuplicateCatch(DollInfo info)
    {
        int instanceId = info.GetInstanceID();
        float now = Time.time;
        float suppressSeconds = Mathf.Max(0f, duplicateSuppressSeconds);

        if (suppressSeconds > 0f &&
            recentCatchTimes.TryGetValue(instanceId, out float lastCatchTime) &&
            now - lastCatchTime < suppressSeconds)
        {
            return true;
        }

        recentCatchTimes[instanceId] = now;
        return false;
    }

    // Changed: 인형 획득 위치에서 0.5초간 8-12개 파티클을 방사하고 자동 소멸.
    // Why: 과도하지 않은 짧은 이펙트로 VR 시야에서 성공 피드백을 전달.
    private void SpawnCatchParticle(Vector3 position, EmotionType emotion)
    {
        var go = new GameObject("CatchParticle");
        go.transform.position = position;

        var ps = go.AddComponent<ParticleSystem>();
        // Changed: ParticleSystem은 AddComponent 시 자동 Play됨. duration 변경 전에 Stop 필요.
        // Why: "Setting the duration while system is still playing is not supported" 에러 수정.
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = 0.6f;
        main.startSpeed = 1.5f;
        main.startSize = 0.04f;
        main.maxParticles = 12;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        // Changed: 재생 완료 후 자동 소멸.
        main.stopAction = ParticleSystemStopAction.Destroy;

        // Changed: 감정별 색상 적용.
        Color particleColor = EmotionParticleColors.ContainsKey(emotion)
            ? EmotionParticleColors[emotion]
            : Color.white;
        main.startColor = particleColor;

        // Changed: 방출량 설정 — 0.5초 동안 버스트로 8-12개 방출.
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, 8, 12)
        });

        // Changed: 구형 방출 — 모든 방향으로 퍼지는 축하 느낌.
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.05f;

        // Changed: 크기가 수명에 따라 줄어들도록 설정.
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        // Changed: 파티클 머티리얼 — Kenney 텍스처가 있으면 적용, 없으면 기본 파티클 셰이더 사용.
        // Why: 에셋이 없어도 기본 흰색 원형 파티클로 동작하도록 fallback 보장.
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        var mat = new Material(Shader.Find("Particles/Standard Unlit"));
        if (mat.shader.name == "Hidden/InternalErrorShader")
        {
            // Changed: URP 환경에서는 Universal Render Pipeline/Particles/Unlit 셰이더 시도.
            var urpShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (urpShader != null) mat = new Material(urpShader);
        }
        mat.SetColor("_BaseColor", particleColor);
        mat.SetColor("_Color", particleColor); // Standard Unlit 호환
        // Changed: Additive 블렌딩으로 밝은 파티클 연출.
        mat.SetFloat("_Surface", 0f); // Opaque base, additive via render mode
        renderer.material = mat;

        ps.Play();
        // Changed: 파티클 시스템 소멸 보장 — StopAction.Destroy가 동작하지 않는 환경 대비 타이머 삭제.
        Destroy(go, 1.5f);
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out var c);
        return c;
    }
}
