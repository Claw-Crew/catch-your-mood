using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Changed: 자동 렌더 검증 파이프라인 추가.
/// Why: 수동 확인만으로는 회귀를 놓치기 쉬워서, 고정 카메라 캡처 + baseline 비교를 CLI/메뉴에서 실행 가능하게 함.
/// </summary>
public static class SceneRenderValidation
{
    const string MainScenePath = "Assets/00.Main/Scenes/Main/MainScene.unity";
    const string DefaultOutDir = "Assets/00.Main/Validation/Renders/latest";
    const string DefaultBaselineDir = "Assets/00.Main/Validation/Renders/baseline";
    const int Width = 1280;
    const int Height = 720;
    const float DefaultThreshold = 0.06f; // MAE(0~1) 기준

    // Changed: 고정 샷 정의를 코드화.
    // Why: 비교 기준을 일관되게 유지하려면 카메라 위치/회전을 고정해야 함.
    struct Shot
    {
        public string name;
        public Vector3 position;
        public Vector3 euler;
        public float fov;
    }

    static readonly Shot[] Shots =
    {
        new Shot { name = "front", position = new Vector3(0.0f, 1.45f, -1.90f), euler = new Vector3(10f, 0f, 0f), fov = 58f },
        new Shot { name = "left",  position = new Vector3(-1.45f, 1.50f, -0.65f), euler = new Vector3(11f, 33f, 0f), fov = 58f },
        new Shot { name = "right", position = new Vector3(1.45f, 1.50f, -0.65f), euler = new Vector3(11f, -33f, 0f), fov = 58f },
        new Shot { name = "top",   position = new Vector3(0.0f, 2.35f, -0.10f), euler = new Vector3(52f, 0f, 0f), fov = 52f },
    };

    [MenuItem("Claw Crew/Render Validation/Capture Shot Set", false, 20)]
    public static void CaptureMenu()
    {
        var outDir = Path.GetFullPath(DefaultOutDir);
        CaptureShotSet(outDir);
        AssetDatabase.Refresh();
        Debug.Log($"[RenderValidation] Capture completed: {outDir}");
    }

    [MenuItem("Claw Crew/Render Validation/Compare With Baseline", false, 21)]
    public static void CompareMenu()
    {
        var outDir = Path.GetFullPath(DefaultOutDir);
        var baseDir = Path.GetFullPath(DefaultBaselineDir);
        CaptureShotSet(outDir);
        var report = CompareFolders(baseDir, outDir, DefaultThreshold);
        Debug.Log(report.ToHumanReadable());
    }

    // Changed: CI/CLI 진입점 추가.
    // Why: -batchmode -executeMethod 로 자동 검증을 실행하기 위함.
    public static void CaptureFromCli()
    {
        var args = ParseArgs(Environment.GetCommandLineArgs());
        var outDir = GetArg(args, "renderOutDir", Path.GetFullPath(DefaultOutDir));
        CaptureShotSet(outDir);
        AssetDatabase.Refresh();
        EditorApplication.Exit(0);
    }

    // Changed: CI/CLI 비교 진입점 추가.
    // Why: baseline 대비 통과/실패를 exit code로 반환해서 파이프라인에 연결하기 위함.
    public static void CompareFromCli()
    {
        var args = ParseArgs(Environment.GetCommandLineArgs());
        var outDir = GetArg(args, "renderOutDir", Path.GetFullPath(DefaultOutDir));
        var baseDir = GetArg(args, "renderBaselineDir", Path.GetFullPath(DefaultBaselineDir));
        var threshold = GetArgFloat(args, "renderThreshold", DefaultThreshold);

        CaptureShotSet(outDir);
        var report = CompareFolders(baseDir, outDir, threshold);
        var reportPath = Path.Combine(outDir, "compare_report.txt");
        File.WriteAllText(reportPath, report.ToHumanReadable());
        Debug.Log(report.ToHumanReadable());
        EditorApplication.Exit(report.passed ? 0 : 2);
    }

    // Changed: SceneSetup 재빌드를 항상 수행.
    // Why: 코드 수정 후 기존 MainScene이 남아 있으면 "변경이 안 된 것처럼" 보이는 문제를 방지.
    static void EnsureMainSceneReady()
    {
        SceneSetup.Build();
        EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
    }

    // Changed: 캡처 파이프라인 구현.
    // Why: 샷별 PNG를 deterministic하게 뽑아 baseline 비교 입력 데이터 생성.
    static void CaptureShotSet(string outDir)
    {
        EnsureMainSceneReady();
        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

        foreach (var shot in Shots)
        {
            var pngPath = Path.Combine(outDir, $"{shot.name}.png");
            CaptureSingleShot(pngPath, shot);
        }
    }

