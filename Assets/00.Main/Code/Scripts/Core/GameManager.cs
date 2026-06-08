using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;

public class GameManager : MonoBehaviour
{
    // Changed: 30f → 180f (3분) 게임 타이머로 변경.
    // Why: 감정 인형뽑기 체험 시간을 3분으로 확장하여 충분한 뽑기 기회를 제공하기 위함.
    public float gameTime = 180f;
    private float timer;
    private bool isGameOver = false;

    // Changed: 시작 버튼 누르기 전까지 타이머가 흐르지 않도록 시작 플래그 추가.
    // Why: 진짜 인형뽑기처럼 플레이어가 명시적으로 게임을 시작한 시점부터 타이머가 카운트되도록 함.
    private bool gameStarted = false;

    // Changed: 정식 타이머 UI(TMP)를 인스펙터에서 연결할 수 있게 노출.
    // Why: 좌상단 OnGUI 디버그 텍스트 대신 World-space Canvas의 큰 카운트다운으로 표시하기 위함.
    [Header("Timer UI (선택 — TMP 연결 시 화면에 카운트다운 표시)")]
    [SerializeField] private TextMeshProUGUI timerLabel;       // World-space Canvas의 TMP 텍스트
    [SerializeField] private string waitingText = "PRESS START";
    [SerializeField] private string overText = "TIME UP";
    // Changed: 기본 타이머 색을 흰색에서 짙은 코코아색으로 변경.
    // Why: 밝은 배경과 합쳐져 보이지 않던 타이머를 런타임 갱신 후에도 읽기 쉽게 유지하기 위함.
    [SerializeField] private Color normalColor = new Color(0.227f, 0.149f, 0.102f, 1f);  // 평상시
    [SerializeField] private Color warningColor = new Color(0.722f, 0.227f, 0.184f, 1f); // 마지막 10초 빨강
    [SerializeField] private float warningSeconds = 10f;       // 이 시간 이하부터 warningColor

    // Changed: 게임 종료 시 PrizeChute에 떨어질 엽서 사운드 + 머티리얼 참조.
    // Why: 결과 화면 즉시 표시 대신 엽서 grab 트리거 방식으로 변경.
    [Header("Postcard Prize (게임 종료 시 PrizeChute에 떨어짐)")]
    public AudioClip postcardDropSound;
    public Color postcardColor = new Color(0.93f, 0.88f, 0.78f, 1f); // BG_Panel 베이지와 동일
    public Vector3 postcardSize = new Vector3(0.13f, 0.007f, 0.09f); // 가로 13cm / 두께 7mm / 세로 9cm
    public float postcardSpawnHeight = 0.35f; // PickupBin 위 스폰 높이

    void Start()
    {
        timer = gameTime;
    }

    void Update()
    {
        // Changed: 매 프레임 타이머 UI 갱신 (early-return 이전에 호출).
        // Why: WAITING / 카운트다운 / TIME UP 세 상태 모두 화면에 정확히 반영되도록.
        UpdateTimerLabel();

        // Debug: B키로 게임 강제 시작 (버튼 우회 테스트용)
        if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame && !gameStarted)
        {
            Debug.Log("[DEBUG] B키로 BeginGame 강제 호출");
            BeginGame();
        }

        if (isGameOver) return;

        // Changed: 게임 시작 전에는 타이머가 흐르지 않음.
        // Why: StartButton.BeginGame() 호출 시까지 대기 상태 유지.
        if (!gameStarted) return;

