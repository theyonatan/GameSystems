using System;
using UnityEngine;

public class AnimationController
{
    private const int DefaultAnimationClip = 0;
    private const float CrossfadeDuration = 0.1f;
    
    public readonly Animator AgentAnimator;
    private CountdownTimer _timer;

    public AnimationController(Animator agentAnimator)
    {
        AgentAnimator = agentAnimator;
    }

    public void PlayAnimationUsingTimer(int animationClipHash, Action onAnimationFinished = null)
    {
        _timer = new CountdownTimer(GetAnimationLength(animationClipHash));
        _timer.OnTimerStart += () => AgentAnimator.CrossFade(animationClipHash, CrossfadeDuration);

        _timer.OnTimerStop += onAnimationFinished ?? (
            () => AgentAnimator.CrossFade(DefaultAnimationClip, CrossfadeDuration));

        _timer.Start();
    }

    public void UpdateAnimationsTimer()
        => _timer?.Tick(Time.deltaTime);
    
    /// helper functions
    private int GetAnimationClipHash(string animationClipName)
        => Animator.StringToHash(animationClipName);
    
    private float GetAnimationLength(int hash) {
        foreach (AnimationClip clip in AgentAnimator.runtimeAnimatorController.animationClips) {
            if (Animator.StringToHash(clip.name) == hash) {
                return clip.length;
            }
        }

        return -1f;
    }
}
