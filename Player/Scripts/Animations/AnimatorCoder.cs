//Author: Small Hedge Games
//Date: 02/07/2024

using System.Collections;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;

namespace SHG.AnimatorCoder
{
    public abstract class AnimatorCoder : MonoBehaviour
    {
        /// <summary> The baseline animation logic on a specific layer </summary>
        public abstract void DefaultAnimation(int layer);
        private Animator animator = null;
        private string[] currentAnimation;
        private bool[] layerLocked;
        private Dictionary<string, bool> parameters;
        private Coroutine[] currentCoroutine;
        private List<string> animationNames = new ();

        /// <summary> Sets up the Animator Brain </summary>
        public void Initialize(Animator animator = null)
        {
            this.animator = animator ? animator : GetComponent<Animator>();

            AnimatorValues.Initialize(animator);
            
            // 3 arrays each the size of the amount of layers.
            currentCoroutine = new Coroutine[this.animator.layerCount];
            layerLocked = new bool[this.animator.layerCount];
            currentAnimation = new string[this.animator.layerCount];

            for (int i = 0; i < this.animator.layerCount; ++i)
            {
                layerLocked[i] = false;

                int hash = this.animator.GetCurrentAnimatorStateInfo(i).shortNameHash;
                for (int k = 0; k < AnimatorValues.AnimationsHashes.Length; ++k)
                {
                    if (hash == AnimatorValues.AnimationsHashes[k])
                    {
                        currentAnimation[i] = (Animations)Enum.GetValues(typeof(Animations)).GetValue(k);
                        k = AnimatorValues.AnimationsHashes.Length;
                    }
                }
            }

            string[] names = Enum.GetNames(typeof(Parameters));
            parameters = new ParameterDisplay[names.Length];
            for (int i = 0; i < names.Length; ++i)
            {
                parameters[i].name = names[i];
                parameters[i].value = false;
            }
        }

        /// <summary> Returns the current animation that is playing </summary>
        public string GetCurrentAnimation(int layer)
        {
            try
            {
                return currentAnimation[layer];
            }
            catch
            {
                LogError("Can't retrieve Current Animation. Fix: Initialize() in Start() and don't exceed number of animator layers");
                return "RESET";
            }
        }

        /// <summary> Sets the whole layer to be locked or unlocked </summary>
        public void SetLocked(bool lockLayer, int layer)
        {
            try
            {
                layerLocked[layer] = lockLayer;
            }
            catch
            {
                LogError("Can't retrieve Current Animation. Fix: Initialize() in Start() and don't exceed number of animator layers");
            }
        }

        public bool IsLocked(int layer)
        {
            try
            {
                return layerLocked[layer];
            }
            catch
            {
                LogError("Can't retrieve Current Animation. Fix: Initialize() in Start() and don't exceed number of animator layers");
                return false;
            }
        }

        /// <summary> Sets an animator parameter </summary>
        public void SetBool(string id, bool value)
        {
            try
            {
                parameters[id] = value;
            }
            catch
            {
                LogError("Please Initialize() in Start()");
            }
        }

        /// <summary> Returns an animator parameter </summary>
        public bool GetBool(string id)
        {
            try
            {
                return parameters[id];
            }
            catch
            {
                LogError("Please Initialize() in Start()");
                return false;
            }
        }

        /// <summary> Takes in the animation details and the animation layer, then attempts to play the animation </summary>
        public void Play(AnimationData data, int layer = 0)
        {
            try
            {
                if (data.animationName == "RESET")
                {
                    DefaultAnimation(layer);
                }

                if (layerLocked[layer] || currentAnimation[layer] == data.animationName) return;

                if (currentCoroutine[layer] != null) StopCoroutine(currentCoroutine[layer]);
                layerLocked[layer] = data.lockLayer;
                currentAnimation[layer] = data.animationName;

                animator.CrossFade(AnimatorValues.GetHash(currentAnimation[layer]), data.crossfade, layer);

                if (data.nextAnimation == null) return;
                
                // next animation
                currentCoroutine[layer] = StartCoroutine(Wait());
                IEnumerator Wait()
                {
                    animator.Update(0);
                    float delay = animator.GetNextAnimatorStateInfo(layer).length;
                    if (data.crossfade == 0) delay = animator.GetCurrentAnimatorStateInfo(layer).length;
                    yield return new WaitForSeconds(delay - data.nextAnimation.crossfade);
                    SetLocked(false, layer);
                    Play(data.nextAnimation, layer);
                }
            }
            catch
            {
                LogError("Please Initialize() in Start()");
            }
        }

        private void LogError(string message)
        {
            Debug.LogError("AnimatorCoder Error: " + message);
        }
    }

    /// <summary> Holds all data about an animation </summary>
    [Serializable]
    public class AnimationData
    {
        public string animationName;
        /// <summary> Should the layer lock for this animation? </summary>
        public bool lockLayer;
        /// <summary> Should an animation play immediately after? </summary>
        public AnimationData nextAnimation;
        /// <summary> Should there be a transition time into this animation? </summary>
        public float crossfade = 0;

        /// <summary> Sets the animation data </summary>
        public AnimationData(string animationName = "RESET", bool lockLayer = false, AnimationData nextAnimation = null, float crossfade = 0)
        {
            this.animationName = animationName;
            this.lockLayer = lockLayer;
            this.nextAnimation = nextAnimation;
            this.crossfade = crossfade;
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

    /// <summary> Allows the animation parameters to be shown in debug inspector </summary>
    [Serializable]
    public struct ParameterDisplay
    {
        [HideInInspector] public string name;
        public bool value;
    }
}
