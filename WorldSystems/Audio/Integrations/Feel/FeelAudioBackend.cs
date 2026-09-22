using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.Audio;

namespace GameSystems.Audio.Feel
{
    [DisallowMultipleComponent]
    public sealed class FeelAudioBackend : AudioPlaybackBackend
    {
        [Min(1)] public int PoolSize = 32;
        private MMSoundManager _manager;
        private bool _ownsManager;

        private void EnsureManager()
        {
            if (_manager) return;
            if (MMSoundManager.HasInstance) _manager = MMSoundManager.Current;
            else
            {
                var root = new GameObject("FEEL Audio Sources");
                root.SetActive(false);
                _manager = root.AddComponent<MMSoundManager>();
                _manager.AudioSourcePoolSize = PoolSize;
                _manager.PoolCanExpand = true;
                root.SetActive(true);
                _ownsManager = true;
            }
        }

        public override AudioSource Play(AudioClip clip, AudioCue cue, Vector3 position, AudioMixerGroup group)
        {
            EnsureManager();
            var options = MMSoundManagerPlayOptions.Default;
            options.MmSoundManagerTrack = cue.Channel switch
            {
                AudioChannel.Music => MMSoundManager.MMSoundManagerTracks.Music,
                AudioChannel.UI => MMSoundManager.MMSoundManagerTracks.UI,
                AudioChannel.Effects => MMSoundManager.MMSoundManagerTracks.Sfx,
                _ => MMSoundManager.MMSoundManagerTracks.Other
            };
            options.AudioGroup = group;
            options.Location = position;
            options.Volume = cue.Volume;
            options.Pitch = Random.Range(Mathf.Max(0.01f, cue.Pitch.x), Mathf.Max(0.01f, cue.Pitch.y));
            options.SpatialBlend = cue.SpatialBlend;
            options.MinDistance = cue.MinDistance;
            options.MaxDistance = Mathf.Max(cue.MinDistance, cue.MaxDistance);
            options.RolloffMode = AudioRolloffMode.Linear;
            options.DopplerLevel = 0;
            options.Priority = cue.Priority;
            // The service owns lifetime/scene cleanup, avoiding FEEL's delayed recycle
            // coroutines touching a source that has already been stopped and reused.
            options.Loop = true;
            options.Persistent = true;
            var source = _manager.PlaySound(clip, options);
            if (source) source.loop = cue.Loop;
            return source;
        }

        public override void Release(AudioSource source)
        {
            if (_manager && source) _manager.FreeSound(source);
        }
        private void OnDestroy() { if (_ownsManager && _manager) Destroy(_manager.gameObject); }
    }
}
