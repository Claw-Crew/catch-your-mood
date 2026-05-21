using UnityEngine;
using UnityEngine.InputSystem;

// Changed: 디버그 키를 ClawTestController/GameManager와 충돌하지 않게 변경.
// Why: digit1/digit2는 ClawTestController 모드전환/하강과 겹치고,
//      spaceKey는 GameManager 강제 결과 이동과 겹침.
// 변경: digit3=Happy, digit4=Angry, digit5=Sad, digit6=Sleepy, digit7=Scared, digit8=Serene, T=Try
// spaceKey 결과 표시는 GameManager가 담당하므로 제거.
public class DebugTester : MonoBehaviour
{
    void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
            GameResultManager.Instance.RegisterCatch(EmotionType.Happy);

        if (Keyboard.current.digit4Key.wasPressedThisFrame)
            GameResultManager.Instance.RegisterCatch(EmotionType.Angry);

        if (Keyboard.current.digit5Key.wasPressedThisFrame)
            GameResultManager.Instance.RegisterCatch(EmotionType.Sad);

        if (Keyboard.current.digit6Key.wasPressedThisFrame)
            GameResultManager.Instance.RegisterCatch(EmotionType.Sleepy);

        if (Keyboard.current.digit7Key.wasPressedThisFrame)
            GameResultManager.Instance.RegisterCatch(EmotionType.Scared);

        if (Keyboard.current.digit8Key.wasPressedThisFrame)
            GameResultManager.Instance.RegisterCatch(EmotionType.Serene);

        if (Keyboard.current.tKey.wasPressedThisFrame)
            GameResultManager.Instance.RegisterTry();
    }
}