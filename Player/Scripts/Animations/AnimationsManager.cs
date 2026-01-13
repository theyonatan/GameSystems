using System.Collections.Generic;
using System.Linq;
using AYellowpaper.SerializedCollections;
using UnityEngine;
using SHG.AnimatorCoder;
using UnityEngine.Events;

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
            float crossfade = 0.2f, params AnimationCondition[] connections)
        {
            _animations.Add(animationName,
                new AnimationData(animationName, lockLayer, autoNextAnimation, crossfade, connections));

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

        public void Build(AnimationsManager animationsManager)
        {
            // assign values to brain
            animationsManager.Animations = _animations;
            animationsManager.Parameters = new SerializedDictionary<string, bool>(
                _parameters.ToDictionary(param => param, _ => false));
            animationsManager.AnimatorParameters = _parameterHashes;
            animationsManager.OnDefaultAnimationRequested.AddListener(_defaultAnimationAction);
            
            animationsManager.OnEnablePlayer(); // get animator
            animationsManager.playerAnimator.runtimeAnimatorController = _animatorController;
            
            // verify animations exist on AnimatorController
            // ValidateAnimatorClips();
            
            // initialize brain
            animationsManager.Initialize(animationsManager.playerAnimator);
        }

        void ValidateAnimatorClips()
        {
            // get animation clips names from animator
            var clipNames = _animatorController.animationClips
                .Select(c => c.name)
                .ToHashSet();;

            foreach (var clipName in clipNames)
            {
                Debug.Log($"--{clipName}");
            }
            // validate
            foreach (var anim in _animations.Values)
            {
                if (!clipNames.Contains(anim.AnimationClipName))
                    Debug.LogError($"[AnimatorCoder] Animation '{anim.AnimationClipName}' not found in controller");

                if (anim.AutoNextAnimation != null && !clipNames.Contains(anim.AutoNextAnimation))
                    Debug.LogError($"[AnimatorCoder] Next animation '{anim.AutoNextAnimation}' not found in controller");
            }
        }
    }
}
