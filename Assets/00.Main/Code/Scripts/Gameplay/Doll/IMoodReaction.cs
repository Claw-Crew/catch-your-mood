/// <summary>
/// Implemented by per-mood reaction MonoBehaviours (HappyDollReaction, AngryDollReaction, ...).
/// DollInteractable dispatches XRI events to whichever IMoodReaction is on the same GameObject.
/// </summary>
public interface IMoodReaction
{
    /// <summary>Claw enters hover range. Doll is NOT yet held.</summary>
    void OnApproach();

    /// <summary>Claw leaves hover range without grabbing. Reactions should restore rest state.</summary>
    void OnRetreat();

    /// <summary>Claw closes on the doll (XRI selectEntered). Doll is now held.</summary>
    void OnGrabbed();

    /// <summary>Claw releases the doll (XRI selectExited).</summary>
    void OnReleased();
}
