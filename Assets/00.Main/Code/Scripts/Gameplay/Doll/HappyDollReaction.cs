using UnityEngine;

public class HappyDollReaction : DollReactionBase
{
    protected override Vector3 HoverPositionOffset => new(0f, .025f, 0f);
    protected override float HoverFrequencyHz => 3.5f;
    protected override Vector3 GrabEuler => new(0f, 180f, 0f);
}
