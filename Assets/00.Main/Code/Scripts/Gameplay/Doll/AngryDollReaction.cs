using UnityEngine;

public class AngryDollReaction : DollReactionBase
{
    protected override Vector3 HoverEulerAmplitude => new(0f, 0f, 5f);
    protected override float HoverFrequencyHz => 9f;
    protected override Vector3 GrabEuler => new(0f, 0f, -14f);
}