        // Changed: Spacebar로 결과 화면 강제 이동 추가.
        // Why: 디버그/시연 시 3분을 기다리지 않고 즉시 결과를 확인할 수 있어야 함.
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            EndGame();
            return;
        }

        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            EndGame();
        }
    }

    // Changed: 시작 버튼(StartButton.cs)이 호출하는 진입점.
    // Why: 명시적인 시작 시점부터 타이머가 카운트되도록 일원화.
    public void BeginGame()
    {
        Debug.Log($"[GameManager] BeginGame 호출됨. 이미 시작? {gameStarted}");
        if (gameStarted) return;
        gameStarted = true;
    }

    // Changed: 외부에서 시작 여부 조회 가능하도록 노출.
    // Why: StartButton이 중복 호출 방지 / 디버그 표시 등에 사용.
    public bool HasStarted => gameStarted;

    void EndGame()
    {
        isGameOver = true;

        // Changed: 즉시 ShowResult() 대신 엽서를 PrizeChute에 스폰. 사용자가 컨트롤러로 grab해야 결과 표시.
        // Why: 사용자 요청 — 게임 종료 후 엽서를 잡아야 결과지가 나오는 인터랙션 흐름으로 변경.
        if (TrySpawnPostcard()) return;

        // Fallback: PickupBin 못 찾거나 EmotionRecipeUI 없으면 즉시 ShowResult.
        FallbackImmediateShowResult();
    }

    // Changed: PrizeChute(PickupBin) 위에 엽서 GameObject를 절차적으로 생성.
    // Why: 사용자 요청 — 베이지 엽서가 떨어지고 grab하면 결과지 표시.
    bool TrySpawnPostcard()
    {
        var pickupBin = GameObject.Find("PickupBin");
        if (pickupBin == null)
        {
            Debug.LogWarning("[GameManager] PickupBin not found — falling back to immediate result.");
            return false;
        }
        // EmotionRecipeUI가 있어야만 엽서 흐름 사용 (PostcardPrize가 grab 시 호출).
        if (FindAnyObjectByType<EmotionRecipeUI>() == null)
        {
            Debug.LogWarning("[GameManager] EmotionRecipeUI not found — falling back to immediate result.");
            return false;
        }

        var postcard = GameObject.CreatePrimitive(PrimitiveType.Cube);
        postcard.name = "PostcardPrize";
        postcard.transform.position = pickupBin.transform.position + Vector3.up * postcardSpawnHeight;
        postcard.transform.localScale = postcardSize;

        // 베이지 머티리얼 (결과지 BG_Panel과 동일 색)
        var renderer = postcard.GetComponent<Renderer>();
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader) { name = "M_PostcardPrize" };
        mat.SetColor("_BaseColor", postcardColor);
        mat.SetColor("_Color", postcardColor);
        mat.SetFloat("_Smoothness", 0.18f);
        renderer.material = mat;

        // 물리: 떨어지도록
        var rb = postcard.AddComponent<Rigidbody>();
        rb.mass = 0.05f;
        rb.linearDamping = 0.4f;
        rb.angularDamping = 0.6f;

        // XR Grab Interactable (컨트롤러 grip으로 잡기)
        postcard.AddComponent<XRGrabInteractable>();

        // 3D AudioSource (낙하 사운드용)
        var audio = postcard.AddComponent<AudioSource>();
        audio.spatialBlend = 1f;
        audio.playOnAwake = false;
        audio.volume = 1f;

        // PostcardPrize: grab 시 ShowResult() 호출
        var prize = postcard.AddComponent<PostcardPrize>();
        prize.dropSound = postcardDropSound;

        return true;
    }

    void FallbackImmediateShowResult()
    {
        // Changed: 1안(EmotionRecipeUI)과 2안(ResultPanelUI)을 모두 지원하는 우선순위 기반 결과 표시.
        // Why: 엽서 스폰 실패 시 즉시 결과를 보여 게임 흐름이 멈추지 않도록.
        EmotionRecipeUI recipeUI = FindAnyObjectByType<EmotionRecipeUI>();
        if (recipeUI != null)
        {
            recipeUI.ShowResult();
            return;
        }
        ResultPanelUI resultPanel = FindAnyObjectByType<ResultPanelUI>();
        if (resultPanel != null)
        {
            resultPanel.ShowResult();
            return;
        }
        ResultUI ui = FindAnyObjectByType<ResultUI>();
        if (ui != null)
        {
            ui.ShowResult();
        }
    }

    // Changed: 정식 타이머 UI 갱신. WAITING / mm:ss / TIME UP 세 상태를 한 곳에서 처리.
    // Why: timerLabel 없어도 안전하게 동작(null 체크), 있으면 World-space 큰 글자로 표시.
    private void UpdateTimerLabel()
    {
        if (timerLabel == null) return;

        if (isGameOver)
        {
            timerLabel.text = overText;
            timerLabel.color = normalColor;
            return;
        }

        if (!gameStarted)
        {
            timerLabel.text = waitingText;
            timerLabel.color = normalColor;
            return;
        }

        int m = Mathf.FloorToInt(Mathf.Max(0f, timer) / 60f);
        int s = Mathf.FloorToInt(Mathf.Max(0f, timer) % 60f);
        timerLabel.text = $"{m}:{s:D2}";
        timerLabel.color = (timer <= warningSeconds) ? warningColor : normalColor;
    }

    // Debug: 타이머 상태 화면 좌상단에 표시.
    // timerLabel 연결되면 자동 비활성화 (정식 UI와 중복 방지).
    private void OnGUI()
    {
        if (timerLabel != null) return;  // 정식 UI 있으면 디버그 표시 X

        string state = isGameOver ? "OVER" : (gameStarted ? $"{timer:F1}s" : "WAITING (press button)");
        GUI.Label(new Rect(10, 10, 300, 30), $"Timer: {state}");
    }
}
