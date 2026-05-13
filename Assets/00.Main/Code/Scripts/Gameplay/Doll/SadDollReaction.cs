using UnityEngine;

public class SadDollReaction : DollReactionBase
{
    protected override Vector3 HoverPositionOffset => new(0f, -.012f, 0f);
    protected override float HoverFrequencyHz => 1f;
    protected override Vector3 GrabEuler => new(8f, 0f, 0f);
}
