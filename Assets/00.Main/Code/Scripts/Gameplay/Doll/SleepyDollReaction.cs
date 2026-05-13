using UnityEngine;

public class SleepyDollReaction : DollReactionBase
{
    protected override Vector3 HoverEulerAmplitude => new(6f, 0f, 0f);
    protected override float HoverFrequencyHz => .8f;
    protected override Vector3 GrabScaleMultiplier => new(1f, 1.06f, 1f);
}
