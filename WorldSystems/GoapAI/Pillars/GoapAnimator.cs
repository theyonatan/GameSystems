using System;
using UnityEngine;

public class GoapAnimator
{
    private const int DefaultAnimationClip = 0;
    private const float CrossfadeDuration = 0.1f;
    
    private readonly Animator AgentAnimator;
    public readonly GoapAnimationMapper AnimationMapper;
    private CountdownTimer _timer;

    public GoapAnimator(Animator agentAnimator, GoapAnimationMapper animationMapper)
    {
        AgentAnimator = agentAnimator;
        AnimationMapper = animationMapper;
    }

    public void PlayAnimationUsingTimer(string animationClipName, Action onAnimationFinished = null)
    {
        if (!AgentAnimator)
        {
            onAnimationFinished?.Invoke();
            return;
        }
        
        int animationClipHash = GetAnimationClipHash(animationClipName);
        
        _timer = new CountdownTimer(animationClipHash);
        _timer.OnTimerStart += () => AgentAnimator.CrossFade(animationClipHash, CrossfadeDuration);

        _timer.OnTimerStop += onAnimationFinished ?? (
            () => AgentAnimator.CrossFade(DefaultAnimationClip, CrossfadeDuration));

        _timer.Start();
    }

    public void PlayAnimationImmediately(string animationClipName, Action onAnimationFinished = null)
    {
        if (!AgentAnimator)
        {
            onAnimationFinished?.Invoke();
            return;
        }
        
        int animationClipHash = GetAnimationClipHash(animationClipName);
        
        AgentAnimator.CrossFade(animationClipHash, CrossfadeDuration);

        onAnimationFinished?.Invoke();

        _timer.Start();
    }
    
    public void SetFloat(string animationClipName, float value) => AgentAnimator?.SetFloat(animationClipName, value);
    public void SetBool(string animationClipName, bool value) => AgentAnimator?.SetBool(animationClipName, value);
    public void SetTrigger(string animationClipName) => AgentAnimator?.SetTrigger(animationClipName);

    public void UpdateAnimationsTimer(float deltaTime)
        => _timer?.Tick(deltaTime);
    
    /// helper functions
    public float GetAnimationLength(string animationClipName)
    {
        int animationClipHash = GetAnimationClipHash(animationClipName);
        
        foreach (AnimationClip clip in AgentAnimator.runtimeAnimatorController.animationClips) {
            if (Animator.StringToHash(clip.name) == animationClipHash) {
                return clip.length;
            }
        }

        return -1f;
    }
    
    private int GetAnimationClipHash(string animationClipName)
        => Animator.StringToHash(animationClipName);
}
