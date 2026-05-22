using System;
using System.Collections;
using System.Collections.Generic;
using TMPro; // Changed: World-space status text uses TMP. Why: SDF text stays readable in the room/VR view.
using UnityEngine;

/// <summary>
/// Scoreboard sticker collection display.
/// GameResultManager.OnCatch remains the data source; stickers are placed across one bounded display area.
/// </summary>
public class ScoreboardUI : MonoBehaviour
{
    // Changed: Kept as serialized legacy cleanup references. Why: Existing scenes may still contain slot roots, but placement no longer uses them.
    [Header("Legacy Emotion Slots (cleanup only)")]
    public GameObject[] slotRoots = new GameObject[EmotionSlotCount];

    // Changed: SceneSetup may assign the root for the free-position sticker display. Why: New catches should use the whole board area, not emotion slots.
    [Header("Sticker Display (optional SceneSetup refs)")]
    public Transform stickerAreaRoot;

    // Changed: SceneSetup assigns the tries text directly. Why: Preserve the existing GameResultManager.OnTry display path.
    [Header("Score Text (optional SceneSetup refs)")]
    public TMP_Text totalTriesText;
    public TMP_Text countRowText;
    public TMP_Text overflowSummaryText;

    // Changed: Kept as a serialized compatibility field but no longer used. Why: Stickers are procedural circular emotion badges, not doll texture fragments.
    [Header("Legacy Doll Textures (unused)")]
    public Texture2D[] dollTextures = new Texture2D[EmotionSlotCount];

    // Changed: Fixed six-emotion order shared with GameResultManager counts. Why: Count rows and rebuilds need stable deterministic ordering.
    private static readonly EmotionType[] EmotionOrder =
    {
        EmotionType.Happy, EmotionType.Angry, EmotionType.Sleepy,
        EmotionType.Sad,   EmotionType.Scared, EmotionType.Serene
    };

    // Changed: Emotion colors are used by each circular sticker. Why: Stickers must stay readable without slot labels beside each catch.
    private static readonly Dictionary<EmotionType, Color> EmotionColors = new Dictionary<EmotionType, Color>
    {
        { EmotionType.Happy,  HexColor("FFD93D") },
        { EmotionType.Angry,  HexColor("FF5A4D") },
        { EmotionType.Sleepy, HexColor("C3AED6") },
        { EmotionType.Sad,    HexColor("74B9FF") },
        { EmotionType.Scared, HexColor("A29BFE") },
        { EmotionType.Serene, HexColor("55EFC4") },
    };

    // Changed: Sticker capacity is a fixed 6x4 grid. Why: Cell spacing guarantees non-overlap and bounds Quest object count.
    private const int EmotionSlotCount = 6;
    private const int StickerColumns = 6;
    private const int StickerRows = 4;
    private const int VisibleStickerCapacity = StickerColumns * StickerRows;
    private const int PlacementSeed = 260522;

    // Changed: Display bounds are smaller than the rejected cork board, with the sticker area raised slightly. Why: The board stays compact while leaving a readable bottom text band.
    private const float DisplayWidth = 1.04f;
    private const float DisplayHeight = 0.52f;
    private const float StickerAreaY = 0.065f;
    private const float StickerAreaZ = 0.060f;
    private const float BackdropWidth = 1.18f;
    private const float BackdropHeight = 0.76f;
    private const float StickerRadius = 0.050f;
    private const float CellJitterX = 0.018f;
    private const float CellJitterY = 0.012f;
    private const float BoardVisualZ = 0.000f;
    private const float BoardTextZ = 0.018f;
    private const float TmpWorldScale = 0.005f;
    private const float StickerPopDuration = 0.22f;
    private const int CircleSegments = 48;
    private const int ArcSegments = 24;

    // Changed: Shared text layout separates header, count summary, and overflow chip. Why: The previous bottom stack overlapped at room/VR scale.
    private static readonly Vector3 TitleTextPosition = new Vector3(-0.075f, 0.320f, 0.065f);
    private static readonly Vector3 TriesTextPosition = new Vector3(0.325f, 0.320f, 0.065f);
    private static readonly Vector3 CountRowTextPosition = new Vector3(-0.155f, -0.282f, 0.065f);
    private static readonly Vector3 OverflowTextPosition = new Vector3(0.410f, -0.345f, 0.065f);
    private static readonly Vector2 TitleTextSize = new Vector2(200f, 42f);
    private static readonly Vector2 TriesTextSize = new Vector2(100f, 24f);
    private static readonly Vector2 CountRowTextSize = new Vector2(180f, 36f);
    private static readonly Vector2 OverflowTextSize = new Vector2(82f, 22f);
    private const float TitleTextFontSize = 54f;
    private const float TriesTextFontSize = 24f;
    private const float CountRowTextFontSize = 20f;
    private const float OverflowTextFontSize = 21f;

    // Changed: Runtime state separates total counts from visible sticker capacity. Why: Once full, counts still increase through overflow text.
    private readonly int[] displayedEmotionCounts = new int[EmotionSlotCount];
    private readonly int[] overflowEmotionCounts = new int[EmotionSlotCount];
    private readonly Vector3[] placementPositions = new Vector3[VisibleStickerCapacity];
    private readonly float[] placementRotations = new float[VisibleStickerCapacity];
    private readonly GameObject[] stickerRoots = new GameObject[VisibleStickerCapacity];
    private readonly Coroutine[] stickerPopCoroutines = new Coroutine[VisibleStickerCapacity];
    private int visibleStickerCount;

    // Changed: Runtime materials and meshes are tracked for cleanup. Why: Procedural sticker geometry should not leak Unity objects.
    private readonly Material[] badgeFaceMaterials = new Material[EmotionSlotCount];
    private readonly Material[] badgeRimMaterials = new Material[EmotionSlotCount];
    private readonly Material[] badgeHighlightMaterials = new Material[EmotionSlotCount];
    private readonly List<Material> runtimeMaterials = new List<Material>();
    private readonly List<Mesh> runtimeMeshes = new List<Mesh>();

