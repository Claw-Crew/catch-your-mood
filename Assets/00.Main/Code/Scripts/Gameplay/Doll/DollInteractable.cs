using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Dispatches XR Interaction Toolkit hover/select events on a doll to its
/// per-mood IMoodReaction sibling component (HappyDollReaction, etc.).
///
/// The doll's facial expression is baked into the material's _BaseMap at
/// edit time (each M_*Doll_Orig.mat already binds the matching T_*Doll_BC.png),
/// so no runtime texture handling is performed here.
///
/// Required sibling components on the doll prefab:
///   - DollInfo            (already exists; sets EmotionType)
///   - XRGrabInteractable  (XRI 3.3.1, grabbed by the claw)
///   - Rigidbody + Collider (so XRI + CatchDetector trigger work)
///   - One *DollReaction component implementing IMoodReaction
/// Tag the GameObject "Doll" so CatchDetector.OnTriggerEnter routes through it.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(DollInfo))]
[DisallowMultipleComponent]
public class DollInteractable : MonoBehaviour
{
    private XRGrabInteractable grabInteractable;
    private IMoodReaction reaction;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        reaction = GetComponent<IMoodReaction>();

        if (reaction == null)
        {
            Debug.LogWarning(
                $"[DollInteractable] No IMoodReaction component on '{name}'. " +
                "Add one of the *DollReaction components (HappyDollReaction, AngryDollReaction, etc.).",
                this);
        }
    }

    private void OnEnable()
    {
        if (grabInteractable == null) return;
        grabInteractable.hoverEntered.AddListener(HandleHoverEntered);
        grabInteractable.hoverExited.AddListener(HandleHoverExited);
        grabInteractable.selectEntered.AddListener(HandleSelectEntered);
        grabInteractable.selectExited.AddListener(HandleSelectExited);
    }

    private void OnDisable()
    {
        if (grabInteractable == null) return;
        grabInteractable.hoverEntered.RemoveListener(HandleHoverEntered);
        grabInteractable.hoverExited.RemoveListener(HandleHoverExited);
        grabInteractable.selectEntered.RemoveListener(HandleSelectEntered);
        grabInteractable.selectExited.RemoveListener(HandleSelectExited);
    }

    private void HandleHoverEntered(HoverEnterEventArgs args)
    {
        // Don't trigger hover reaction while the doll is already held.
        if (grabInteractable.isSelected) return;
        reaction?.OnApproach();
    }

    private void HandleHoverExited(HoverExitEventArgs args)
    {
        reaction?.OnRetreat();
    }

    private void HandleSelectEntered(SelectEnterEventArgs args)
    {
        reaction?.OnRetreat();   // cancel any in-flight hover effects first
        reaction?.OnGrabbed();
    }

    private void HandleSelectExited(SelectExitEventArgs args)
    {
        reaction?.OnReleased();
    }
}
