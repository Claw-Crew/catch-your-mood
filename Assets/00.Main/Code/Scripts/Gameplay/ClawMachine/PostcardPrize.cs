using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// 게임 종료 시 PrizeChute(PickupBin) 위에 떨어지는 베이지 엽서.
/// 데이터 흐름:
///   GameManager.EndGame() → Spawn(PickupBin 위) → Start()에서 dropSound 1회 재생
///   → Rigidbody로 자연 낙하 → 컨트롤러 grip으로 grab
///   → XRGrabInteractable.selectEntered → EmotionRecipeUI.ShowResult() 호출.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(AudioSource))]
public class PostcardPrize : MonoBehaviour
{
    [Tooltip("엽서가 떨어질 때 1회 재생할 사운드(예: Freesound #205911 Papers falling, CC0).")]
    public AudioClip dropSound;

    [Tooltip("드롭 사운드 볼륨.")]
    [Range(0f, 1f)]
    public float dropVolume = 0.7f;

    private XRGrabInteractable grab;
    private AudioSource audioSource;
    private bool resultTriggered;

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        if (dropSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(dropSound, dropVolume);
        }
    }

    private void OnEnable()
    {
        if (grab != null) grab.selectEntered.AddListener(OnGrabbed);
    }

    private void OnDisable()
    {
        if (grab != null) grab.selectEntered.RemoveListener(OnGrabbed);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (resultTriggered) return;
        resultTriggered = true;

        var ui = FindAnyObjectByType<EmotionRecipeUI>();
        if (ui != null)
        {
            ui.ShowResult();
        }
    }
}
