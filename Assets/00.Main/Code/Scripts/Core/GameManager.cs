using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    // Changed: 30f → 180f (3분) 게임 타이머로 변경.
    // Why: 감정 인형뽑기 체험 시간을 3분으로 확장하여 충분한 뽑기 기회를 제공하기 위함.
    public float gameTime = 180f;
    private float timer;
    private bool isGameOver = false;

    void Start()
    {
        timer = gameTime;
    }

    void Update()
    {
        if (isGameOver) return;

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
}