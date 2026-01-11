using System.Collections.Generic;
using UnityEngine;
using SHG.AnimatorCoder;

public class AnimationsManager : AnimatorCoder, IPlayerBehavior
{
    [SerializeField] private Animator playerAnimator;

    private readonly Dictionary<string, int> _animationHashes = new();
    private readonly Dictionary<string, int> _parameterHashes = new();
    
    public void OnEnablePlayer()
    {
        if (!GetComponent<Player>().HasAuthority)
            return;
        
        playerAnimator ??= GetComponentInChildren<Animator>();
        if (!playerAnimator)
            Debug.LogError("no player animator was found!");
        
        Initialize(playerAnimator);
    }
    
    // ===== Loading =====

    public void LoadAnimator(RuntimeAnimatorController animatorController)
    {
        playerAnimator.runtimeAnimatorController = animatorController;
    }

    public void LoadAnimations(params string[] animationNames)
    {
        _animationHashes.Clear();
        
        // TODO: Use add or replace instead
        foreach (var animationName in animationNames)
            _animationHashes.Add(animationName, Animator.StringToHash(animationName));
    }

    /// <summary>
    /// adds a new animation or replaces if already exists
    /// </summary>
    /// <param name="animationData"></param>
    public void AddOrReplace(AnimationData animationData)
    {
        
    }

    public void LoadParameters(params string[] parameterNames)
    {
        _parameterHashes.Clear();
        
        foreach (var parameterName in parameterNames)
            _parameterHashes.Add(parameterName, Animator.StringToHash(parameterName));
    }
    
    // ===== Runtime API =====

    public bool Play(string animationName, int layer = 0, float crossfade = 0.15f)
    {
        if (!_animationHashes.TryGetValue(animationName, out var hash))
        {
            Debug.LogWarning($"Animation '{animationName}' not loaded");
            return false;
        }

        playerAnimator.CrossFade(hash, crossfade, layer);
        return true;
    }

    public void SetBool(string parameter, bool value)
    {
        if (_parameterHashes.TryGetValue(parameter, out var hash))
            playerAnimator.SetBool(hash, value);
    }

    public void SetFloat(string parameter, float value)
    {
        if (_parameterHashes.TryGetValue(parameter, out var hash))
            playerAnimator.SetFloat(hash, value);
    }

    public override void DefaultAnimation(int layer)
    {
        
    }
}