    private Material inkMaterial;
    private Material creamMaterial;
    private Material whiteMaterial;
    private Material shadowMaterial;
    private Material tearMaterial;
    private Material backdropMaterial;

    private GameResultManager subscribedManager;
    private Coroutine waitForManagerCoroutine;

    private void Start()
    {
        // Changed: Build the compact display before event subscription. Why: Catch callbacks can immediately place a non-overlapping sticker.
        DisableLegacyScoreboardVisuals();
        EnsureSharedMaterials();
        EnsureDisplayObjects();
        BuildPlacementGrid();
        EnsureStickerPool();
        NormalizeBoardTexts();
        HideResultOnlyTextsUntilResultMode();
        SubscribeEvents();
    }

    private void OnDestroy()
    {
        // Changed: Stop delayed subscription and sticker animations. Why: Prevent callbacks into destroyed board instances.
        if (waitForManagerCoroutine != null)
            StopCoroutine(waitForManagerCoroutine);

        for (int i = 0; i < stickerPopCoroutines.Length; i++)
        {
            if (stickerPopCoroutines[i] != null)
                StopCoroutine(stickerPopCoroutines[i]);
        }

        UnsubscribeEvents();
        DestroyRuntimeObjects();
    }

    private void DisableLegacyScoreboardVisuals()
    {
        // Changed: Deactivate old cork/pin/slot visuals at runtime. Why: Existing serialized scenes may still contain rejected clutter before rebuilding.
        DisableLegacyChildrenRecursive(transform);

        if (slotRoots == null)
            return;

        for (int i = 0; i < slotRoots.Length; i++)
        {
            if (slotRoots[i] != null)
                slotRoots[i].SetActive(false);
        }
    }

    private void DisableLegacyChildrenRecursive(Transform root)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (IsLegacyScoreboardVisual(child.name))
            {
                child.gameObject.SetActive(false);
                continue;
            }

