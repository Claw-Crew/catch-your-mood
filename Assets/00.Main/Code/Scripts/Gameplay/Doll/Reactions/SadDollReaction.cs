using System.Collections;
using UnityEngine;

/// <summary>
/// Sad doll: droops downward while hovered; emits a tear particle and sighs when grabbed.
/// </summary>
[DisallowMultipleComponent]
public class SadDollReaction : MonoBehaviour, IMoodReaction
{
    [Header("Hover droop")]
    [SerializeField] private float droopHeight = 0.02f;
    [SerializeField] private float droopTransitionDuration = 0.25f;

    [Header("Grab tears")]
    [SerializeField] private ParticleSystem tearParticles;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sighClip;

    [Header("Animated transform (default: mesh child)")]
    [SerializeField] private Transform visualRoot;

    private Vector3 baseLocalPosition;
    private Coroutine moveCo;

    private void Awake()
    {
        if (visualRoot == null)
        {
            var mr = GetComponentInChildren<MeshRenderer>();
            visualRoot = (mr != null) ? mr.transform : transform;
        }
        baseLocalPosition = visualRoot.localPosition;

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    public void OnApproach()
    {
        StartMove(baseLocalPosition + Vector3.down * droopHeight);
    }

    public void OnRetreat()
    {
        StartMove(baseLocalPosition);
    }

    public void OnGrabbed()
    {
        PlayClip(sighClip);
        if (tearParticles != null) tearParticles.Play();
    }

    public void OnReleased()
    {
        if (tearParticles != null) tearParticles.Stop();
        StartMove(baseLocalPosition);
    }

    private void StartMove(Vector3 target)
    {
        if (moveCo != null) StopCoroutine(moveCo);
        moveCo = StartCoroutine(MoveTo(target));
    }

    private IEnumerator MoveTo(Vector3 target)
    {
        Vector3 start = visualRoot.localPosition;
        float t = 0f;
        while (t < droopTransitionDuration)
        {
            t += Time.deltaTime;
            visualRoot.localPosition = Vector3.Lerp(start, target, t / droopTransitionDuration);
            yield return null;
        }
        visualRoot.localPosition = target;
        moveCo = null;
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }
}
