using UnityEngine;

public class GameManager : MonoBehaviour
{
    public float gameTime = 30f;
    private float timer;
    private bool isGameOver = false;

    void Start()
    {
        timer = gameTime;
    }

    void Update()
    {
        if (isGameOver) return;

        timer -= Time.deltaTime;

        if (timer <= 0)
        {
            EndGame();
        }
    }

    void EndGame()
    {
        isGameOver = true;

        ResultUI ui = FindAnyObjectByType<ResultUI>();
        if (ui != null)
        {
            ui.ShowResult();
        }
    }
}