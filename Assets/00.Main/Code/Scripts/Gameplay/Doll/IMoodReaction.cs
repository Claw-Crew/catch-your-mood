/// <summary>
/// Implemented by per-mood reaction MonoBehaviours. ClawHub dispatches distance-driven hover/grab events to it.
/// </summary>
public interface IMoodReaction
{
    /// <summary>Claw enters hover range. Doll is not yet held.</summary>
    void OnApproach();

    /// <summary>Claw leaves hover range without grabbing. Reactions should restore rest state.</summary>
    void OnRetreat();

    /// <summary>Claw closes on the doll after the controller enters the grab phase.</summary>
    void OnGrabbed();

    /// <summary>Claw releases the doll.</summary>
    void OnReleased();
}
