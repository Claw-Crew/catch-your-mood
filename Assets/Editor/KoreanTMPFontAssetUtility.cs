using System;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Editor-only Korean TMP Font Asset bootstrapper for result UI text.
/// </summary>
public static class KoreanTMPFontAssetUtility
{
    public const string SourceFontPath = "Assets/Fonts/NotoSansKR-Regular.ttf";
    public const string FontAssetPath = "Assets/Fonts/NotoSansKR-Regular SDF.asset";

    private const int SamplingPointSize = 90;
    private const int AtlasPadding = 9;
    private const int AtlasSize = 4096;

    // Changed: 결과지에서 사용하는 한국어/기호를 TMP Font Asset에 미리 추가한다.
    // Why: Build Main Scene 직후 Quest 빌드 씬에 한글 렌더링 가능한 NotoSansKR 기반 TMP FontAsset 참조가 남아야 한다.
    private const string KoreanResultCharacters =
        "오늘의 감정 레시피" +
        "햇살 가득 바닐라 라떼" +
        "비 오는 날의 카모마일 티" +
        "불꽃 시나몬 에스프레소" +
        "달빛 라벤더 핫초코" +
        "안개 속 민트 모카" +
        "고요한 오후의 말차 라떼" +
        "오늘 당신의 하루는 반짝반짝 빛나고 있어요!" +
        "가끔은 눈물도 좋은 양념이 되어요. 괜찮아요." +
        "뜨거운 에너지가 가득한 하루! 그 열정을 응원해요." +
        "포근한 꿈에 빠질 시간. 오늘도 수고했어요." +
        "용기는 두려움을 넘는 거예요. 당신은 이미 충분히 용감해요." +
        "평온한 당신의 하루가 주변을 따뜻하게 해요." +
        "기쁨슬픔분노졸림두려움평온" +
        "한 꼬집두 방울스푼아직 재료가 없어요" +
        "시도회성공마리0123456789 /+...";

    public static TMP_FontAsset LoadOrCreate()
    {
        // Changed: 기존 TMP_FontAsset이 있으면 재사용하고, 없으면 NotoSansKR TTF에서 생성한다.
        // Why: Build Main Scene을 반복해도 같은 에셋 경로가 유지되어 씬 직렬화 참조가 안정적으로 남아야 한다.
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (existing != null)
        {
            WarnIfSourceFontMissing("기존 TMP Font Asset을 재사용합니다.");
            WarmKoreanCharacters(existing);
            PersistSubAssets(existing);
            AssetDatabase.SaveAssets();
            return existing;
        }

        if (!File.Exists(FontAssetPath))
            return CreateFontAsset();

        Debug.LogWarning($"[CatchYourMood] Korean TMP Font Asset 생성 실패: '{FontAssetPath}' 경로에 TMP_FontAsset이 아닌 에셋 파일이 이미 있습니다.");
        return null;
    }

    private static TMP_FontAsset CreateFontAsset()
    {
        // Changed: NotoSansKR 원본 TTF가 없으면 생성 대신 명확한 Warning을 남긴다.
        // Why: 폰트 파일 누락을 조용히 영어 fallback으로 숨기지 않고 빌드 단계에서 발견하기 위함.
        if (!File.Exists(SourceFontPath))
        {
            Debug.LogWarning($"[CatchYourMood] Korean TMP Font Asset 생성 실패: 원본 폰트 파일이 없습니다. 경로: {SourceFontPath}");
            return null;
        }

        try
        {
            AssetDatabase.ImportAsset(SourceFontPath, ImportAssetOptions.ForceSynchronousImport);
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                Debug.LogWarning($"[CatchYourMood] Korean TMP Font Asset 생성 실패: Unity가 원본 폰트를 Font 에셋으로 로드하지 못했습니다. 경로: {SourceFontPath}");
                return null;
            }

            string fontAssetDirectory = Path.GetDirectoryName(FontAssetPath);
            if (!string.IsNullOrEmpty(fontAssetDirectory) && !Directory.Exists(fontAssetDirectory))
                Directory.CreateDirectory(fontAssetDirectory);

            // Changed: 동적 multi-atlas SDF TMP_FontAsset을 생성한다.
            // Why: 결과지 문구는 씬에 직렬화된 FontAsset을 기본으로 쓰고, 추가 한글 글리프도 런타임에 같은 NotoSansKR에서 보충할 수 있어야 한다.
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                SamplingPointSize,
                AtlasPadding,
                GlyphRenderMode.SDFAA,
                AtlasSize,
                AtlasSize,
                AtlasPopulationMode.Dynamic,
                true);

