using UnityEngine;
using UnityEngine.InputSystem;

public class DebugTester : MonoBehaviour
{
    void Update()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            GameResultManager.Instance.RegisterCatch(EmotionType.Happy);
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            GameResultManager.Instance.RegisterCatch(EmotionType.Angry);
        }

        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            GameResultManager.Instance.RegisterTry();
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            FindFirstObjectByType<ResultUI>().ShowResult();
        }
    }
}