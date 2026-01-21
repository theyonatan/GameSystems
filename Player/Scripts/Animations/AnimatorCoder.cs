using System.Collections;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using AYellowpaper.SerializedCollections;
using UnityEditor.Animations;
using UnityEngine.Events;

namespace SHG.AnimatorCoder
{
    public abstract class AnimatorCoder : MonoBehaviour
    {
        public bool Initialized;
        public bool DebugMode;
        
        /// <summary> The baseline animation logic on a specific layer </summary>
        private void EntryAnimation() => OnDefaultAnimationRequested?.Invoke();
        protected readonly UnityEvent OnDefaultAnimationRequested = new ();
        
        private Animator _animator;

        protected Dictionary<string, AnimationData> Animations; // all available animations for the current animator controller
        protected SerializedDictionary<string, bool> Parameters; // code animator params to control animation flow
        protected Dictionary<string, int> AnimatorParameters; // animator params to control blend trees and animation values
        
        private string[] _currentAnimation;
        private bool[] _layerLocked;
        private Coroutine[] _currentCoroutine;
        
        private readonly HashSet<string> _loggedErrors = new();

        /// <summary> Sets up the Animator Brain </summary>
        protected void Initialize(Animator animator)
        {
            _animator = animator;
            AnimatorValues.Initialize(animator);
            
            // 3 arrays each the size of the amount of layers.
            _currentCoroutine = new Coroutine[_animator.layerCount];
            _layerLocked = new bool[_animator.layerCount];
            _currentAnimation = new string[_animator.layerCount];

            // setup default animation hash for all layers
            for (int i = 0; i < _animator.layerCount; ++i)
            {
                _layerLocked[i] = false;

                var state = _animator.GetCurrentAnimatorStateInfo(i);
                
                var hash = Animations
                    .FirstOrDefault(x => x.Value.Hash == state.shortNameHash)
                    .Key;

                _currentAnimation[i] = hash ?? "Locomotion";
            }

            Initialized = true;
        }

        /// <summary> Returns the current animation that is playing </summary>
        public string GetCurrentAnimation(int layer)
        {
            try
            {
                return _currentAnimation[layer];
            }
            catch
            {
                Debug.LogError("Can't retrieve Current Animation. Fix: Initialize() in Start() and don't exceed number of animator layers");
                return "RESET";
            }
        }

        /// <summary> Sets the whole layer to be locked or unlocked </summary>
        public void SetLocked(bool lockLayer, int layer)
        {
            try
            {
                _layerLocked[layer] = lockLayer;
            }
            catch
            {
                Debug.LogError("Can't retrieve Current Animation. Fix: Initialize() in Start() and don't exceed number of animator layers");
            }
        }

        public bool IsLocked(int layer)
        {
            try
            {
                return _layerLocked[layer];
            }
            catch
            {
                Debug.LogError("Can't retrieve Current Animation. Fix: Initialize() in Start() and don't exceed number of animator layers");
                return false;
            }
        }

        /// <summary> Sets an animator parameter </summary>
        public void SetBool(string parameterName, bool value)
        {
            if (!Initialized) return;
            
            try
            {
                if (Parameters[parameterName] == value)
                    return;
                
                if (DebugMode) Debug.Log($"Setting {parameterName} to {value}");

                Parameters[parameterName] = value;
            }
            catch (Exception e)
            {
                Debug.LogError($"Trying to set: Parameter Not found: {parameterName}\n {e}");
            }
            
            ReEvaluateParameters();
        }

        /// <summary> Returns an animator parameter </summary>
        public bool GetBool(string parameterName)
        {
            try
            {
                return Parameters[parameterName];
            }
            catch
            {
                Debug.LogError($"Trying to get: Parameter Not found: {parameterName}");
                return false;
            }
        }
        
        /// <summary>
        /// Sets a parameter on the actual Unity animator
        /// </summary>
        public void SetFloat(string parameter, float value)
        {
            _animator.SetFloat(AnimatorParameters[parameter], value);
        }

        /// <summary> Takes in the animation details and the animation layer, then attempts to play the animation </summary>
        public void Play(string animationClipName, int layer = 0, float customCrossfade=-1, string reason="Play()")
        {
            if (DebugMode) Debug.LogWarning($"Playing {animationClipName} from {reason}");
            AnimationData animationToPlay = Animations[animationClipName];
            
            // verify layer locked
            if (_layerLocked[layer] || _currentAnimation[layer] == animationClipName)
                return;
            
            if (_currentCoroutine[layer] != null)
            {
                StopCoroutine(_currentCoroutine[layer]);
                _currentCoroutine[layer] = null;
            }
            _layerLocked[layer] = animationToPlay.LockLayer;
            _currentAnimation[layer] = animationClipName;

            // before playing, reevaluate to check if we need to pass to somewhere else.
            ReEvaluateParameters();
            
            // Animator Play new animation
            if (Mathf.Approximately(customCrossfade, -1))
                customCrossfade = animationToPlay.EntryCrossfade;
            _animator.CrossFade(Animations[_currentAnimation[layer]].Hash, customCrossfade, layer);

            // Handle if There's next animation
            if (animationToPlay.AutoNextAnimation == null)
            {
                if (!animationToPlay.Loops)
                    _currentCoroutine[layer] = StartCoroutine(WaitAndPlayDefault());
                
                return;
            }
            
            // Handle if there is a next animation:
            // wait for current one to finish in coroutine, and play the next after.
            _currentCoroutine[layer] = StartCoroutine(WaitAndPlayNext());
            
            IEnumerator WaitAndPlayNext()
            {
                yield return null; // let animator switch to current playing animation so we can work with it.
                
                // wait for the current animation to finish
                float delay = _animator.GetNextAnimatorStateInfo(layer).length;
                if (animationToPlay.EntryCrossfade == 0) delay = _animator.GetCurrentAnimatorStateInfo(layer).length;
                
                // Get next animation (earlier we checked not null)
                var nextAnimation = Animations[animationToPlay.AutoNextAnimation];

                // play next animation
                float timeToWait = delay < nextAnimation.EntryCrossfade ? delay : delay - nextAnimation.EntryCrossfade;
                yield return new WaitForSeconds(timeToWait);
                
                // above we can cancel the coroutine in case an overriding animation is played before reaching this
                SetLocked(false, layer);
                Play(nextAnimation.AnimationClipName, layer, reason: "nextClip");
            }

            IEnumerator WaitAndPlayDefault()
            {
                yield return null; // let animator switch to current playing animation so we can work with it.
                
                // wait for the current animation to finish
                float delay = _animator.GetNextAnimatorStateInfo(layer).length;
                if (animationToPlay.EntryCrossfade == 0) delay = _animator.GetCurrentAnimatorStateInfo(layer).length;
                
                yield return new WaitForSeconds(delay);
                
                // above we can cancel the coroutine in case an overriding animation is played before reaching this
                SetLocked(false, layer);
                EntryAnimation();
            }
        }