            DisableLegacyChildrenRecursive(child);
        }
    }

    private static bool IsLegacyScoreboardVisual(string childName)
    {
        // Changed: Centralized legacy name filter. Why: Pins, placeholders, old stickers, ghost rings, and the large panel must not remain visible.
        return childName == "Frame" ||
               childName == "Panel" ||
               childName == "Pin" ||
               childName == "Placeholder" ||
               childName == "BadgeGhost" ||
               childName == "CountText" ||
               childName == "OverflowChip" ||
               childName == "OverflowText" ||
               childName.StartsWith("Slot_", StringComparison.Ordinal) ||
               childName.StartsWith("Sticker_", StringComparison.Ordinal) ||
               childName.StartsWith("Badge_", StringComparison.Ordinal) ||
               childName.StartsWith("MiniDoll_", StringComparison.Ordinal);
    }

    private void EnsureDisplayObjects()
    {
        // Changed: Missing SceneSetup refs are created at runtime. Why: The checked-in scene may not have been rebuilt yet.
        if (stickerAreaRoot == null)
        {
            Transform existing = transform.Find("StickerArea");
            stickerAreaRoot = existing != null ? existing : new GameObject("StickerArea").transform;
        }

        stickerAreaRoot.SetParent(transform, false);
        stickerAreaRoot.localPosition = new Vector3(0f, StickerAreaY, StickerAreaZ);
        stickerAreaRoot.localRotation = Quaternion.identity;
        stickerAreaRoot.localScale = Vector3.one;

        EnsureBackdrop();

        if (countRowText == null)
            countRowText = CreateTMPText("EmotionCounts", transform, string.Empty, CountRowTextPosition, CountRowTextFontSize, HexColor("E7E4DC"), FontStyles.Bold, CountRowTextSize, TextAlignmentOptions.Center);

        if (overflowSummaryText == null)
            overflowSummaryText = CreateTMPText("OverflowSummary", transform, string.Empty, OverflowTextPosition, OverflowTextFontSize, HexColor("FFDFA8"), FontStyles.Bold, OverflowTextSize, TextAlignmentOptions.Right);
    }

    private void EnsureBackdrop()
    {
        // Changed: Create a small neutral backing only if SceneSetup did not serialize one. Why: No ochre/cork board should remain necessary.
        Transform existing = transform.Find("StickerBackdrop");
        if (existing != null)
            return;

        GameObject backdrop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        backdrop.name = "StickerBackdrop";
        backdrop.transform.SetParent(transform, false);
        backdrop.transform.localPosition = new Vector3(0f, 0f, 0.036f);
        backdrop.transform.localRotation = Quaternion.identity;
        backdrop.transform.localScale = new Vector3(BackdropWidth, BackdropHeight, 0.012f);
        backdrop.GetComponent<Renderer>().sharedMaterial = backdropMaterial;

        Collider collider = backdrop.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);
    }

    private void NormalizeBoardTexts()
    {
        // Changed: Normalize old serialized title/tries transforms into the smaller display envelope. Why: The current scene may still have large-board coordinates.
        Transform titleTf = transform.Find("Title");
        if (titleTf != null)
        {
            TMP_Text title = titleTf.GetComponent<TMP_Text>();
            if (title != null)
            {
                title.text = "Caught moods";
                title.fontSize = TitleTextFontSize;
                title.fontStyle = FontStyles.Bold;
                title.alignment = TextAlignmentOptions.Center;
                title.color = HexColor("F1F0EA");
                title.textWrappingMode = TextWrappingModes.NoWrap;
                title.overflowMode = TextOverflowModes.Overflow;
            }

            titleTf.localPosition = TitleTextPosition;
            titleTf.localRotation = Quaternion.Euler(0f, 180f, 0f);
            titleTf.localScale = Vector3.one * TmpWorldScale;

            RectTransform rt = titleTf.GetComponent<RectTransform>();
            if (rt != null)
                rt.sizeDelta = TitleTextSize;
        }

        // Changed: TotalTries is normalized into the header and overflow into a compact bottom-right chip. Why: Counts, overflow, and tries must not compete for the same bottom space.
        PrepareStatusText(totalTriesText, TriesTextPosition, TriesTextFontSize, HexColor("D4D7D5"), TriesTextSize, TextAlignmentOptions.Right);
        PrepareStatusText(countRowText, CountRowTextPosition, CountRowTextFontSize, HexColor("E7E4DC"), CountRowTextSize, TextAlignmentOptions.Center);
        PrepareStatusText(overflowSummaryText, OverflowTextPosition, OverflowTextFontSize, HexColor("FFDFA8"), OverflowTextSize, TextAlignmentOptions.Right);
    }

    private void PrepareStatusText(TMP_Text text, Vector3 localPosition, float fontSize, Color color, Vector2 sizeDelta, TextAlignmentOptions alignment)
    {
        // Changed: Shared TMP positioning now includes alignment. Why: Header/right-chip text needs horizontal separation from the count summary.
        if (text == null)
            return;

        Transform textTransform = text.transform;
        textTransform.localPosition = localPosition;
        textTransform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        textTransform.localScale = Vector3.one * TmpWorldScale;

        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;

        RectTransform rt = text.GetComponent<RectTransform>();
        if (rt != null)
            rt.sizeDelta = sizeDelta;
    }

    private void HideResultOnlyTextsUntilResultMode()
    {
        // Changed: Result-only TMP objects are inactive during normal accumulation. Why: Prevent stale result text from cluttering the sticker display.
        Transform detail = transform.Find("ResultDetail");
        if (detail != null)
            detail.gameObject.SetActive(false);

        Transform stats = transform.Find("ResultStats");
        if (stats != null)
            stats.gameObject.SetActive(false);
    }

    private void BuildPlacementGrid()
    {
        // Changed: Use a deterministic shuffled cell grid. Why: Positions look random, stay stable, remain inside bounds, and cannot overlap.
        List<int> order = new List<int>(VisibleStickerCapacity);
        List<Vector3> basePositions = new List<Vector3>(VisibleStickerCapacity);
        List<float> baseRotations = new List<float>(VisibleStickerCapacity);

        float cellWidth = DisplayWidth / StickerColumns;
        float cellHeight = DisplayHeight / StickerRows;
        for (int row = 0; row < StickerRows; row++)
        {
            for (int col = 0; col < StickerColumns; col++)
            {
                int cellIndex = row * StickerColumns + col;
                float x = -DisplayWidth * 0.5f + cellWidth * (col + 0.5f) + DeterministicSigned(cellIndex, 11) * CellJitterX;
                float y = DisplayHeight * 0.5f - cellHeight * (row + 0.5f) + DeterministicSigned(cellIndex, 29) * CellJitterY;
                basePositions.Add(new Vector3(x, y, 0f));
                baseRotations.Add(DeterministicSigned(cellIndex, 47) * 13f);
                order.Add(cellIndex);
            }
        }

        System.Random random = new System.Random(PlacementSeed);
        for (int i = order.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            int temp = order[i];
            order[i] = order[swapIndex];
            order[swapIndex] = temp;
        }

        for (int i = 0; i < VisibleStickerCapacity; i++)
        {
            int sourceIndex = order[i];
            placementPositions[i] = basePositions[sourceIndex];
            placementRotations[i] = baseRotations[sourceIndex];
        }
    }

    private static float DeterministicSigned(int index, int salt)
    {
        // Changed: Deterministic jitter avoids UnityEngine.Random state. Why: Sticker layout should be reproducible across play sessions.
        float value = Mathf.Sin((index + 1) * 12.9898f + salt * 78.233f) * 43758.5453f;
        return (value - Mathf.Floor(value)) * 2f - 1f;
    }

    private void EnsureStickerPool()
    {
        // Changed: Pre-create only root objects; badge meshes are attached as catches arrive. Why: Capacity is bounded while initial scene stays light.
        for (int i = 0; i < VisibleStickerCapacity; i++)
        {
            if (stickerRoots[i] != null)
                continue;

            GameObject sticker = new GameObject($"Sticker_{i + 1:00}");
            sticker.transform.SetParent(stickerAreaRoot, false);
            sticker.SetActive(false);
            stickerRoots[i] = sticker;
        }
    }

    private void SubscribeEvents()
    {
        // Changed: Subscribe to the existing GameResultManager events only. Why: Preserve the catch/try data flow.
        if (GameResultManager.Instance != null)
        {
            AttachToManager(GameResultManager.Instance);
            return;
        }

        waitForManagerCoroutine = StartCoroutine(WaitForGameResultManager());
    }

    private IEnumerator WaitForGameResultManager()
    {
        // Changed: Wait for scene initialization order without permanent polling. Why: Some scene builds create managers after UI components.
        while (GameResultManager.Instance == null)
            yield return null;

        AttachToManager(GameResultManager.Instance);
        waitForManagerCoroutine = null;
    }

    private void AttachToManager(GameResultManager manager)
    {
        if (manager == null || subscribedManager == manager)
            return;

        // Changed: Detach a previous manager before subscribing to a new one. Why: Rebuilt scenes must not receive duplicate callbacks.
        UnsubscribeEvents();

        subscribedManager = manager;
        subscribedManager.OnCatch += HandleCatch;
        subscribedManager.OnTry += HandleTry;

        RebuildFromCurrentResults(false);
        HandleTry(subscribedManager.GetTryCount());
    }

    private void UnsubscribeEvents()
    {
        // Changed: Track the exact manager instance used for subscription. Why: GameResultManager.Instance can change during scene rebuilds.
        if (subscribedManager == null)
            return;

        subscribedManager.OnCatch -= HandleCatch;
        subscribedManager.OnTry -= HandleTry;
        subscribedManager = null;
    }

    private void HandleCatch(EmotionType emotion)
    {
        int idx = Array.IndexOf(EmotionOrder, emotion);
        if (idx < 0 || idx >= EmotionSlotCount || subscribedManager == null)
            return;

        int managerCount = Mathf.Max(0, subscribedManager.GetEmotionCount(emotion));
        int missingCount = managerCount - displayedEmotionCounts[idx];
        if (missingCount <= 0)
        {
            RebuildFromCurrentResults(false);
            return;
        }

        for (int i = 0; i < missingCount; i++)
            AddSticker(emotion, idx, true);
    }

    private void HandleTry(int totalTries)
    {
        // Changed: Only the display text changes; GameResultManager remains the source of truth.
        if (totalTriesText != null)
            totalTriesText.text = $"Tries: {totalTries}";
    }

    private void RebuildFromCurrentResults(bool animate)
    {
        // Changed: Reconstruct a deterministic visible board from current counts. Why: Counts may exist before ScoreboardUI subscribes.
        if (subscribedManager == null)
            return;

        ClearStickerState();

        for (int i = 0; i < EmotionSlotCount; i++)
        {
            EmotionType emotion = EmotionOrder[i];
            int count = Mathf.Max(0, subscribedManager.GetEmotionCount(emotion));
            for (int n = 0; n < count; n++)
                AddSticker(emotion, i, animate);
        }
    }

    private void ClearStickerState()
    {
        // Changed: Clear only runtime sticker roots and counters. Why: Result text and manager data remain owned by other modules.
        visibleStickerCount = 0;
        Array.Clear(displayedEmotionCounts, 0, displayedEmotionCounts.Length);
        Array.Clear(overflowEmotionCounts, 0, overflowEmotionCounts.Length);

        for (int i = 0; i < stickerRoots.Length; i++)
        {
            if (stickerPopCoroutines[i] != null)
            {
                StopCoroutine(stickerPopCoroutines[i]);
                stickerPopCoroutines[i] = null;
            }

            if (stickerRoots[i] == null)
                continue;

            ClearStickerRoot(stickerRoots[i].transform);
            stickerRoots[i].SetActive(false);
            stickerRoots[i].transform.localScale = Vector3.one;
        }

        UpdateCountRowText();
        UpdateOverflowSummaryText();
    }

    private void AddSticker(EmotionType emotion, int emotionIndex, bool animate)
    {
        // Changed: Each catch consumes the next shuffled free cell. Why: Stickers appear across the whole display without overlapping.
        displayedEmotionCounts[emotionIndex]++;

        if (visibleStickerCount >= VisibleStickerCapacity)
        {
            overflowEmotionCounts[emotionIndex]++;
            UpdateCountRowText();
            UpdateOverflowSummaryText();
            return;
        }

        int stickerIndex = visibleStickerCount;
        visibleStickerCount++;
        SetStickerVisual(stickerIndex, emotion, emotionIndex);
        UpdateCountRowText();
        UpdateOverflowSummaryText();

        if (animate)
            PlayStickerPop(stickerIndex);
    }

    private void SetStickerVisual(int stickerIndex, EmotionType emotion, int emotionIndex)
    {
        GameObject sticker = stickerRoots[stickerIndex];
        if (sticker == null)
            return;

        // Changed: Rebuild the pooled root for the requested emotion. Why: Reconstructed boards may assign a different emotion to the same free cell.
        ClearStickerRoot(sticker.transform);
        EnsureEmotionMaterials(emotionIndex, emotion);

        sticker.transform.localPosition = placementPositions[stickerIndex];
        sticker.transform.localRotation = Quaternion.Euler(0f, 0f, placementRotations[stickerIndex]);
        sticker.transform.localScale = Vector3.one;
        BuildBadgeVisual(emotionIndex, sticker.transform, emotion);
        sticker.SetActive(true);
    }

    private void ClearStickerRoot(Transform root)
    {
        // Changed: Remove only procedural children below a pooled sticker root. Why: The pool object keeps stable references and bounded count.
        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);
    }

    private void UpdateCountRowText()
    {
        // Changed: Two short count lines stay in the lower-left summary area. Why: Full labels remain readable without colliding with tries or overflow.
        if (countRowText == null)
            return;

        countRowText.text =
            $"Happy {displayedEmotionCounts[0]}   Angry {displayedEmotionCounts[1]}   Sleepy {displayedEmotionCounts[2]}\n" +
            $"Sad {displayedEmotionCounts[3]}   Scared {displayedEmotionCounts[4]}   Serene {displayedEmotionCounts[5]}";
    }

    private void UpdateOverflowSummaryText()
    {
        // Changed: Overflow is explicit once all cells are filled. Why: Later catches must still produce readable feedback with bounded stickers.
        if (overflowSummaryText == null)
            return;

        int overflowTotal = 0;
        for (int i = 0; i < overflowEmotionCounts.Length; i++)
            overflowTotal += overflowEmotionCounts[i];

        // Changed: Use a compact overflow chip label. Why: After 24 visible catches, feedback stays readable in the bottom-right corner.
        overflowSummaryText.text = overflowTotal > 0 ? $"Full +{overflowTotal}" : string.Empty;
    }

    private void EnsureSharedMaterials()
    {
        // Changed: Shared materials are reused by all sticker geometry. Why: The visible pool is bounded in object and material count.
        if (inkMaterial != null)
            return;

        inkMaterial = CreateRuntimeMaterial("StickerInk", HexColor("332D2A"));
        creamMaterial = CreateRuntimeMaterial("StickerCream", HexColor("FFF7DF"));
        whiteMaterial = CreateRuntimeMaterial("StickerWhite", HexColor("FFFFFF"));
        shadowMaterial = CreateRuntimeMaterial("StickerShadow", new Color(0.04f, 0.05f, 0.05f, 0.24f));
        tearMaterial = CreateRuntimeMaterial("StickerTear", HexColor("EAF8FF"));
        backdropMaterial = CreateRuntimeMaterial("StickerBackdropNeutral", new Color(0.15f, 0.17f, 0.18f, 0.78f));
    }

    private void EnsureEmotionMaterials(int idx, EmotionType emotion)
    {
        // Changed: Each emotion gets a face/rim/highlight material set. Why: Palette stays exact while icons share common ink/cream assets.
        if (badgeFaceMaterials[idx] != null)
            return;

        Color baseColor = EmotionColors[emotion];
        badgeFaceMaterials[idx] = CreateRuntimeMaterial($"StickerFace_{emotion}", baseColor);
        badgeRimMaterials[idx] = CreateRuntimeMaterial($"StickerRim_{emotion}", Color.Lerp(baseColor, Color.white, 0.45f));
        badgeHighlightMaterials[idx] = CreateRuntimeMaterial($"StickerHighlight_{emotion}", new Color(1f, 1f, 1f, 0.22f));
    }

    private void BuildBadgeVisual(int idx, Transform parent, EmotionType emotion)
    {
        // Changed: Common layered circle gives every token a readable sticker silhouette. Why: Designed badges need consistent size and depth.
        CreateCircle("BadgeShadow", parent, new Vector3(0.005f, -0.006f, BoardVisualZ), StickerRadius * 1.05f, shadowMaterial);
        CreateCircle("BadgeFace", parent, Vector3.forward * (BoardVisualZ + 0.002f), StickerRadius, badgeFaceMaterials[idx]);
        CreateCircle("BadgeHighlight", parent, new Vector3(-0.011f, 0.013f, BoardVisualZ + 0.003f), StickerRadius * 0.58f, badgeHighlightMaterials[idx]);
        CreateRing("BadgeRim", parent, Vector3.forward * (BoardVisualZ + 0.004f), StickerRadius * 1.01f, StickerRadius * 0.88f, badgeRimMaterials[idx]);

        switch (emotion)
        {
            case EmotionType.Happy:
                BuildHappyIcon(parent);
                break;
            case EmotionType.Angry:
                BuildAngryIcon(parent);
                break;
            case EmotionType.Sleepy:
                BuildSleepyIcon(idx, parent);
                break;
            case EmotionType.Sad:
                BuildSadIcon(parent);
                break;
            case EmotionType.Scared:
                BuildScaredIcon(parent);
                break;
            case EmotionType.Serene:
                BuildSereneIcon(parent);
                break;
        }
    }

    private void BuildHappyIcon(Transform parent)
    {
        // Changed: Happy icon is a smiling sun. Why: It reads as a designed emotion mark from the room view.
        CreateSunRays("SunRays", parent, new Vector3(0f, 0.003f, BoardVisualZ + 0.006f), 0.029f, 0.043f, creamMaterial);
        CreateCircle("EyeLeft", parent, new Vector3(-0.014f, 0.010f, BoardVisualZ + 0.008f), 0.0048f, inkMaterial);
        CreateCircle("EyeRight", parent, new Vector3(0.014f, 0.010f, BoardVisualZ + 0.008f), 0.0048f, inkMaterial);
        CreateArc("Smile", parent, new Vector3(0f, -0.002f, BoardVisualZ + 0.008f), 0.021f, 0.0049f, 205f, 335f, inkMaterial);
    }

    private void BuildAngryIcon(Transform parent)
    {
        // Changed: Angry icon uses a lightning/flame mark plus brow bars. Why: It avoids stale doll fragments while keeping the emotion obvious.
        CreateLightning("LightningFlame", parent, new Vector3(0f, -0.004f, BoardVisualZ + 0.007f), creamMaterial);
        CreateRect("BrowLeft", parent, new Vector3(-0.015f, 0.018f, BoardVisualZ + 0.009f), 0.024f, 0.0055f, -18f, inkMaterial);
        CreateRect("BrowRight", parent, new Vector3(0.015f, 0.018f, BoardVisualZ + 0.009f), 0.024f, 0.0055f, 18f, inkMaterial);
    }

    private void BuildSleepyIcon(int idx, Transform parent)
    {
        // Changed: Sleepy icon is a crescent moon plus Z label. Why: Simple geometry stays readable on the circular sticker.
        CreateCircle("MoonBase", parent, new Vector3(-0.012f, 0.004f, BoardVisualZ + 0.007f), 0.025f, creamMaterial);
        CreateCircle("MoonCutout", parent, new Vector3(0.001f, 0.008f, BoardVisualZ + 0.008f), 0.025f, badgeFaceMaterials[idx]);
        CreateTMPText("SleepZ", parent, "Z", new Vector3(0.025f, 0.018f, BoardTextZ + 0.004f), 46f, HexColor("FFF7DF"), FontStyles.Bold, new Vector2(28f, 28f));
    }

    private void BuildSadIcon(Transform parent)
    {
        // Changed: Sad icon uses downturned mouth and tear geometry. Why: The display no longer depends on full doll imagery.
        CreateCircle("EyeLeft", parent, new Vector3(-0.015f, 0.011f, BoardVisualZ + 0.008f), 0.0043f, inkMaterial);
        CreateCircle("EyeRight", parent, new Vector3(0.012f, 0.011f, BoardVisualZ + 0.008f), 0.0043f, inkMaterial);
        CreateArc("DownMouth", parent, new Vector3(-0.002f, -0.014f, BoardVisualZ + 0.008f), 0.019f, 0.0045f, 25f, 155f, inkMaterial);
        CreateTear("Tear", parent, new Vector3(0.024f, -0.006f, BoardVisualZ + 0.009f), tearMaterial);
    }

    private void BuildScaredIcon(Transform parent)
    {
        // Changed: Scared icon uses wide eyes and an exclamation mark. Why: The sticker reads clearly without small texture details.
        CreateCircle("EyeLeftWhite", parent, new Vector3(-0.016f, 0.011f, BoardVisualZ + 0.007f), 0.012f, whiteMaterial);
        CreateCircle("EyeRightWhite", parent, new Vector3(0.016f, 0.011f, BoardVisualZ + 0.007f), 0.012f, whiteMaterial);
        CreateCircle("PupilLeft", parent, new Vector3(-0.016f, 0.011f, BoardVisualZ + 0.009f), 0.0048f, inkMaterial);
        CreateCircle("PupilRight", parent, new Vector3(0.016f, 0.011f, BoardVisualZ + 0.009f), 0.0048f, inkMaterial);
        CreateRect("ExclaimBar", parent, new Vector3(0f, -0.012f, BoardVisualZ + 0.009f), 0.006f, 0.020f, 0f, creamMaterial);
        CreateCircle("ExclaimDot", parent, new Vector3(0f, -0.028f, BoardVisualZ + 0.009f), 0.0045f, creamMaterial);
    }

    private void BuildSereneIcon(Transform parent)
    {
        // Changed: Serene icon uses a closed-eye curve and leaf/wave accents. Why: It distinguishes calm from sleepy at badge scale.
        CreateArc("ClosedEye", parent, new Vector3(0f, 0.009f, BoardVisualZ + 0.008f), 0.023f, 0.0042f, 205f, 335f, inkMaterial);
        CreateLeaf("Leaf", parent, new Vector3(0.018f, -0.010f, BoardVisualZ + 0.009f), creamMaterial);
        CreateArc("Wave", parent, new Vector3(-0.015f, -0.019f, BoardVisualZ + 0.008f), 0.019f, 0.0038f, 20f, 155f, creamMaterial);
    }

    private TMP_Text CreateTMPText(string name, Transform parent, string text, Vector3 localPosition, float fontSize, Color color, FontStyles style, Vector2 sizeDelta, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        // Changed: TMP creation centralizes scale, z, rotation, and alignment. Why: Runtime fallback text must match SceneSetup's separated layout.
        GameObject textGo = new GameObject(name);
        textGo.transform.SetParent(parent, false);
        textGo.transform.localPosition = localPosition;
        textGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        textGo.transform.localScale = Vector3.one * TmpWorldScale;

        TextMeshPro tmp = textGo.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.alignment = alignment;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;

        RectTransform rt = textGo.GetComponent<RectTransform>();
        if (rt != null)
            rt.sizeDelta = sizeDelta;

        return tmp;
    }

    private void PlayStickerPop(int stickerIndex)
    {
        if (stickerIndex < 0 || stickerIndex >= stickerRoots.Length)
            return;

        GameObject sticker = stickerRoots[stickerIndex];
        if (sticker == null)
            return;

        // Changed: Stop the prior animation on the same sticker. Why: Rapid catches should not leave incorrect scale values.
        if (stickerPopCoroutines[stickerIndex] != null)
            StopCoroutine(stickerPopCoroutines[stickerIndex]);

        sticker.transform.localScale = Vector3.zero;
        stickerPopCoroutines[stickerIndex] = StartCoroutine(AnimateScale(
            sticker.transform,
            Vector3.zero,
            Vector3.one,
            StickerPopDuration,
            () => stickerPopCoroutines[stickerIndex] = null));
    }

    private IEnumerator AnimateScale(Transform target, Vector3 from, Vector3 to, float duration, Action onComplete)
    {
        // Changed: Shared pop animation for sticker visuals. Why: Catch feedback should be readable without particles.
        if (target == null)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            target.localScale = Vector3.LerpUnclamped(from, to, EaseOutBack(t));
            yield return null;
        }

        if (target != null)
            target.localScale = to;

        onComplete?.Invoke();
    }

    private void CreateCircle(string name, Transform parent, Vector3 localPosition, float radius, Material material)
    {
        // Changed: Circles use procedural mesh discs. Why: Stickers must be circular geometry, not square quads.
        Mesh mesh = CreateCircleMesh(radius, CircleSegments);
        CreateMeshObject(name, parent, localPosition, Quaternion.identity, mesh, material);
    }

    private void CreateRing(string name, Transform parent, Vector3 localPosition, float outerRadius, float innerRadius, Material material)
    {
        // Changed: Sticker rims use ring meshes. Why: Filled states need circular badge silhouettes.
        Mesh mesh = CreateRingMesh(outerRadius, innerRadius, 0f, 360f, CircleSegments);
        CreateMeshObject(name, parent, localPosition, Quaternion.identity, mesh, material);
    }

    private void CreateArc(string name, Transform parent, Vector3 localPosition, float radius, float thickness, float startDegrees, float endDegrees, Material material)
    {
        // Changed: Face curves are procedural arc meshes. Why: Smile, frown, eye, and wave marks remain crisp at sticker size.
        Mesh mesh = CreateRingMesh(radius + thickness * 0.5f, radius - thickness * 0.5f, startDegrees, endDegrees, ArcSegments);
        CreateMeshObject(name, parent, localPosition, Quaternion.identity, mesh, material);
    }

    private void CreateRect(string name, Transform parent, Vector3 localPosition, float width, float height, float zDegrees, Material material)
    {
        // Changed: Small bars use rectangular meshes. Why: Brow and exclamation marks should be geometry in the sticker layer.
        Mesh mesh = CreateRectMesh(width, height);
        CreateMeshObject(name, parent, localPosition, Quaternion.Euler(0f, 0f, zDegrees), mesh, material);
    }

    private void CreateSunRays(string name, Transform parent, Vector3 localPosition, float innerRadius, float outerRadius, Material material)
    {
        // Changed: Sun rays are one mesh per sticker. Why: It keeps the happy symbol readable without adding many separate objects.
        Mesh mesh = CreateSunRaysMesh(innerRadius, outerRadius, 8);
        CreateMeshObject(name, parent, localPosition, Quaternion.identity, mesh, material);
    }

    private void CreateLightning(string name, Transform parent, Vector3 localPosition, Material material)
    {
        // Changed: Lightning/flame silhouette is custom mesh geometry. Why: Angry sticker needs a strong, non-text symbol.
        Mesh mesh = CreateLightningMesh();
        CreateMeshObject(name, parent, localPosition, Quaternion.identity, mesh, material);
    }

    private void CreateTear(string name, Transform parent, Vector3 localPosition, Material material)
    {
        // Changed: Tear shape is a procedural droplet. Why: Sad sticker should not rely on texture fragments.
        Mesh mesh = CreateTearMesh();
        CreateMeshObject(name, parent, localPosition, Quaternion.identity, mesh, material);
    }

    private void CreateLeaf(string name, Transform parent, Vector3 localPosition, Material material)
    {
        // Changed: Leaf/wave accent is procedural geometry. Why: Serene sticker needs a distinct calm mark.
        Mesh mesh = CreateLeafMesh();
        CreateMeshObject(name, parent, localPosition, Quaternion.Euler(0f, 0f, -25f), mesh, material);
    }

    private GameObject CreateMeshObject(string name, Transform parent, Vector3 localPosition, Quaternion localRotation, Mesh mesh, Material material)
    {
        // Changed: Mesh object creation strips physics and stores the generated mesh. Why: Scoreboard visuals are non-interactive and cleanup-safe.
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;
        go.transform.localScale = Vector3.one;

        MeshFilter filter = go.AddComponent<MeshFilter>();
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        filter.sharedMesh = mesh;
        renderer.sharedMaterial = material;
        return go;
    }

    private Mesh CreateCircleMesh(float radius, int segments)
    {
        // Changed: Filled circular mesh generator. Why: Unity primitives do not provide a flat circular disc without extra colliders.
        Mesh mesh = new Mesh { name = "ScoreboardCircleMesh" };
        Vector3[] vertices = new Vector3[segments + 1];
        int[] triangles = new int[segments * 3];
        vertices[0] = Vector3.zero;

        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
        }

        for (int i = 0; i < segments; i++)
        {
            int next = i == segments - 1 ? 1 : i + 2;
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = next;
            triangles[i * 3 + 2] = i + 1;
        }

        mesh.vertices = vertices;
        mesh.triangles = MakeDoubleSided(triangles);
        mesh.RecalculateBounds();
        runtimeMeshes.Add(mesh);
        return mesh;
    }

    private Mesh CreateRingMesh(float outerRadius, float innerRadius, float startDegrees, float endDegrees, int segments)
    {
        // Changed: Ring/arc mesh generator. Why: Rims, smiles, frowns, and waves all share the same geometry path.
        Mesh mesh = new Mesh { name = "ScoreboardRingMesh" };
        bool fullCircle = Mathf.Abs(endDegrees - startDegrees) >= 359.9f;
        int steps = fullCircle ? segments : Mathf.Max(2, segments);
        int vertexCount = (steps + 1) * 2;
        Vector3[] vertices = new Vector3[vertexCount];
        int[] triangles = new int[steps * 6];

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            float degrees = fullCircle ? startDegrees + 360f * t : Mathf.Lerp(startDegrees, endDegrees, t);
            float radians = degrees * Mathf.Deg2Rad;
            Vector3 outer = new Vector3(Mathf.Cos(radians) * outerRadius, Mathf.Sin(radians) * outerRadius, 0f);
            Vector3 inner = new Vector3(Mathf.Cos(radians) * innerRadius, Mathf.Sin(radians) * innerRadius, 0f);
            int vertexIndex = i * 2;
            vertices[vertexIndex] = outer;
            vertices[vertexIndex + 1] = inner;
        }

        for (int i = 0; i < steps; i++)
        {
            int vertexIndex = i * 2;
            int triangleIndex = i * 6;
            triangles[triangleIndex] = vertexIndex;
            triangles[triangleIndex + 1] = vertexIndex + 2;
            triangles[triangleIndex + 2] = vertexIndex + 1;
            triangles[triangleIndex + 3] = vertexIndex + 2;
            triangles[triangleIndex + 4] = vertexIndex + 3;
            triangles[triangleIndex + 5] = vertexIndex + 1;
        }

        mesh.vertices = vertices;
        mesh.triangles = MakeDoubleSided(triangles);
        mesh.RecalculateBounds();
        runtimeMeshes.Add(mesh);
        return mesh;
    }

    private Mesh CreateRectMesh(float width, float height)
    {
        // Changed: Rectangle mesh generator. Why: Small sticker bars should not instantiate cube primitives or colliders.
        Mesh mesh = new Mesh { name = "ScoreboardRectMesh" };
        float hx = width * 0.5f;
        float hy = height * 0.5f;
        mesh.vertices = new[]
        {
            new Vector3(-hx, -hy, 0f),
            new Vector3(-hx,  hy, 0f),
            new Vector3( hx,  hy, 0f),
            new Vector3( hx, -hy, 0f)
        };
        mesh.triangles = MakeDoubleSided(new[] { 0, 1, 2, 0, 2, 3 });
        mesh.RecalculateBounds();
        runtimeMeshes.Add(mesh);
        return mesh;
    }

    private Mesh CreateSunRaysMesh(float innerRadius, float outerRadius, int rays)
    {
        // Changed: Sun ray mesh generator. Why: Happy sticker rays render as one designed symbol layer.
        Mesh mesh = new Mesh { name = "ScoreboardSunRaysMesh" };
        Vector3[] vertices = new Vector3[rays * 3];
        int[] triangles = new int[rays * 3];

        for (int i = 0; i < rays; i++)
        {
            float center = Mathf.PI * 2f * i / rays;
            float halfWidth = Mathf.PI / rays * 0.32f;
            int baseIndex = i * 3;
            vertices[baseIndex] = new Vector3(Mathf.Cos(center - halfWidth) * innerRadius, Mathf.Sin(center - halfWidth) * innerRadius, 0f);
            vertices[baseIndex + 1] = new Vector3(Mathf.Cos(center) * outerRadius, Mathf.Sin(center) * outerRadius, 0f);
            vertices[baseIndex + 2] = new Vector3(Mathf.Cos(center + halfWidth) * innerRadius, Mathf.Sin(center + halfWidth) * innerRadius, 0f);
            triangles[baseIndex] = baseIndex;
            triangles[baseIndex + 1] = baseIndex + 1;
            triangles[baseIndex + 2] = baseIndex + 2;
        }

        mesh.vertices = vertices;
        mesh.triangles = MakeDoubleSided(triangles);
        mesh.RecalculateBounds();
        runtimeMeshes.Add(mesh);
        return mesh;
    }

    private Mesh CreateLightningMesh()
    {
        // Changed: Lightning mesh generator. Why: The angry/flame symbol needs a bolder silhouette than text.
        Mesh mesh = new Mesh { name = "ScoreboardLightningMesh" };
        mesh.vertices = new[]
        {
            new Vector3(-0.002f,  0.034f, 0f),
            new Vector3( 0.020f,  0.004f, 0f),
            new Vector3( 0.006f,  0.004f, 0f),
            new Vector3( 0.015f, -0.035f, 0f),
            new Vector3(-0.021f, -0.001f, 0f),
            new Vector3(-0.007f, -0.001f, 0f)
        };
        mesh.triangles = MakeDoubleSided(new[] { 0, 1, 2, 0, 2, 5, 5, 2, 3, 5, 3, 4 });
        mesh.RecalculateBounds();
        runtimeMeshes.Add(mesh);
        return mesh;
    }

    private Mesh CreateTearMesh()
    {
        // Changed: Tear mesh generator. Why: Sad sticker tear is a simple readable droplet.
        Mesh mesh = new Mesh { name = "ScoreboardTearMesh" };
        mesh.vertices = new[]
        {
            new Vector3( 0.000f,  0.019f, 0f),
            new Vector3( 0.011f,  0.004f, 0f),
            new Vector3( 0.008f, -0.014f, 0f),
            new Vector3( 0.000f, -0.022f, 0f),
            new Vector3(-0.008f, -0.014f, 0f),
            new Vector3(-0.011f,  0.004f, 0f)
        };
        mesh.triangles = MakeDoubleSided(new[] { 0, 1, 5, 5, 1, 4, 4, 1, 2, 4, 2, 3 });
        mesh.RecalculateBounds();
        runtimeMeshes.Add(mesh);
        return mesh;
    }

    private Mesh CreateLeafMesh()
    {
        // Changed: Leaf mesh generator. Why: Serene sticker needs a small natural symbol with no external assets.
        Mesh mesh = new Mesh { name = "ScoreboardLeafMesh" };
        mesh.vertices = new[]
        {
            new Vector3( 0.000f,  0.019f, 0f),
            new Vector3( 0.014f,  0.008f, 0f),
            new Vector3( 0.016f, -0.005f, 0f),
            new Vector3( 0.000f, -0.018f, 0f),
            new Vector3(-0.016f, -0.005f, 0f),
            new Vector3(-0.014f,  0.008f, 0f)
        };
        mesh.triangles = MakeDoubleSided(new[] { 0, 1, 5, 5, 1, 4, 4, 1, 2, 4, 2, 3 });
        mesh.RecalculateBounds();
        runtimeMeshes.Add(mesh);
        return mesh;
    }

    private static int[] MakeDoubleSided(int[] sourceTriangles)
    {
        // Changed: Duplicate procedural triangles with reverse winding. Why: Sticker meshes remain visible if a fallback shader culls one side.
        int[] doubleSided = new int[sourceTriangles.Length * 2];
        Array.Copy(sourceTriangles, doubleSided, sourceTriangles.Length);

        for (int i = 0; i < sourceTriangles.Length; i += 3)
        {
            int target = sourceTriangles.Length + i;
            doubleSided[target] = sourceTriangles[i];
            doubleSided[target + 1] = sourceTriangles[i + 2];
            doubleSided[target + 2] = sourceTriangles[i + 1];
        }

        return doubleSided;
    }

    private Material CreateRuntimeMaterial(string materialName, Color color)
    {
        // Changed: Create unlit runtime materials with transparent support. Why: Sticker symbols should render consistently across URP/built-in fallbacks.
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null || shader.name == "Hidden/InternalErrorShader")
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null || shader.name == "Hidden/InternalErrorShader")
            shader = Shader.Find("Standard");

        Material material = new Material(shader) { name = materialName };
        SetMaterialColor(material, color);
        ConfigureMaterialForAlpha(material, color.a < 0.99f);

        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", 0f);

        runtimeMaterials.Add(material);
        return material;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        // Changed: Set URP and built-in color properties when available. Why: Project render pipeline can vary in editor tests.
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private static void ConfigureMaterialForAlpha(Material material, bool transparent)
    {
        // Changed: Configure common URP/built-in alpha properties. Why: The neutral backing and shadows need controlled opacity.
        if (!transparent)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Mode"))
            material.SetFloat("_Mode", 3f);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = 3000;
    }

    private void DestroyRuntimeObjects()
    {
        // Changed: Destroy only objects created by this component. Why: Scene assets and shared materials must not be touched.
        for (int i = 0; i < runtimeMaterials.Count; i++)
        {
            if (runtimeMaterials[i] != null)
                Destroy(runtimeMaterials[i]);
        }

        for (int i = 0; i < runtimeMeshes.Count; i++)
        {
            if (runtimeMeshes[i] != null)
                Destroy(runtimeMeshes[i]);
        }

        runtimeMaterials.Clear();
        runtimeMeshes.Clear();
    }

    private static float EaseOutBack(float t)
    {
        // Changed: EaseOutBack gives each new sticker a small pop. Why: Catch feedback should be readable without particles.
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color color);
        return color;
    }
}