    // Changed: 카메라 1회 캡처 구현.
    // Why: 에디터 화면 해상도/뷰포트 영향 없이 RenderTexture에서 직접 추출.
    static void CaptureSingleShot(string outputPath, Shot shot)
    {
        var cameraGo = new GameObject($"ValidationCam_{shot.name}");
        var cam = cameraGo.AddComponent<Camera>();
        cam.transform.position = shot.position;
        cam.transform.rotation = Quaternion.Euler(shot.euler);
        cam.fieldOfView = shot.fov;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 50f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.88f, 0.90f, 0.90f, 1f);
        cam.allowHDR = false;
        cam.allowMSAA = false;

        var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 1;
        cam.targetTexture = rt;
        cam.Render();

        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
        tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        tex.Apply(false, false);

        var bytes = tex.EncodeToPNG();
        File.WriteAllBytes(outputPath, bytes);

        RenderTexture.active = prev;
        cam.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(tex);
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(cameraGo);
    }

    struct CompareRow
    {
        public string name;
        public bool missingBaseline;
        public bool missingCurrent;
        public float mae;
        public bool passed;
    }

    struct CompareReport
    {
        public bool passed;
        public float threshold;
        public List<CompareRow> rows;

        public string ToHumanReadable()
        {
            var sw = new System.Text.StringBuilder();
            sw.AppendLine("[RenderValidation] Compare Report");
            sw.AppendLine($"passed: {passed}");
            sw.AppendLine($"threshold: {threshold:F4}");
            foreach (var r in rows)
            {
                sw.AppendLine($"- {r.name}: passed={r.passed}, mae={r.mae:F5}, missingBaseline={r.missingBaseline}, missingCurrent={r.missingCurrent}");
            }
            return sw.ToString();
        }
    }

    // Changed: baseline 비교 구현.
    // Why: 룩 회귀를 수치로 감지하고 CI 실패 조건으로 연결하기 위함.
    static CompareReport CompareFolders(string baselineDir, string currentDir, float threshold)
    {
        var rows = new List<CompareRow>();
        var allPassed = true;

        foreach (var shot in Shots)
        {
            var bPath = Path.Combine(baselineDir, $"{shot.name}.png");
            var cPath = Path.Combine(currentDir, $"{shot.name}.png");
            // Changed: CompareRow에 없는 필드(threshold) 초기화 제거.
            // Why: 컴파일 오류를 막아 SceneSetup 최신 코드가 정상 반영되게 함.
            var row = new CompareRow { name = shot.name, mae = 1f };

            if (!File.Exists(bPath))
            {
                row.missingBaseline = true;
                row.passed = false;
                rows.Add(row);
                allPassed = false;
                continue;
            }

            if (!File.Exists(cPath))
            {
                row.missingCurrent = true;
                row.passed = false;
                rows.Add(row);
                allPassed = false;
                continue;
            }

            var mae = ComputeMae(bPath, cPath);
            row.mae = mae;
            row.passed = mae <= threshold;
            rows.Add(row);
            if (!row.passed) allPassed = false;
        }

        return new CompareReport
        {
            passed = allPassed,
            threshold = threshold,
            rows = rows
        };
    }

    // Changed: 픽셀 MAE 계산 로직 추가.
    // Why: 단순/빠른 회귀 체크를 위해 RGB 절대오차 평균을 사용.
    static float ComputeMae(string baselinePath, string currentPath)
    {
        var bBytes = File.ReadAllBytes(baselinePath);
        var cBytes = File.ReadAllBytes(currentPath);
        var bTex = new Texture2D(2, 2, TextureFormat.RGB24, false, false);
        var cTex = new Texture2D(2, 2, TextureFormat.RGB24, false, false);
        bTex.LoadImage(bBytes, false);
        cTex.LoadImage(cBytes, false);

        if (bTex.width != cTex.width || bTex.height != cTex.height)
        {
            UnityEngine.Object.DestroyImmediate(bTex);
            UnityEngine.Object.DestroyImmediate(cTex);
            return 1f;
        }

        var b = bTex.GetPixels32();
        var c = cTex.GetPixels32();
        double sum = 0.0;
        for (var i = 0; i < b.Length; i++)
        {
            sum += Math.Abs(b[i].r - c[i].r) / 255.0;
            sum += Math.Abs(b[i].g - c[i].g) / 255.0;
            sum += Math.Abs(b[i].b - c[i].b) / 255.0;
        }

        UnityEngine.Object.DestroyImmediate(bTex);
        UnityEngine.Object.DestroyImmediate(cTex);
        return (float)(sum / (b.Length * 3.0));
    }

    // Changed: CLI 인자 파서 추가.
    // Why: CI 환경별 경로/임계값을 런타임에 주입하기 위함.
    static Dictionary<string, string> ParseArgs(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("-")) continue;
            var key = args[i].TrimStart('-');
            if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
            {
                map[key] = args[i + 1];
                i++;
            }
            else
            {
                map[key] = "true";
            }
        }
        return map;
    }

    static string GetArg(Dictionary<string, string> args, string key, string fallback)
    {
        return args.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;
    }

    static float GetArgFloat(Dictionary<string, string> args, string key, float fallback)
    {
        if (!args.TryGetValue(key, out var v)) return fallback;
        return float.TryParse(v, out var parsed) ? parsed : fallback;
    }
}
