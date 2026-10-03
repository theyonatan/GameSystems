using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameSystems.Audio.Editor
{
    public sealed class AudioReviewWindow : EditorWindow
    {
        [SerializeField] private AudioLibrary library;
        [SerializeField] private AudioLibrary working;
        [SerializeField] private List<string> changed = new();
        [SerializeField] private string selectedKey;
        [SerializeField] private List<AudioDiagnostics.Record> records = new();
        private readonly Dictionary<AudioClip, float> onsets = new();
        private DropdownField sound;
        private VisualElement details;
        private Label status;
        private ListView events;
        private bool capture = true;
        private int variant;

        [MenuItem("Tools/GameSystems/Audio/Audio Review")]
        public static void Open() => GetWindow<AudioReviewWindow>("Audio Review");
        public static void Open(AudioLibrary asset)
        {
            var window = GetWindow<AudioReviewWindow>("Audio Review");
            if (window.library != asset) window.UseLibrary(asset);
        }
        private void OnEnable()
        {
            minSize = new Vector2(620, 500);
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += ModeChanged;
            AudioDiagnostics.Recorded -= Record;
            AudioDiagnostics.Recorded += Record;
        }
        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= ModeChanged;
            AudioDiagnostics.Recorded -= Record;
            ClearOverrides();
        }
        private void ModeChanged(PlayModeStateChange state)
        {
            AudioDiagnostics.Recorded -= Record;
            AudioDiagnostics.Recorded += Record;
            if (state == PlayModeStateChange.EnteredEditMode) AudioDiagnostics.SoloKey = null;
            ApplyOverrides();
        }
        private void Tick()
        {
            if (!library && AudioService.Instance) UseLibrary(AudioService.Instance.Library);
            ApplyOverrides();
        }
        private void ClearOverrides()
        {
            foreach (string key in changed) AudioDiagnostics.Overrides.Remove(key);
            AudioDiagnostics.SoloKey = null;
        }
        private void ApplyOverrides()
        {
            if (!working) return;
            foreach (string key in changed) AudioDiagnostics.Overrides[key] = working.Find(key);
        }
        private void UseLibrary(AudioLibrary asset)
        {
            ClearOverrides();
            if (working) DestroyImmediate(working);
            library = asset;
            working = asset ? Instantiate(asset) : null;
            if (working) working.hideFlags = HideFlags.HideAndDontSave;
            changed.Clear(); variant = 0;
            CreateGUI();
        }
        public void CreateGUI()
        {
            rootVisualElement.Clear();
            var scroll = new ScrollView(); rootVisualElement.Add(scroll);
            var root = scroll.contentContainer; root.AddToClassList("audio-review");
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this))).Replace('\\','/') + "/AudioReviewWindow.uss");
            if (style) root.styleSheets.Add(style);
            var hint = new Label("Play the game normally. Select a captured event to tune it, then repeat the action. Changes are temporary until Save changes. Close this window to stop audition overrides."); hint.AddToClassList("hint"); root.Add(hint);
            var field = new ObjectField("Library") { objectType = typeof(AudioLibrary), allowSceneObjects = false, value = library };
            field.RegisterValueChangedCallback(e => UseLibrary((AudioLibrary)e.newValue));
            field.SetEnabled(changed.Count == 0); root.Add(field);
            if (!library) return;
            if (!working) { working = Instantiate(library); working.hideFlags = HideFlags.HideAndDontSave; }
            var cues = working.Cues.Where(c => c != null).ToList();
            int selection = Math.Max(0, cues.FindIndex(c => c.Key == selectedKey));
            sound = new DropdownField("Sound", cues.Select(c => c.Category + " / " + c.Name).ToList(), selection);
            sound.RegisterValueChangedCallback(_ => { selectedKey = cues[sound.index].Key; variant = 0; ShowCue(); }); root.Add(sound);
            selectedKey = cues.Count > 0 ? cues[selection].Key : null;
            details = new VisualElement(); root.Add(details); ShowCue();
            var row = new VisualElement(); row.AddToClassList("row");
            row.Add(new Button(SaveChanges) { text = "Save changes" });
            row.Add(new Button(() => UseLibrary(library)) { text = "Discard changes" });
            root.Add(row);
            var recording = new Toggle("Capture gameplay events") { value = capture };
            recording.RegisterValueChangedCallback(e => capture = e.newValue); root.Add(recording);
            events = new ListView(records, 26, () => new Label(), (element, index) =>
            {
                var entry = records[index];
                string action = double.IsNaN(entry.ActionAt) ? "unmarked" : $"{(entry.RequestedAt-entry.ActionAt)*1000:0.0} ms";
                ((Label)element).text = $"{entry.RequestedAt:0.00}s | {entry.Status} | {library.Find(entry.Key)?.Name ?? entry.Key} | {entry.Emitter} | action→request {action}";
                element.tooltip = entry.Key;
            });
            events.AddToClassList("events"); events.selectionChanged += items =>
            {
                if (items.FirstOrDefault() is not AudioDiagnostics.Record entry) return;
                int index = cues.FindIndex(c => c.Key == entry.Key);
                if (index >= 0) sound.index = index;
                variant = Math.Max(0, Array.IndexOf(working.Find(entry.Key)?.Clips ?? Array.Empty<AudioClip>(), entry.Clip));
                ShowCue();
                float onset = entry.Clip ? Onset(entry.Clip) : -1;
                status.text = $"{entry.Action ?? "No action marker"} • {entry.Clip?.name ?? entry.Status}\n" +
                    $"Request → backend Play: {(entry.DispatchedAt-entry.RequestedAt)*1000:0.0} ms. DSP clock: {entry.DspTime:0.000}s.\n" +
                    (onset < 0 ? "Onset estimate unavailable." : $"Recording onset ≈ {onset*1000:0} ms; skipped {entry.StartSeconds*1000:0} ms; remaining ≈ {Mathf.Max(0,onset-entry.StartSeconds)*1000/Mathf.Max(.01f,entry.Pitch):0} ms.") +
                    "\nOnset uses sustained −40 dBFS. Estimates exclude device buffers and network transit before the local action marker.";
            }; root.Add(events);
            var actions = new VisualElement(); actions.AddToClassList("row");
            actions.Add(new Button(() => { records.Clear(); events.Rebuild(); }) { text = "Clear capture" });
            actions.Add(new Button(Export) { text = "Export capture + flags" }); root.Add(actions);
            status = new Label("Ready. Jump, land, or release Hook to record marked timing. Other sounds are captured without action markers."); status.AddToClassList("status"); root.Add(status);
        }
        private void ShowCue()
        {
            details.Clear(); var cue = working.Find(selectedKey); if (cue == null) return;
            void Dirty() { if (!changed.Contains(cue.Key)) changed.Add(cue.Key); ApplyOverrides(); }
            var muted = new Toggle("Muted") { value = cue.Muted }; muted.RegisterValueChangedCallback(e => { cue.Muted = e.newValue; Dirty(); }); details.Add(muted);
            var solo = new Toggle("Solo for this session") { value = AudioDiagnostics.SoloKey == cue.Key };
            solo.RegisterValueChangedCallback(e => AudioDiagnostics.SoloKey = e.newValue ? cue.Key : null); details.Add(solo);
            var volume = new Slider("Volume", 0, 1) { showInputField = true, value = cue.Volume };
            volume.RegisterValueChangedCallback(e => { cue.Volume = e.newValue; Dirty(); }); details.Add(volume);
            if (cue.Clips == null || cue.Clips.Length == 0) cue.Clips = new AudioClip[1];
            variant = Mathf.Clamp(variant, 0, cue.Clips.Length-1);
            var variations = new DropdownField("Variation", cue.Clips.Select((c,i) => $"{i+1}: {(c ? c.name : "Unassigned")}").ToList(), variant);
            variations.RegisterValueChangedCallback(_ => { variant = variations.index; ShowCue(); }); details.Add(variations);
            var clip = cue.Clips[variant];
            var replacement = new ObjectField("Recording") { objectType = typeof(AudioClip), allowSceneObjects = false, value = clip };
            replacement.RegisterValueChangedCallback(e => { cue.Clips[variant] = (AudioClip)e.newValue; Dirty(); ShowCue(); }); details.Add(replacement);
            var trim = new FloatField("Start offset (ms)") { value = cue.StartSeconds(clip)*1000, isDelayed = true };
            trim.SetEnabled(clip && !cue.Loop);
            trim.RegisterValueChangedCallback(e =>
            {
                if (!clip) return;
                cue.ClipStarts ??= new(); var start = cue.ClipStarts.Find(s => s.Clip == clip);
                if (start == null) { start = new AudioClipStart { Clip = clip }; cue.ClipStarts.Add(start); }
                start.Seconds = Mathf.Clamp(e.newValue/1000, 0, Mathf.Max(0, clip.length-.02f));
                trim.SetValueWithoutNotify(start.Seconds*1000); Dirty();
            }); details.Add(trim);
            var estimate = new Label("Analyze checks the first two seconds for a sustained onset. Listen before saving."); estimate.AddToClassList("hint"); details.Add(estimate);
            var analyze = new Button(() =>
            {
                float onset = Onset(clip);
                estimate.text = onset < 0 ? "Cannot estimate this recording. Set the offset manually." : $"Onset ≈ {onset*1000:0} ms; suggested start ≈ {Mathf.Max(0,onset-.015f)*1000:0} ms (15 ms attack margin).";
            }) { text = "Analyze quiet lead-in" }; analyze.SetEnabled(clip && !cue.Loop); details.Add(analyze);
            string pref = "AudioReview." + Application.dataPath + "." + cue.Key;
            var flag = new DropdownField("Review flag", new List<string>{"None", "Late", "Too frequent", "Replace", "Approved"}, EditorPrefs.GetString(pref, "None"));
            flag.RegisterValueChangedCallback(e => EditorPrefs.SetString(pref,e.newValue)); details.Add(flag);
        }
        private void Record(AudioDiagnostics.Record entry)
        {
            if (!capture) return;
            if (records.Count == 500) records.RemoveAt(0);
            records.Add(entry); events?.RefreshItems();
        }
        public void SaveChanges()
        {
            if (!library || !working || changed.Count == 0) return;
            Undo.RecordObject(library,"Save audio review changes");
            foreach (string key in changed)
            {
                var from = working.Find(key); var to = library.Find(key); if (from == null || to == null) continue;
                to.Muted = from.Muted; to.Volume = from.Volume; to.Clips = from.Clips.ToArray();
                to.ClipStarts = from.ClipStarts.Select(s => new AudioClipStart { Clip = s.Clip, Seconds = s.Seconds }).ToList();
            }
            EditorUtility.SetDirty(library); AssetDatabase.SaveAssetIfDirty(library);
            ClearOverrides(); changed.Clear(); status.text = "Saved to the library. Original audio files are unchanged.";
        }
        public static float EstimateOnset(AudioClip clip)
        {
            if (!clip || clip.loadType != AudioClipLoadType.DecompressOnLoad || !clip.LoadAudioData()) return -1;
            int frames = Math.Min(clip.samples, clip.frequency*2);
            var data = new float[frames*clip.channels]; if (!clip.GetData(data,0)) return -1;
            int bin = Math.Max(1,clip.frequency/100)*clip.channels; int consecutive = 0;
            for (int i=0;i+bin<=data.Length;i+=bin)
            {
                double sum=0; for (int j=i;j<i+bin;j++) sum+=data[j]*data[j];
                consecutive = Math.Sqrt(sum/bin)>=.01 ? consecutive+1 : 0;
                if (consecutive==2) return (float)(i-bin)/clip.channels/clip.frequency;
            }
            return -1;
        }
        private float Onset(AudioClip clip)
        {
            if (!clip) return -1;
            if (!onsets.TryGetValue(clip,out float value)) onsets[clip] = value = EstimateOnset(clip);
            return value;
        }
        private void Export()
        {
            string folder = Path.GetFullPath("Library/AudioReview"); Directory.CreateDirectory(folder);
            string path=Path.Combine(folder,DateTime.Now.ToString("yyyyMMdd-HHmmss")+".csv");
            string Cell(string value) => "\""+(value??"").Replace("\"","\"\"")+"\"";
            var lines = new List<string>{"Cue,Clip,Emitter,Status,Action,ActionToRequestMs,RequestToPlayMs,SkippedMs,OnsetMs,Flag"};
            foreach(var r in records) lines.Add(string.Join(",",new[]{Cell(r.Key),Cell(r.Clip?r.Clip.name:""),Cell(r.Emitter),Cell(r.Status),Cell(r.Action),
                Cell(double.IsNaN(r.ActionAt)?"":((r.RequestedAt-r.ActionAt)*1000).ToString("F2",System.Globalization.CultureInfo.InvariantCulture)),
                Cell(((r.DispatchedAt-r.RequestedAt)*1000).ToString("F2",System.Globalization.CultureInfo.InvariantCulture)), Cell((r.StartSeconds*1000).ToString("F0")),Cell((Onset(r.Clip)*1000).ToString("F0")),Cell(EditorPrefs.GetString("AudioReview."+Application.dataPath+"."+r.Key,"None"))}));
            File.WriteAllLines(path,lines); status.text="Exported "+path;
        }
    }
}
