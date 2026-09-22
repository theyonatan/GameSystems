using UnityEngine;

namespace GameSystems.Audio
{
    [AddComponentMenu("GameSystems/Audio/Scene Music")]
    public sealed class SceneMusic : MonoBehaviour
    {
        [Tooltip("Optional for games without an automatic audio bootstrap. Never instantiated twice.")]
        public AudioService AudioPrefab;
        [Tooltip("Choose a library key OR a direct clip below. The key takes precedence.")]
        public string MusicKey;
        public AudioClip Music;
        [Range(0f, 1f)] public float Volume = 0.6f;
        public bool Loop = true;
        [Min(0f)] public float FadeSeconds = 1.5f;
        public bool PlayOnStart = true;

        private void Awake()
        {
            if (!AudioService.Instance && AudioPrefab) Instantiate(AudioPrefab);
        }
        private void Start() { if (PlayOnStart) Play(); }
        public void Play()
        {
            var audio = AudioService.Instance;
            if (!audio) return;
            if (!string.IsNullOrWhiteSpace(MusicKey)) audio.PlayMusic(MusicKey, FadeSeconds, this);
            else audio.PlayMusic(Music, Volume, Loop, FadeSeconds, this);
        }
        private void OnDisable() { if (AudioService.Instance) AudioService.Instance.StopMusic(FadeSeconds, this); }
    }
}
