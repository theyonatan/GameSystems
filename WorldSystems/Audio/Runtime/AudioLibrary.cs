using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameSystems.Audio
{
    public enum AudioChannel { Effects, Music, UI, Ambience, Voice }

    [Serializable]
    public sealed class AudioClipStart
    {
        public AudioClip Clip;
        [Min(0f), Tooltip("Skip the recording's quiet lead-in. Original audio file is unchanged.")]
        public float Seconds;
    }

    [Serializable]
    public sealed class AudioCue
    {
        [Tooltip("Stable identifier used by gameplay. Changing it requires updating callers.")]
        public string Key;
        public string Category = "General";
        public string Name;
        [TextArea] public string Notes;
        [Tooltip("Empty entries are deliberately silent. Add multiple clips for random variations.")]
        public AudioClip[] Clips = Array.Empty<AudioClip>();
        public AudioChannel Channel;
        public bool Muted;
        public List<AudioClipStart> ClipStarts = new();
        [Range(0f, 1f)] public float Volume = 0.8f;
        public Vector2 Pitch = Vector2.one;
        public bool Loop;
        [Range(0f, 1f)] public float SpatialBlend = 1f;
        [Min(0.01f)] public float MinDistance = 3f;
        [Min(0.1f)] public float MaxDistance = 35f;
        [Range(0, 256)] public int Priority = 128;
        [Min(1)] public int MaxInstances = 8;
        [Tooltip("Minimum interval per emitter. Different emitters may play simultaneously.")]
        [Min(0f)] public float Cooldown = 0.03f;

        public bool HasClips => Clips != null && Array.Exists(Clips, clip => clip);

        public float StartSeconds(AudioClip clip)
        {
            var start = ClipStarts?.Find(s => s != null && s.Clip == clip);
            return clip ? Mathf.Clamp(start?.Seconds ?? 0f, 0f, Mathf.Max(0, clip.length - .02f)) : 0f;
        }

        public AudioClip PickClip(AudioClip previous = null)
        {
            if (Clips == null) return null;
            int count = 0;
            foreach (var clip in Clips) if (clip && clip != previous) count++;
            bool excludePrevious = count > 0;
            if (!excludePrevious) foreach (var clip in Clips) if (clip) count++;
            if (count == 0) return null;
            int chosen = UnityEngine.Random.Range(0, count);
            foreach (var clip in Clips)
                if (clip && (!excludePrevious || clip != previous) && chosen-- == 0) return clip;
            return null;
        }
    }

    [CreateAssetMenu(menuName = "GameSystems/Audio/Audio Library")]
    public sealed class AudioLibrary : ScriptableObject
    {
        public List<AudioCue> Cues = new();
        private readonly Dictionary<string, AudioCue> _lookup = new(StringComparer.Ordinal);
        private bool _indexed;

        public AudioCue Find(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (!_indexed) RebuildIndex();
            return _lookup.TryGetValue(key, out var cue) ? cue : null;
        }

        public void RebuildIndex()
        {
            _lookup.Clear();
            foreach (var cue in Cues)
                if (cue != null && !string.IsNullOrWhiteSpace(cue.Key)) _lookup.TryAdd(cue.Key, cue);
            _indexed = true;
        }

        public List<string> ValidateLibrary()
        {
            var issues = new List<string>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var cue in Cues)
            {
                if (cue == null || string.IsNullOrWhiteSpace(cue.Key)) { issues.Add("An entry has no key."); continue; }
                if (!keys.Add(cue.Key)) issues.Add("Duplicate key: " + cue.Key);
                if (cue.Pitch.x <= 0 || cue.Pitch.y < cue.Pitch.x) issues.Add("Invalid pitch range: " + cue.Key);
                if (cue.MaxDistance < cue.MinDistance) issues.Add("Invalid distance range: " + cue.Key);
            }
            return issues;
        }

        private void OnEnable() => _indexed = false;
        private void OnValidate() => _indexed = false;
    }
}
