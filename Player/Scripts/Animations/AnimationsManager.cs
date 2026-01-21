using System.Collections.Generic;
using System.Linq;
using AYellowpaper.SerializedCollections;
using UnityEngine;
using SHG.AnimatorCoder;
using UnityEngine.Events;

/// <summary>
/// Guide: AnimationsManager
///
/// there are 2 sides to this:
/// 1. main animations, the movement state gives us the base animations
/// 2. extension animations, adding to those on movement states.
/// calling the builder will not delete old animations, I trust the new state to never call them.
/// it will add or replace animations and parameter configurations.
///
/// about default animations:
/// only the movementstate can call default animations.
/// in the builder, if a new default is requested, it will remove the old and put a new one.
/// extensions will call their animations on top of whats running on the movement states,
/// which are the base player animations.
/// </summary>
public class AnimationsManager : AnimatorCoder, IPlayerBehavior
{
    [SerializeField] private Animator playerAnimator;

    public void OnEnablePlayer()
    {
        if (!GetComponent<Player>().HasAuthority)
            return;

        playerAnimator = GetComponentInChildren<Animator>(true);
        if (!playerAnimator)
            Debug.LogError($"[AnimationManager] animator on character not found!");
    }

    // ===== Loading =====

    public class Builder
    {
        private readonly Dictionary<string, AnimationData> _animations;
        private readonly List<string> _parameters;
        private readonly Dictionary<string, int> _parameterHashes;
        private readonly RuntimeAnimatorController _animatorController;
        private UnityAction _defaultAnimationAction;
        private bool _debugMode;

        public Builder(RuntimeAnimatorController animatorController)
        {
            _animatorController = animatorController;

            _animations = new Dictionary<string, AnimationData>();
            _parameters = new List<string>();
            _parameterHashes = new Dictionary<string, int>();
        }

        public Builder(string animatorControllerName)
        {
            var animatorController = Resources.Load<RuntimeAnimatorController>(animatorControllerName);

            if (!animatorController)
                Debug.LogError($"Animator controller '{animatorControllerName}' not found!");
            else
                _animatorController = animatorController;

            _animations = new Dictionary<string, AnimationData>();
            _parameters = new List<string>();
            _parameterHashes = new Dictionary<string, int>();
        }

        public Builder AddAnimation(string animationName, bool lockLayer = false, string autoNextAnimation = null,
            bool loops = true, float entryCrossfade = 0f, params Connection[] connections)
        {
            _animations.Add(animationName,
                new AnimationData(animationName, lockLayer, autoNextAnimation, loops, entryCrossfade, connections));

            return this;
        }

        /// <summary>
        /// Adds a boolean parameter that is used in the code-based animation system
        /// animator based parameters are used in the animator controller only.
        /// </summary>
        public Builder AddParameter(string parameterName)
        {
            _parameters.Add(parameterName);
            _parameterHashes.Add(parameterName, Animator.StringToHash(parameterName));

            return this;
        }

        /// <summary>
        /// Animation to play when unsure what to play / Default / Entry
        /// </summary>
        /// <param name="defaultAnimationAction">This function will get called which should play the animation</param>
        public Builder SetDefaultAnimation(UnityAction defaultAnimationAction)
        {
            _defaultAnimationAction = defaultAnimationAction;
            
            return this;
        }

        public Builder AllowDebug()
        {
            _debugMode = true;
            
            return this;
        }

        /// <summary>
        /// Building animations:
        /// Adds new animations or overrides existing ones.
        /// same for parameters.
        /// this function also detects the animator and sets the default animation if a new one is requested.
        /// </summary>
        /// <param name="animationsManager"></param>
        public void Build(AnimationsManager animationsManager)
        {
            // -------------------------------------------------------
            // assign animations and parameters to brain - add new or replace if existing.
            // -------------------------------------------------------
            // animations
            animationsManager.Animations ??= new Dictionary<string, AnimationData>();
            foreach (var kvp in _animations)
                animationsManager.Animations[kvp.Key] = kvp.Value;
            
            // parameters
            animationsManager.Parameters ??= new SerializedDictionary<string, bool>();
            foreach (var param in _parameters)
                if (!animationsManager.Parameters.ContainsKey(param))
                    animationsManager.Parameters.Add(param, false);
            
            // parameter hashes
            animationsManager.AnimatorParameters ??= new Dictionary<string, int>();
            foreach (var kvp in _parameterHashes)
                animationsManager.AnimatorParameters[kvp.Key] = kvp.Value;

            // -------------------------------------------------------
            // if movement state, a default animation function is requested.
            if (_defaultAnimationAction != null)
            {
                animationsManager.OnDefaultAnimationRequested.RemoveAllListeners();
                animationsManager.OnDefaultAnimationRequested.AddListener(_defaultAnimationAction);
            }
            
            // -------------------------------------------------------
            // we only initialize the brain once. works between MovementStates.
            if (!animationsManager.Initialized)
            {
                animationsManager.OnEnablePlayer(); // get animator
                animationsManager.playerAnimator.runtimeAnimatorController = _animatorController;
                animationsManager.DebugMode = _debugMode;
                
                
                // initialize brain
                animationsManager.Initialize(animationsManager.playerAnimator);
            }
        }
    }
}
