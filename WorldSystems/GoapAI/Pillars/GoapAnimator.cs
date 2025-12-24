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

    public void CrossplayAnimationUsingTimer(string animationClipName, Action onAnimationFinished = null)
    {
        if (!AgentAnimator)
        {
            onAnimationFinished?.Invoke();
            return;
        }
        
        _timer = new CountdownTimer(GetAnimationLength(animationClipName));
        _timer.OnTimerStart += () => AgentAnimator.CrossFade(animationClipName, CrossfadeDuration);

        _timer.OnTimerStop += onAnimationFinished ?? (
            () => AgentAnimator.CrossFade(DefaultAnimationClip, CrossfadeDuration));

        _timer.Start();
    }

    /// <summary>
    /// sets a trigger on animator and throws event after animation ends.
    /// </summary>
    /// <param name="animationClipName">name of the imported clip! the one you drag to animator controller.</param>
    /// <param name="animationTrigger">name of trigger to activate in animator controller.</param>
    /// <param name="onAnimationFinished">event to throw after animation finished playing.</param>
    public void TriggerAnimationUsingTimer(string animationClipName, string animationTrigger, Action onAnimationFinished = null)
    {
        if (!AgentAnimator)
        {
            onAnimationFinished?.Invoke();
            return;
        }
        
        _timer = new CountdownTimer(GetAnimationLength(animationClipName));
        _timer.OnTimerStart += () => AgentAnimator.SetTrigger(animationTrigger);

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
        
        AgentAnimator.CrossFade(animationClipName, CrossfadeDuration);

        onAnimationFinished?.Invoke();

        _timer.Start();
    }
    
    public void SetFloat(string animationClipName, float value) => AgentAnimator?.SetFloat(animationClipName, value);
    public void SetBool(string animationClipName, bool value) => AgentAnimator?.SetBool(animationClipName, value);
    public void SetTrigger(string animationClipName) => AgentAnimator?.SetTrigger(animationClipName);

    public void UpdateAnimationsTimer(float deltaTime)
        => _timer?.Tick(deltaTime);
    
    /// helper functions
    private float GetAnimationLength(string animationClipName)
    {
        int animationClipHash = Animator.StringToHash(animationClipName);
        
        foreach (AnimationClip clip in AgentAnimator.runtimeAnimatorController.animationClips) {
            if (Animator.StringToHash(clip.name) == animationClipHash) {
                return clip.length;
            }
        }

        return -1f;
    }
}
