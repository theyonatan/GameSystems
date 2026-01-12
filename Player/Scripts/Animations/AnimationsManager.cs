using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SHG.AnimatorCoder;

public class AnimationsManager : AnimatorCoder, IPlayerBehavior
{
    [SerializeField] private Animator playerAnimator;
    
    private Dictionary<string, AnimationData> _animations = new();
    private Dictionary<string, int> _parameterHashes = new();

    public void OnEnablePlayer()
    {
        if (!GetComponent<Player>().HasAuthority)
            return;

        playerAnimator ??= GetComponentInChildren<Animator>();
        if (!playerAnimator)
            Debug.LogError("no player animator was found!");
        else
            Initialize(playerAnimator);
    }

    // ===== Loading =====

    class Builder
    {
        private readonly Dictionary<string, AnimationData> _animations;
        private readonly List<string> _parameters;
        private readonly Dictionary<string, int> _parameterHashes;
        private readonly RuntimeAnimatorController _animatorController;

        public Builder(RuntimeAnimatorController animatorController)
        {
            _animatorController = animatorController;

            _animations = new Dictionary<string, AnimationData>();
            _parameters = new List<string>();
            _parameterHashes = new Dictionary<string, int>();
        }

        public Builder(string animatorControllerName)
        {
            var controller = Resources.Load<RuntimeAnimatorController>(animatorControllerName);

            if (!controller)
                Debug.LogError($"Animator controller '{animatorControllerName}' not found!");
            else
                _animatorController = controller;

            _animations = new Dictionary<string, AnimationData>();
            _parameters = new List<string>();
            _parameterHashes = new Dictionary<string, int>();
        }

        public Builder AddAnimation(AnimationData animationData)
        {
            _animations.Add(animationData.animationName, animationData);

            return this;
        }

        public Builder AddParameter(string parameterName)
        {
            _parameters.Add(parameterName);
            _parameterHashes.Add(parameterName, Animator.StringToHash(parameterName));

            return this;
        }

        public void Build(AnimationsManager animationsManager)
        {
            animationsManager._animations = _animations;
            animationsManager._parameters = _parameters.ToDictionary(param => param, _ => false);

            animationsManager.playerAnimator.runtimeAnimatorController = _animatorController;
            animationsManager.Initialize(animationsManager.playerAnimator);
        }
    }

    /// <summary>
    /// Sets a parameter on the actual Unity animator
    /// </summary>
    public void SetFloat(string parameter, float value)
    {
        playerAnimator.SetFloat(hash, value);
    }

public override void DefaultAnimation(int layer)
    {
        // todo: Locomotion blend tree is Default.
    }
}
