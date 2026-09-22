using UnityEngine;
using UnityEngine.Audio;

namespace GameSystems.Audio
{
    // Deliberately independent of FEEL and any networking package.
    public abstract class AudioPlaybackBackend : MonoBehaviour
    {
        public abstract AudioSource Play(AudioClip clip, AudioCue cue, Vector3 position, AudioMixerGroup group);
        public abstract void Release(AudioSource source);
    }

    public sealed class AudioHandle
    {
        internal AudioSource Source;
        internal AudioCue Cue;
        internal Transform Follow;
        internal bool HasFollow;
        internal int Scene;
        internal bool Music;
        internal float FadeFrom, FadeTo, FadeTime, FadeDuration;
        internal bool ReleaseAfterFade;
        public bool IsValid => Source;
        public string Key => Cue?.Key;
    }
}
