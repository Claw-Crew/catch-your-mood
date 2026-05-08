using UnityEngine;
using UnityEngine.UI;
using System.Text;
using TMPro;

public class ResultUI : MonoBehaviour
{
    public TMP_Text resultText;

    public void ShowResult()
    {
        var results = GameResultManager.Instance.GetResults();
        int tryCount = GameResultManager.Instance.GetTryCount();

        StringBuilder sb = new StringBuilder();

        sb.AppendLine("=== Result ===");
        sb.AppendLine("Tried: " + tryCount);

        foreach (var pair in results)
        {
            sb.AppendLine(pair.Key + ": " + pair.Value);
        }

        resultText.text = sb.ToString();
    }
}