using System.Collections.Generic;
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
    private RuntimeAnimatorController _animatorController;

    public void OnEnablePlayer()
    {
        if (!GetComponent<Player>().HasAuthority)
            return;

        playerAnimator = GetComponentInChildren<Animator>(true);
        if (!playerAnimator)
            Debug.LogError($"[AnimationManager] animator on character not found!");
    }

    /// <summary>
    /// used to refresh the animator.
    /// useful for when swapping skins.
    /// 
    /// this clears the old animator cache and re-run the default animation.
    /// </summary>
    public void RefreshPlayerAnimator()
    {
        if (!GetComponent<Player>().HasAuthority)
            return;
        
        // recollect player animator
        OnEnablePlayer();

        if (!playerAnimator)
        {
            Debug.LogError("[AnimationsManager] no animator found on new skin after refresh!");
            return;
        }
        
        // load animation controller from new skin
        RuntimeAnimatorController controllerToUse = playerAnimator.runtimeAnimatorController;
        if (!controllerToUse)
            controllerToUse = _animatorController; // keep old one if no new one
        
        _animatorController = controllerToUse;
        
        // apply new controller or old one
        playerAnimator.runtimeAnimatorController = _animatorController;
        RefreshBrain(playerAnimator);
        
        ResetCurrentAnimationCache();
        EntryAnimation();
    }

    // ===== Loading =====

    public class Builder
    {
        private readonly Dictionary<string, AnimationData> _animations;
        private readonly List<string> _parameters;
        private readonly Dictionary<string, int> _parameterHashes;
        private RuntimeAnimatorController _animatorController;
        private UnityAction _defaultAnimationAction;
        private bool _debugMode;

        public Builder(RuntimeAnimatorController animatorController)
        {
            _animatorController = animatorController;

            _animations = new Dictionary<string, AnimationData>();
            _parameters = new List<string>();
            _parameterHashes = new Dictionary<string, int>();
        }

        public Builder(string animatorControllerName=null)
        {
            if (animatorControllerName != null)
                LoadAnimatorControllerResources(animatorControllerName);
            else // no controller provided, must be second initialization or provided via skin
                _animatorController = null;

            _animations = new Dictionary<string, AnimationData>();
            _parameters = new List<string>();
            _parameterHashes = new Dictionary<string, int>();
        }

        private void LoadAnimatorControllerResources(string animatorControllerName="tps_animator")
        {
            var animatorController = Resources.Load<RuntimeAnimatorController>(animatorControllerName);

            if (!animatorController)
                Debug.LogError($"Animator controller '{animatorControllerName}' not found!");
            else
                _animatorController = animatorController;
        }

        public Builder AddAnimation(string animationName, bool lockLayer = false, string autoNextAnimation = null,
            float autoNextCrossfade = -1f, bool loops = true, float entryCrossfade = 0f, bool onEndControlsTransition = false, UnityAction onEnd=null,
            params Connection[] connections)
        {
            _animations.Add(animationName,
                new AnimationData(animationName, lockLayer, autoNextAnimation, autoNextCrossfade, loops, entryCrossfade, connections, onEndControlsTransition, onEnd));

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
            // if Anyone asks for a debug mode we enable it
            if (_debugMode)
                animationsManager.DebugMode = _debugMode;
            
            // -------------------------------------------------------
            // Initialize the animation brain with a controller only once.
            // Only movement states should initialize it because they provide
            // the default animation logic.
            //
            // Extensions may build before the movement state.
            // In that case, they only register their animations/parameters,
            // and the movement state initializes the brain later.
            if (!animationsManager.Initialized && _defaultAnimationAction != null)
            {
                animationsManager.OnEnablePlayer(); // get animator
                
                // check if an AnimationController is available with skin
                RuntimeAnimatorController controllerToUse = _animatorController;
                if (!controllerToUse)
                    controllerToUse = animationsManager.playerAnimator.runtimeAnimatorController;

                if (!controllerToUse)
                {
                    Debug.LogWarning("[AnimationsManager] No RuntimeAnimatorController found on skin. reverting to default");
                    LoadAnimatorControllerResources();
                    controllerToUse = _animatorController;
                }

                animationsManager.playerAnimator.runtimeAnimatorController = controllerToUse;
                animationsManager._animatorController = controllerToUse;
                
                // initialize brain
                animationsManager.Initialize(animationsManager.playerAnimator);
            }
        }
    }
}
