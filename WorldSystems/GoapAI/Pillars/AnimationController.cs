using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class AnimationController : MonoBehaviour {
    const float k_crossfadeDuration = 0.1f;
    
    public Animator animator;
    CountdownTimer timer;

    private bool _lockAnimationOverideTimer;
    
    float animationLength;

    public Dictionary<string, int> animationClips = new();

    public abstract void InitializeClips();

    void Awake() {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        InitializeClips();
    }

    int defaultAnimationClip = 0;
    private int GetLocomotionDefaultClip()
    {
        if (animationClips.ContainsKey("LocomotionClip"))
            return animationClips["LocomotionClip"];
        Debug.LogError("Locomotion animation not found or used animation on an agent without animation!");
        return defaultAnimationClip;
    }
    
    /// <summary>
    /// Plays an animation by state hash.
    /// </summary>
    /// <param name="animationClipHash">hash of the name of the animation to play</param>
    /// <param name="animationCallback">function to call when animation ends</param>
    /// <param name="lockOveride">if this is the last animation that will play, this will lock the timer and not allow playing anymore animations.</param>
    public void PlayAnimationUsingTimer(int animationClipHash, Action animationCallback = null, bool lockOveride = false)
    {
        if (_lockAnimationOverideTimer)
            return;
        _lockAnimationOverideTimer = lockOveride;
        
        timer = new CountdownTimer(GetAnimationLength(animationClipHash));
        timer.OnTimerStart += () => animator.CrossFade(animationClipHash, k_crossfadeDuration);

        timer.OnTimerStop += animationCallback != null
            ? animationCallback
            : () => animator.CrossFade(GetLocomotionDefaultClip(), k_crossfadeDuration);

        timer.Start();
    }

    private void Update()
    {
        timer?.Tick(Time.deltaTime);
    }

    public float GetAnimationLength(int hash) {
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips) {
            if (Animator.StringToHash(clip.name) == hash) {
                animationLength = clip.length;
                return clip.length;
            }
        }

        return -1f;
    }
}
