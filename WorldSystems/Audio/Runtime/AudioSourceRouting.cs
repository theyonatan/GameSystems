using UnityEngine;

namespace GameSystems.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioSourceRouting : MonoBehaviour
    {
        public AudioChannel Channel = AudioChannel.Voice;
        private AudioSource _source;
        private void Awake() => _source = GetComponent<AudioSource>();
        private void Start() => Apply();
        private void OnEnable() { if (_source) Apply(); }
        public void Apply()
        {
            var service = AudioService.Instance;
            if (_source && service && service.Mix) _source.outputAudioMixerGroup = service.Mix.Group(Channel);
        }
    }
}
