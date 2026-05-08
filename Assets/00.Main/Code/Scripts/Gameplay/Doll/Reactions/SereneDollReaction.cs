using System.Collections;
using UnityEngine;

/// <summary>
/// Serene doll: emission glow rises while hovered; slow rotation and chime when grabbed.
/// Note: the doll's material must have the _EMISSION shader keyword enabled
/// (Material inspector -> Emission checkbox) for the glow to be visible.
/// </summary>
[DisallowMultipleComponent]
public class SereneDollReaction : MonoBehaviour, IMoodReaction
{
    [Header("Hover glow")]
    [SerializeField] private Color glowColor = new Color(1f, 0.95f, 0.7f);
    [SerializeField] private float glowIntensity = 1.5f;
    [SerializeField] private float glowFadeDuration = 0.4f;

    [Header("Grab rotation")]
    [SerializeField] private float rotationSpeedDegPerSec = 60f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip chimeClip;

    [Header("References (auto-found if null)")]
    [SerializeField] private MeshRenderer targetRenderer;
    [SerializeField] private Transform visualRoot;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private MaterialPropertyBlock propertyBlock;
    private Coroutine glowCo;
    private Coroutine rotateCo;
    private Quaternion baseLocalRotation;

    private void Awake()
    {
        if (targetRenderer == null) targetRenderer = GetComponentInChildren<MeshRenderer>();
        if (visualRoot == null)
            visualRoot = (targetRenderer != null) ? targetRenderer.transform : transform;
        baseLocalRotation = visualRoot.localRotation;

        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        propertyBlock = new MaterialPropertyBlock();
        SetEmission(Color.black);
    }

    public void OnApproach()
    {
        StartGlow(glowColor * glowIntensity);
    }

    public void OnRetreat()
    {
        StartGlow(Color.black);
    }

    public void OnGrabbed()
    {
        PlayClip(chimeClip);
        StartGlow(glowColor * glowIntensity);
        StopRotate();
        rotateCo = StartCoroutine(SlowRotateLoop());
    }

    public void OnReleased()
    {
        StopRotate();
        StartGlow(Color.black);
    }

    private void StartGlow(Color target)
    {
        if (glowCo != null) StopCoroutine(glowCo);
        glowCo = StartCoroutine(LerpEmission(target));
    }

    private IEnumerator LerpEmission(Color target)
    {
        Color start = GetCurrentEmission();
        float t = 0f;
        while (t < glowFadeDuration)
        {
            t += Time.deltaTime;
            SetEmission(Color.Lerp(start, target, t / glowFadeDuration));
            yield return null;
        }
        SetEmission(target);
        glowCo = null;
    }

    private IEnumerator SlowRotateLoop()
    {
        while (true)
        {
            visualRoot.localRotation *= Quaternion.Euler(0f, rotationSpeedDegPerSec * Time.deltaTime, 0f);
            yield return null;
        }
    }

    private void StopRotate()
    {
        if (rotateCo != null) { StopCoroutine(rotateCo); rotateCo = null; }
        visualRoot.localRotation = baseLocalRotation;
    }

    private Color GetCurrentEmission()
    {
        if (targetRenderer == null) return Color.black;
        targetRenderer.GetPropertyBlock(propertyBlock);
        return propertyBlock.GetColor(EmissionColorId);
    }

    private void SetEmission(Color c)
    {
        if (targetRenderer == null) return;
        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(EmissionColorId, c);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }
}
