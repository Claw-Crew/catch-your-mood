using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Physical start button on the claw machine. Pressing it (via XR controller select
/// or hand-tracking poke) calls GameManager.BeginGame() to start the 3-minute timer.
///
/// Visual feedback: button scales down (squish) + emission glow on press.
/// One-shot: disables itself after first successful press.
///
/// Required components on the button GameObject:
///   - Collider (BoxCollider/SphereCollider) — used by XRSimpleInteractable for hit detection
///   - XRSimpleInteractable (XRI 3.x)
///   - Renderer (MeshRenderer) — for emission glow effect
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class StartButton : MonoBehaviour
{
    [Header("References (auto-found if null)")]
    [SerializeField] private GameManager gameManager;       // 시작 호출 대상
    [SerializeField] private Renderer buttonRenderer;       // emission 변경 대상

    [Header("Press visual feedback")]
    [SerializeField] private Vector3 pressedScaleMul = new(0.95f, 0.6f, 0.95f);  // 눌릴 때 살짝 납작 (Y만 크게 줄임)
    [SerializeField] private float pressAnimDuration = 0.12f;

    [Header("Emission glow")]
    [SerializeField] private Color idleEmission = Color.black;
    [SerializeField] private Color pressedEmission = new Color(1f, 0.15f, 0.15f) * 2f;
    
    [Header("Audio (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip clickClip;

    private XRSimpleInteractable interactable;
    private Vector3 baseLocalScale;
    private MaterialPropertyBlock propertyBlock;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private bool pressed;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        baseLocalScale = transform.localScale;

        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();
        if (buttonRenderer == null)
            buttonRenderer = GetComponentInChildren<Renderer>();
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        propertyBlock = new MaterialPropertyBlock();
        SetEmission(idleEmission);
    }

    private void OnEnable()
    {
        interactable.selectEntered.AddListener(OnSelectEntered);
    }

    private void OnDisable()
    {
        interactable.selectEntered.RemoveListener(OnSelectEntered);
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        // 한 번만 작동 — 다시 못 눌리도록


        Debug.Log($"[StartButton] PRESSED — interactor: {args.interactorObject.transform.name}, gameManager null? {gameManager == null}");
        if (pressed) return;
        pressed = true;

        // 클릭 사운드
        if (audioSource != null && clickClip != null)
            audioSource.PlayOneShot(clickClip);

        // 시각 피드백 (납작 + 발광)
        StartCoroutine(PressAnimation());

        // 게임 시작 신호
        if (gameManager != null)
            gameManager.BeginGame();
        else
            Debug.LogWarning("[StartButton] GameManager 참조 없음 — BeginGame 호출 못 함.", this);

        // 재누름 차단
        interactable.enabled = false;
    }

    private IEnumerator PressAnimation()
    {
        SetEmission(pressedEmission);
        Vector3 target = Vector3.Scale(baseLocalScale, pressedScaleMul);

        float t = 0f;
        while (t < pressAnimDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / pressAnimDuration);
            transform.localScale = Vector3.Lerp(baseLocalScale, target, p);
            yield return null;
        }
        transform.localScale = target;
    }

    private void SetEmission(Color c)
    {
        if (buttonRenderer == null) return;
        buttonRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(EmissionColorId, c);
        buttonRenderer.SetPropertyBlock(propertyBlock);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.4f, 0.4f);
        Gizmos.DrawCube(transform.position, transform.lossyScale);
    }
}
