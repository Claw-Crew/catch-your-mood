using UnityEngine;

public class CatchDetector : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Doll"))
        {
            DollInfo info = other.GetComponent<DollInfo>();

            if (info != null)
            {
                GameResultManager.Instance.RegisterCatch(info.emotionType);
            }
        }
    }
    
}