using UnityEngine;
using UnityEngine.InputSystem;
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

    void Start()
    {
        timer = gameTime;
    }
    void Update()
    {
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
        // Changed: 1안(EmotionRecipeUI)과 2안(ResultPanelUI)을 모두 지원하는 우선순위 기반 결과 표시.
        // Why: 씬에 배치된 UI에 따라 자동으로 적절한 결과 시스템이 활성화됨.
        // 1안: 감정 레시피 카드 (별도 Canvas)
        EmotionRecipeUI recipeUI = FindAnyObjectByType<EmotionRecipeUI>();
        if (recipeUI != null)
        {
            recipeUI.ShowResult();
            return;
        }
        // 2안: Scoreboard 결과 모드 전환
        ResultPanelUI resultPanel = FindAnyObjectByType<ResultPanelUI>();
        if (resultPanel != null)
        {
            resultPanel.ShowResult();
            return;
        }
        // Fallback: 기존 텍스트 결과
        ResultUI ui = FindAnyObjectByType<ResultUI>();
        if (ui != null)
        {
            ui.ShowResult();
        }
    }
    private void OnGUI()
    {       
    string state = isGameOver ? "OVER" : (gameStarted ? $"{timer:F1}s" : "WAITING (press button)");
    GUI.Label(new Rect(10, 10, 300, 30), $"Timer: {state}");
    }
}

