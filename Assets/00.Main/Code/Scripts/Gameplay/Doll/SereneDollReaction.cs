using UnityEngine;

public class SereneDollReaction : DollReactionBase
{
    protected override Vector3 HoverEulerAmplitude => new(0f, 8f, 0f);
    protected override float HoverFrequencyHz => .6f;
    protected override Vector3 GrabEuler => new(0f, 24f, 0f);
}
