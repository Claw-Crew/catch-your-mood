using UnityEngine;

public class ScaredDollReaction : DollReactionBase
{
    protected override Vector3 HoverEulerAmplitude => new(4f, 0f, 4f);
    protected override float HoverFrequencyHz => 11f;
    protected override Vector3 GrabScaleMultiplier => new(.94f, .94f, .94f);
}