            if (fontAsset == null)
            {
                Debug.LogWarning($"[CatchYourMood] Korean TMP Font Asset 생성 실패: TMP_FontAsset.CreateFontAsset이 null을 반환했습니다. Font Import Settings의 Include Font Data를 확인하세요. 경로: {SourceFontPath}");
                return null;
            }

            fontAsset.name = Path.GetFileNameWithoutExtension(FontAssetPath);
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
            PersistSubAssets(fontAsset);
            WarmKoreanCharacters(fontAsset);
            PersistSubAssets(fontAsset);

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceUpdate);

            TMP_FontAsset savedFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            return savedFontAsset != null ? savedFontAsset : fontAsset;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CatchYourMood] Korean TMP Font Asset 생성 실패: {ex.Message}");
            return null;
        }
    }

    private static void WarmKoreanCharacters(TMP_FontAsset fontAsset)
    {
        // Changed: 생성/로드된 FontAsset에 결과지 문구 글리프를 요청하고 누락 문자를 Warning으로 노출한다.
        // Why: 에셋은 있어도 정적/손상 상태라 한글을 렌더링하지 못하는 경우를 Build Main Scene 단계에서 확인하기 위함.
        if (fontAsset == null) return;

        bool hasCharacters = fontAsset.HasCharacters(
            KoreanResultCharacters,
            out uint[] missingCharacters,
            false,
            fontAsset.atlasPopulationMode != AtlasPopulationMode.Static);

        if (!hasCharacters)
        {
            Debug.LogWarning($"[CatchYourMood] Korean TMP Font Asset에 결과지 글리프 일부가 없습니다. Missing: {FormatMissingCharacters(missingCharacters)} / Asset: {FontAssetPath}");
        }

        EditorUtility.SetDirty(fontAsset);
    }

    private static void PersistSubAssets(TMP_FontAsset fontAsset)
    {
        // Changed: TMP FontAsset이 만든 atlas texture/material을 같은 .asset의 sub-asset으로 저장한다.
        // Why: Quest 빌드에서 씬의 TMP_FontAsset 참조만 남고 atlas/material 리소스가 유실되는 상황을 피하기 위함.
        if (fontAsset == null) return;

        if (fontAsset.material != null && !AssetDatabase.Contains(fontAsset.material))
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

        Texture2D[] atlasTextures = fontAsset.atlasTextures;
        if (atlasTextures == null) return;

        foreach (Texture2D atlasTexture in atlasTextures)
        {
            if (atlasTexture != null && !AssetDatabase.Contains(atlasTexture))
                AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
        }
    }

    private static void WarnIfSourceFontMissing(string context)
    {
        // Changed: TMP_FontAsset이 이미 있어도 원본 TTF 누락을 Warning으로 남긴다.
        // Why: 동적 글리프 추가가 필요한 경우 원본 폰트 파일 누락이 런타임 한글 깨짐으로 이어질 수 있기 때문.
        if (!File.Exists(SourceFontPath))
            Debug.LogWarning($"[CatchYourMood] Korean TMP Font Asset 원본 폰트 파일이 없습니다. {context} 경로: {SourceFontPath}");
    }

    private static string FormatMissingCharacters(uint[] missingCharacters)
    {
        // Changed: 누락 글리프 목록을 사람이 읽을 수 있는 짧은 문자열로 변환한다.
        // Why: Warning만 보고 어떤 문자가 빠졌는지 바로 확인할 수 있어야 한다.
        if (missingCharacters == null || missingCharacters.Length == 0)
            return "(none)";

        const int maxCharacters = 24;
        var builder = new StringBuilder();
        int count = Math.Min(missingCharacters.Length, maxCharacters);
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
}
