#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameSystems.Audio
{
    /// <summary>Optional local instrumentation. Times describe engine calls, not speaker output.</summary>
    public static class AudioDiagnostics
    {
        [Serializable]
        public struct Record
        {
            public string Key, Emitter, Status, Action;
            public AudioClip Clip;
            public double RequestedAt, DispatchedAt, DspTime, ActionAt;
            public float StartSeconds, Pitch;
            public Record(AudioCue cue, AudioClip clip, Transform emitter, string status, double requested,
                string action, double actionAt, float pitch)
            {
                Key = cue.Key; Clip = clip; Emitter = emitter ? emitter.name : "World";
                Status = status; RequestedAt = requested; DispatchedAt = Time.realtimeSinceStartupAsDouble;
                DspTime = AudioSettings.dspTime; Action = action; ActionAt = actionAt;
                StartSeconds = cue.StartSeconds(clip); Pitch = pitch;
            }
        }
        public static event Action<Record> Recorded;
        public static readonly Dictionary<string, AudioCue> Overrides = new();
        public static string SoloKey;
        private static readonly Dictionary<(string, int), (string, double)> Actions = new();

        public static void MarkAction(string key, Transform emitter, string label)
        {
            if (Recorded == null) return;
            Actions[(key, emitter ? emitter.GetInstanceID() : 0)] = (label, Time.realtimeSinceStartupAsDouble);
        }
        internal static AudioCue Resolve(AudioCue cue) => cue != null && Overrides.TryGetValue(cue.Key, out var replacement) ? replacement : cue;
        internal static void Report(AudioCue cue, AudioClip clip, Transform emitter, string status, double requested, float pitch = 1)
        {
            if (Recorded == null) return;
            var id = (cue.Key, emitter ? emitter.GetInstanceID() : 0);
            bool found = Actions.Remove(id, out var action);
            Recorded(new Record(cue, clip, emitter, status, requested, found ? action.Item1 : null, found ? action.Item2 : double.NaN, pitch));
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { Actions.Clear(); Overrides.Clear(); SoloKey = null; Recorded = null; }
    }
}
#endif
