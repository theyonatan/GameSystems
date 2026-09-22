using System;
using UnityEngine;
using UnityEngine.Audio;

namespace GameSystems.Audio
{
    [Serializable]
    public sealed class AudioChannelBinding
    {
        public AudioChannel Channel;
        public AudioMixerGroup Group;
        public string VolumeParameter;
        [Range(0f, 1f)] public float DefaultVolume = 1f;
    }

    [CreateAssetMenu(menuName = "GameSystems/Audio/Mix Settings")]
    public sealed class AudioMixSettings : ScriptableObject
    {
        public AudioMixer Mixer;
        public string MasterParameter = "MasterVolume";
        [Range(0f, 1f)] public float DefaultMasterVolume = 1f;
        [Tooltip("Use a different prefix in each game.")]
        public string PreferencesPrefix = "GameAudio";
        public AudioChannelBinding[] Channels = Array.Empty<AudioChannelBinding>();

        public AudioChannelBinding Find(AudioChannel channel) => Array.Find(Channels, x => x != null && x.Channel == channel);
        public AudioMixerGroup Group(AudioChannel channel) => Find(channel)?.Group;
    }
}
