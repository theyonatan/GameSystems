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
                
                var hash = AnimatorValues.AnimationsHashes
                    .FirstOrDefault(x => x.Value == state.shortNameHash)
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

                Parameters[parameterName] = value;
            }
            catch (Exception e)
            {
                Debug.LogError($"Trying to set: Parameter Not found: {parameterName}\n {e}");
            }
            
            EvaluateParameters();
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
        public void Play(string animationClipName, int layer = 0)
        {
            AnimationData animationToPlay = Animations[animationClipName];
            
            // verify if current animation needs to reset
            if (animationClipName == "RESET")
                EntryAnimation();

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

            // Animator Play new animation
            _animator.CrossFade(AnimatorValues.GetHash(_currentAnimation[layer]), animationToPlay.Crossfade, layer);

            // Handle if There's next animation
            if (animationToPlay.AutoNextAnimation == null) return;
            
            _currentCoroutine[layer] = StartCoroutine(Wait());
            IEnumerator Wait()
            {
                // wait for the current animation to finish
                float delay = _animator.GetNextAnimatorStateInfo(layer).length;
                if (animationToPlay.Crossfade == 0) delay = _animator.GetCurrentAnimatorStateInfo(layer).length;

                if (Animations.TryGetValue(animationToPlay.AutoNextAnimation, out AnimationData nextAnimation))
                {
                    // play next animation
                    yield return null; // Let animator settle one frame
                    yield return new WaitForSeconds(delay - nextAnimation.Crossfade);
                
                    // above we can cancel the coroutine in case an overriding animation is played before reaching this
                    SetLocked(false, layer);
                    Play(nextAnimation.AnimationClipName, layer);
                }
                else
                {
                    yield return new WaitForSeconds(delay);
                    
                    // no next animation, do we transition to a new one?
                    EvaluateParameters();
                }
            }
        }

        /// <summary>
        /// Called after animation finishes or parameter changed
        /// checks if we can transition to a new state based on parameters
        /// </summary>
        private void EvaluateParameters()
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

                    Play(possible.ResultAnimationName, layer);
                    
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
        public float Crossfade;

        /// <summary> Next animations with conditions </summary>
        public IReadOnlyList<AnimationCondition> FollowingAnimations;

        /// <summary> Sets the animation data </summary>
        public AnimationData(string animationClipName = "RESET", bool lockLayer = false, string autoNextAnimation = null, float crossfade = 0, IReadOnlyList<AnimationCondition> conditions = null)
        {
            AnimationClipName = animationClipName;
            LockLayer = lockLayer;
            AutoNextAnimation = autoNextAnimation;
            Crossfade = crossfade;
            FollowingAnimations = conditions ?? new List<AnimationCondition>();
            Hash = Animator.StringToHash(animationClipName);
        }
    }

    public class AnimationCondition
    {
        public string ResultAnimationName;
        private List<AnimationParameter> _conditions;

        public AnimationCondition(string resultAnimation, params AnimationParameter[] conditions)
        {
            ResultAnimationName = resultAnimation;
            _conditions = conditions.ToList();
        }

        public bool Evaluate(Dictionary<string, bool> parameters)
        {
            if (_conditions == null || _conditions.Count == 0)
                return false;

            return _conditions.All(c => parameters[c.ParameterName] == c.TargetCondition);
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
            // load names from controller clips
            var names = GetAllAnimationNames(animator);
            
            // load their hashes
            AnimationsHashes = names.ToDictionary(name => name, Animator.StringToHash);
        }

        /// <summary> Gets the animator hash value of an animation </summary>
        public static int GetHash(string animationName)
        {
            return AnimationsHashes[animationName];
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
