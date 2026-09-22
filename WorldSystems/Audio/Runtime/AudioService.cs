using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameSystems.Audio
{
    [DefaultExecutionOrder(-10000), DisallowMultipleComponent]
    public sealed class AudioService : MonoBehaviour
    {
        public AudioLibrary Library;
        public AudioMixSettings Mix;
        public AudioPlaybackBackend Backend;
        public static AudioService Instance { get; private set; }
        public event Action<string> CuePlayed;
        public int PlaybackCount { get; private set; }
        public int ActiveVoiceCount => _voices.Count;
        public string CurrentMusicKey => _music != null && _music.IsValid ? _music.Key : null;

        private readonly List<AudioHandle> _voices = new();
        private readonly Dictionary<(string, int), float> _lastPlay = new();
        private readonly Dictionary<string, AudioClip> _lastClip = new();
        private readonly Dictionary<AudioChannel, float> _volumes = new();
        private AudioHandle _music;
        private UnityEngine.Object _musicOwner;
        private float _master = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            if (!Backend) Backend = GetComponent<AudioPlaybackBackend>();
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void Start()
        {
            if (Instance != this) return;
            _master = Mix ? PlayerPrefs.GetFloat(Mix.PreferencesPrefix + ".Master", Mix.DefaultMasterVolume) : 1f;
            SetMasterVolume(_master, false);
            foreach (AudioChannel channel in Enum.GetValues(typeof(AudioChannel)))
            {
                float defaultVolume = Mix ? Mix.Find(channel)?.DefaultVolume ?? 1f : 1f;
                float value = Mix ? PlayerPrefs.GetFloat(Mix.PreferencesPrefix + "." + channel, defaultVolume) : defaultVolume;
                SetVolume(channel, value, false);
            }
        }

        public AudioHandle Play(string key, Vector3 position = default, Transform emitter = null)
        {
            var cue = Library ? Library.Find(key) : null;
            return PlayCue(cue, position, emitter, false);
        }

        private AudioHandle PlayCue(AudioCue cue, Vector3 position, Transform emitter, bool music)
        {
            if (Instance != this || !Backend || cue == null || !cue.HasClips) return null;
            int emitterId = emitter ? emitter.GetInstanceID() : 0;
            var cooldownKey = (cue.Key ?? string.Empty, emitterId);
            if (!music && _lastPlay.TryGetValue(cooldownKey, out float last) && Time.unscaledTime - last < cue.Cooldown) return null;
            int count = 0;
            foreach (var active in _voices)
                if (active.IsValid && active.Source.isPlaying && active.Cue == cue) count++;
            if (!music && count >= Mathf.Max(1, cue.MaxInstances)) return null;
            _lastClip.TryGetValue(cue.Key ?? string.Empty, out var previous);
            AudioClip clip = cue.PickClip(previous);
            var source = Backend.Play(clip, cue, position, Mix ? Mix.Group(cue.Channel) : null);
            if (!source) return null;
            _lastPlay[cooldownKey] = Time.unscaledTime;
            _lastClip[cue.Key ?? string.Empty] = clip;
            var handle = new AudioHandle
            {
                Source = source, Cue = cue, Follow = emitter, HasFollow = emitter,
                Scene = emitter ? emitter.gameObject.scene.handle : SceneManager.GetActiveScene().handle,
                Music = music, FadeFrom = cue.Volume, FadeTo = cue.Volume
            };
            _voices.Add(handle);
            PlaybackCount++;
            CuePlayed?.Invoke(cue.Key);
            return handle;
        }

        public AudioHandle PlayMusic(string key, float fadeSeconds = 1f, UnityEngine.Object owner = null)
        {
            var cue = Library ? Library.Find(key) : null;
            if (cue == null || !cue.HasClips) { StopMusic(fadeSeconds); return null; }
            if (_music != null && _music.IsValid && _music.Key == key && !_music.ReleaseAfterFade)
            { _musicOwner = owner; return _music; }
            var next = PlayCue(cue, Vector3.zero, null, true);
            if (next == null) return null;
            ConfigureMusic(next, fadeSeconds, owner);
            return next;
        }

        public AudioHandle PlayMusic(AudioClip clip, float volume = 0.6f, bool loop = true, float fadeSeconds = 1f, UnityEngine.Object owner = null)
        {
            if (!clip) { StopMusic(fadeSeconds); return null; }
            if (_music != null && _music.IsValid && _music.Source.clip == clip && !_music.ReleaseAfterFade)
            {
                _musicOwner = owner;
                _music.Source.loop = loop;
                Fade(_music, Mathf.Clamp01(volume), fadeSeconds, false);
                return _music;
            }
            var cue = new AudioCue { Key = "music:" + clip.GetInstanceID(), Clips = new[] { clip }, Channel = AudioChannel.Music,
                Volume = Mathf.Clamp01(volume), Loop = loop, SpatialBlend = 0f, Priority = 32 };
            var next = PlayCue(cue, Vector3.zero, null, true);
            if (next != null) ConfigureMusic(next, fadeSeconds, owner);
            return next;
        }

        private void ConfigureMusic(AudioHandle next, float fadeSeconds, UnityEngine.Object owner)
        {
            if (_music != null) Fade(_music, 0f, fadeSeconds, true);
            _music = next;
            _musicOwner = owner;
            next.Source.spatialBlend = 0f;
            next.Source.volume = fadeSeconds > 0 ? 0f : next.Cue.Volume;
            Fade(next, next.Cue.Volume, fadeSeconds, false);
        }

        public void StopMusic(float fadeSeconds = 1f, UnityEngine.Object owner = null)
        {
            if (owner && _musicOwner != owner) return;
            if (_music != null) Fade(_music, 0f, fadeSeconds, true);
            _music = null;
            _musicOwner = null;
        }

        public void Stop(AudioHandle handle, float fadeSeconds = 0f)
        {
            if (handle == null || !handle.IsValid || !_voices.Contains(handle)) return;
            Fade(handle, 0f, fadeSeconds, true);
        }

        private void Fade(AudioHandle handle, float target, float seconds, bool release)
        {
            if (!handle.IsValid) return;
            handle.FadeFrom = handle.Source.volume;
            handle.FadeTo = target;
            handle.FadeTime = 0;
            handle.FadeDuration = Mathf.Max(0, seconds);
            handle.ReleaseAfterFade = release;
            if (seconds <= 0) { handle.Source.volume = target; if (release) Release(handle); }
        }

        private void Update()
        {
            for (int i = _voices.Count - 1; i >= 0; i--)
            {
                var voice = _voices[i];
                if (!voice.IsValid) { _voices.RemoveAt(i); continue; }
                if ((!voice.Source.isPlaying && !AudioListener.pause) || (voice.Cue.Loop && voice.HasFollow && !voice.Follow))
                { Release(voice); _voices.RemoveAt(i); continue; }
                if (voice.Follow) voice.Source.transform.position = voice.Follow.position;
                if (voice.FadeDuration > 0)
                {
                    voice.FadeTime += Time.unscaledDeltaTime;
                    voice.Source.volume = Mathf.Lerp(voice.FadeFrom, voice.FadeTo, voice.FadeTime / voice.FadeDuration);
                    if (voice.FadeTime >= voice.FadeDuration)
                    {
                        voice.FadeDuration = 0;
                        if (voice.ReleaseAfterFade) { Release(voice); _voices.RemoveAt(i); }
                    }
                }
            }
        }

        private void Release(AudioHandle voice)
        {
            if (voice.Source && Backend) Backend.Release(voice.Source);
            voice.Source = null; // A stale handle can never stop a recycled source.
            if (_music == voice) { _music = null; _musicOwner = null; }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            foreach (var voice in _voices) if (!voice.Music && voice.Scene == scene.handle) Release(voice);
            _lastPlay.Clear();
        }

        public float GetVolume(AudioChannel channel) => _volumes.TryGetValue(channel, out float value) ? value : 1f;
        public float MasterVolume => _master;
        public void SetMasterVolume(float value, bool save = true)
        {
            _master = Mathf.Clamp01(value);
            ApplyVolume(Mix ? Mix.MasterParameter : null, _master);
            if (save && Mix) PlayerPrefs.SetFloat(Mix.PreferencesPrefix + ".Master", _master);
        }
        public void SetVolume(AudioChannel channel, float value, bool save = true)
        {
            value = Mathf.Clamp01(value);
            _volumes[channel] = value;
            ApplyVolume(Mix ? Mix.Find(channel)?.VolumeParameter : null, value);
            if (save && Mix) PlayerPrefs.SetFloat(Mix.PreferencesPrefix + "." + channel, value);
        }
        private void ApplyVolume(string parameter, float value)
        {
            if (Mix && Mix.Mixer && !string.IsNullOrEmpty(parameter))
                Mix.Mixer.SetFloat(parameter, value <= 0.0001f ? -80f : Mathf.Log10(value) * 20f);
        }
        private void OnApplicationPause(bool paused) { if (paused) PlayerPrefs.Save(); }
        private void OnApplicationQuit() => PlayerPrefs.Save();
        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            foreach (var voice in _voices) Release(voice);
            Instance = null;
        }
    }
}
