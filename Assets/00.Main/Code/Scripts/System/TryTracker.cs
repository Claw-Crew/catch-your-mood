using UnityEngine;

public class TryTracker : MonoBehaviour
{
    public void RegisterTry()
    {
        GameResultManager.Instance.RegisterTry();
    }
}