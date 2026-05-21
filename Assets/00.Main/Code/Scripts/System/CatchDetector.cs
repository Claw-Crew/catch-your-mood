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

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Doll"))
        {
            DollInfo info = other.GetComponent<DollInfo>();

            if (info != null)
            {
                GameResultManager.Instance.RegisterCatch(info.emotionType);
                // Changed: 인형 획득 시 파티클 이펙트 생성.
                // Why: 짧은 축하 연출로 뽑기 성공 체감 강화.
                SpawnCatchParticle(other.transform.position, info.emotionType);
            }
        }
    }

    // Changed: 인형 획득 위치에서 0.5초간 8-12개 파티클을 방사하고 자동 소멸.
    // Why: 과도하지 않은 짧은 이펙트로 VR 시야에서 성공 피드백을 전달.
    private void SpawnCatchParticle(Vector3 position, EmotionType emotion)
    {
        var go = new GameObject("CatchParticle");
        go.transform.position = position;

        var ps = go.AddComponent<ParticleSystem>();
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