        /// <summary>
        /// Called after animation finishes or parameter changed
        /// checks if we can transition to a new state based on parameters
        /// </summary>
        private void ReEvaluateParameters()
        {
            if (!_animator) return;

            for (int layer = 0; layer < _currentAnimation.Length; layer++)
            {
                if (_layerLocked[layer])
                    continue;

                var current = _currentAnimation[layer];

                if (!Animations.TryGetValue(current, out var animData))
                    continue;

                foreach (var possible in animData.FollowingAnimations)
                {
                    if (!possible.Evaluate(Parameters)) continue;

                    Play(possible.ResultAnimationName, layer, possible.CustomCrossfade);
                    
                    return;
                }
            }
        }
    }

    /// <summary> Holds all data about an animation </summary>
    [Serializable]
    public class AnimationData
    {
        public readonly string AnimationClipName;
        
        public int Hash;
        
        /// <summary> Should the layer lock for this animation? </summary>
        public readonly bool LockLayer;
        
        /// <summary> Should an animation play immediately after? </summary>
        public string AutoNextAnimation;
        
        /// <summary> Should there be a transition time into this animation? </summary>
        public float EntryCrossfade;

        /// <summary> Does this animation loop? e.g. walking, idle </summary>
        public bool Loops;

        /// <summary> Next animations with conditions </summary>
        public IReadOnlyList<Connection> FollowingAnimations;

        /// <summary> Sets the animation data </summary>
        public AnimationData(string animationClipName = "RESET", bool lockLayer = false, string autoNextAnimation = null, bool loops = true, float entryCrossfade = 0, IReadOnlyList<Connection> conditions = null)
        {
            AnimationClipName = animationClipName;
            LockLayer = lockLayer;
            AutoNextAnimation = autoNextAnimation;
            Loops = loops;
            EntryCrossfade = entryCrossfade;
            FollowingAnimations = conditions ?? new List<Connection>();
            Hash = Animator.StringToHash(animationClipName);
        }
    }

    public class Connection
    {
        public string ResultAnimationName { get; private set; }
        public float CustomCrossfade;
        public List<AnimationParameter> Conditions { get; } = new();
        
        public static Connection To(string animationName, float customCrossfade=-1) => 
            new() { ResultAnimationName = animationName, CustomCrossfade =  customCrossfade };

        public Connection When(string param, bool value) 
        {
            Conditions.Add(new AnimationParameter(param, value));
            return this;
        }

        public bool Evaluate(Dictionary<string, bool> parameters)
        {
            if (Conditions == null || Conditions.Count == 0)
                return false;

            return Conditions.All(c => parameters[c.ParameterName] == c.TargetCondition);
        }
    }
    
    public class AnimationParameter
    {
        public readonly string ParameterName;
        public readonly bool TargetCondition;
        
        public AnimationParameter(string parameterName, bool targetValue)
        {
            ParameterName = parameterName;
            TargetCondition = targetValue;
        }
    }
    
    /// <summary> Class the manages the hashes of animations and parameters </summary>
    public class AnimatorValues
    {
        /// <summary> Returns the animation hash array </summary>
        public static Dictionary<string, int> AnimationsHashes { get; private set; }

        /// <summary> Initializes the animator state names </summary>
        public static void Initialize(Animator animator)
        {
            // todo: move to builder
            // load names from controller clips
            var names = GetAllAnimationNames(animator);
            
            // load their hashes
            AnimationsHashes = names.ToDictionary(name => name, Animator.StringToHash);
        }

        private static string[] GetAllAnimationNames(Animator animator)
        {
            if (!animator)
            {
                Debug.LogError("Animator is null while trying to initialize clips");
                return Array.Empty<string>();
            }

            AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;
            if (!controller)
            {
                Debug.LogError("Animator Controller is null while trying to initialize clips");
                return Array.Empty<string>();
            }

            var clips = controller.animationClips;
            string[] names = new string[clips.Length];

            for (int i = 0; i < clips.Length; i++)
                names[i] = clips[i].name;

            return names;
        }
    }
}
