using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameSystems.Audio.Editor
{
    [CustomEditor(typeof(AudioLibrary))]
    public sealed class AudioLibraryEditor : UnityEditor.Editor
    {
        private const string NewCategory = "Create category…";
        private readonly Dictionary<string, bool> _openGroups = new();
        private readonly HashSet<string> _openAdvanced = new();
        private readonly List<Action> _syncCards = new();
        private AudioLibrary Library => (AudioLibrary)target;
        private VisualElement _root, _list;
        private ToolbarSearchField _search;
        private Toggle _missing;
        private Label _status;
        private AudioHandle _preview;
        private bool _editorPreview;
        private static readonly Type AudioUtil = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AudioUtil");
        private static readonly MethodInfo PlayPreview = AudioUtil?.GetMethod("PlayPreviewClip",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null,
            new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
        private static readonly MethodInfo StopEditorPreview = AudioUtil?.GetMethod("StopAllPreviewClips",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        private void OnEnable() => Undo.undoRedoPerformed += OnUndoRedo;
        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            StopPreview();
            _root = null;
        }
        private void OnUndoRedo() { if (_root != null) Populate(); }

        public override VisualElement CreateInspectorGUI()
        {
            _root = new VisualElement();
            _root.AddToClassList("audio-library");
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this))).Replace('\\', '/');
            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(folder + "/AudioLibraryEditor.uss");
            if (styles) _root.styleSheets.Add(styles);
            _root.Add(CreateAddSound());
            _search = new ToolbarSearchField { name = "soundSearch", tooltip = "Search sounds or categories" };
            _root.Add(_search);
            _missing = new Toggle("Only unassigned sounds") { name = "onlyUnassigned" };
            _root.Add(_missing);
            _status = new Label { name = "libraryStatus" };
            _status.AddToClassList("audio-hint");
            _root.Add(_status);
            _list = new VisualElement { name = "soundList" };
            _root.Add(_list);
            _search.RegisterValueChangedCallback(_ => Populate());
            _missing.RegisterValueChangedCallback(_ => Populate());
            _root.TrackSerializedObjectValue(serializedObject, _ => RefreshStatus());
            Populate();
            return _root;
        }

        private VisualElement CreateAddSound()
        {
            var box = new VisualElement();
            box.AddToClassList("audio-add");
            var title = new Label("Add a sound");
            title.AddToClassList("audio-title");
            box.Add(title);
            var hint = new Label("Drop a clip below, choose its channel, then add it.");
            hint.AddToClassList("audio-hint");
            box.Add(hint);
            var clip = ClipField("Clip", "newSoundClip");
            var channel = new EnumField("Channel", AudioChannel.Effects) { name = "newSoundChannel" };
            box.Add(clip);
            box.Add(channel);
            var add = new Button(() =>
            {
                if (!clip.value) return;
                AddSound((AudioClip)clip.value, (AudioChannel)channel.value);
                clip.SetValueWithoutNotify(null);
            }) { text = "Add sound", name = "addSound" };
            add.SetEnabled(false);
            clip.RegisterValueChangedCallback(e => add.SetEnabled(e.newValue != null));
            add.clicked += () => add.SetEnabled(clip.value != null);
            box.Add(add);
            return box;
        }

        private void AddSound(AudioClip clip, AudioChannel channel)
        {
            string stem = "sound." + Regex.Replace(clip.name.ToLowerInvariant(), "[^a-z0-9]+", ".").Trim('.');
            if (stem == "sound.") stem = "sound.clip";
            string key = stem;
            int suffix = 2;
            while (Library.Cues.Any(c => c != null && string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase)))
                key = stem + "." + suffix++;
            var cue = new AudioCue
            {
                Key = key, Name = ObjectNames.NicifyVariableName(clip.name.Replace('_', ' ').Replace('-', ' ')),
                Category = "New sounds", Clips = new[] { clip }, Channel = channel,
                SpatialBlend = channel == AudioChannel.Effects || channel == AudioChannel.Voice ? 1f : 0f,
                Loop = channel == AudioChannel.Music || channel == AudioChannel.Ambience,
                Volume = channel == AudioChannel.Music ? 0.6f : 0.8f,
                Priority = channel == AudioChannel.Music ? 32 : 128
            };
            Edit("Add sound", () => Library.Cues.Insert(0, cue));
            _search.SetValueWithoutNotify("");
            _missing.SetValueWithoutNotify(false);
            _openGroups[cue.Category] = true;
            Populate();
        }

        private void Populate()
        {
            serializedObject.Update();
            _list.Unbind();
            _list.Clear();
            _syncCards.Clear();
            var matches = Library.Cues.Select((cue, index) => new { Cue = cue, Index = index })
                .Where(item => item.Cue != null && (!_missing.value || !item.Cue.HasClips))
                .Where(item => string.IsNullOrEmpty(_search.value) ||
                    (item.Cue.Category + " " + item.Cue.Name + " " + item.Cue.Key).IndexOf(_search.value, StringComparison.OrdinalIgnoreCase) >= 0)
                .GroupBy(item => string.IsNullOrWhiteSpace(item.Cue.Category) ? "General" : item.Cue.Category);
            foreach (var items in matches)
            {
                string category = items.Key;
                bool expanded = _openGroups.TryGetValue(category, out bool open) ? open : category == "Ready to assign" || category == "New sounds";
                var group = new Foldout { text = category, value = expanded || !string.IsNullOrEmpty(_search.value) };
                bool loaded = false;
                void LoadCards()
                {
                    if (loaded) return;
                    loaded = true;
                    serializedObject.Update();
                    var array = serializedObject.FindProperty("Cues");
                    foreach (var item in items) group.Add(CreateSoundCard(item.Cue, array.GetArrayElementAtIndex(item.Index).Copy()));
                    RefreshStatus();
                }
                group.RegisterValueChangedCallback(e =>
                {
                    if (e.target != group) return;
                    _openGroups[category] = e.newValue;
                    if (e.newValue) LoadCards();
                });
                _list.Add(group);
                if (group.value) LoadCards();
            }
            if (_list.childCount == 0) _list.Add(new Label("No sounds match this filter."));
            RefreshStatus();
        }
        private VisualElement CreateSoundCard(AudioCue cue, SerializedProperty property)
        {
            var card = new VisualElement { name = "sound:" + cue.Key };
            card.AddToClassList("audio-card");
            var name = new TextField("Name") { value = cue.Name, isDelayed = true, name = "soundName" };
            name.RegisterValueChangedCallback(e => Edit("Rename sound", () => cue.Name = e.newValue));
            card.Add(name);
            var clip = ClipField("Clip", "mainClip");
            clip.SetValueWithoutNotify(cue.Clips != null && cue.Clips.Length > 0 ? cue.Clips[0] : null);
            clip.RegisterValueChangedCallback(e => Edit("Assign sound clip", () =>
            {
                var clips = cue.Clips?.ToArray() ?? Array.Empty<AudioClip>();
                if (clips.Length == 0) clips = new AudioClip[1];
                clips[0] = (AudioClip)e.newValue;
                cue.Clips = clips;
            }));
            card.Add(clip);
            var channel = new EnumField("Channel", cue.Channel) { name = "soundChannel" };
            channel.BindProperty(property.FindPropertyRelative("Channel"));
            card.Add(channel);
            var volume = new Slider("Volume", 0f, 1f) { showInputField = true, name = "soundVolume" };
            volume.BindProperty(property.FindPropertyRelative("Volume"));
            card.Add(volume);
            var controls = new VisualElement();
            controls.AddToClassList("audio-row");
            var preview = new Button(() => Preview(cue)) { text = "▶ Preview", name = "previewSound",
                tooltip = "Edit Mode auditions the raw recording. Play Mode uses library volume, pitch and mixer settings." };
            controls.Add(preview);
            controls.Add(new Button(StopPreview) { text = "Stop", name = "stopPreview" });
            card.Add(controls);
            card.Add(CategoryPicker(cue));

            var advanced = new Foldout { text = "Advanced", value = _openAdvanced.Contains(cue.Key), name = "advanced" };
            advanced.AddToClassList("audio-advanced");
            advanced.RegisterValueChangedCallback(e =>
            {
                if (e.target != advanced) return;
                if (e.newValue) _openAdvanced.Add(cue.Key); else _openAdvanced.Remove(cue.Key);
                if (e.newValue) LoadAdvanced();
            });
            void LoadAdvanced()
            {
                if (advanced.contentContainer.childCount > 0) return;
                foreach (var field in new[] { "Muted", "Clips", "ClipStarts", "Pitch", "Loop", "SpatialBlend", "MinDistance", "MaxDistance", "Priority", "MaxInstances", "Cooldown", "Notes" })
                {
                    var input = new PropertyField(property.FindPropertyRelative(field), field == "Clips" ? "All clips / variations (includes main clip)" : null);
                    input.Bind(serializedObject);
                    advanced.Add(input);
                }
                var key = new TextField("Internal key") { value = cue.Key, isReadOnly = true,
                    tooltip = "Generated once. Renaming or moving a sound keeps existing gameplay connections intact." };
                advanced.Add(key);
                var actions = new VisualElement();
                actions.AddToClassList("audio-row");
                actions.Add(new Button(() => EditorGUIUtility.systemCopyBuffer = cue.Key) { text = "Copy key" });
                actions.Add(new Button(() =>
                {
                    StopPreview();
                    Edit("Remove sound", () => Library.Cues.Remove(cue));
                    Populate();
                }) { text = "Remove sound", tooltip = "Removing a connected sound makes that event silent. Undo restores it." });
                advanced.Add(actions);
            }
            if (advanced.value) LoadAdvanced();
            card.Add(advanced);
            _syncCards.Add(() =>
            {
                name.SetValueWithoutNotify(cue.Name);
                clip.SetValueWithoutNotify(cue.Clips != null && cue.Clips.Length > 0 ? cue.Clips[0] : null);
                preview.SetEnabled(cue.HasClips);
            });
            return card;
        }

        private VisualElement CategoryPicker(AudioCue cue)
        {
            var container = new VisualElement();
            string current = string.IsNullOrWhiteSpace(cue.Category) ? "General" : cue.Category;
            var choices = Library.Cues.Where(c => c != null).Select(c => string.IsNullOrWhiteSpace(c.Category) ? "General" : c.Category)
                .Distinct().OrderBy(c => c).ToList();
            choices.Add(NewCategory);
            var dropdown = new DropdownField("Category", choices, current) { name = "soundCategory" };
            var create = new TextField("New category") { isDelayed = true, name = "newCategory" };
            create.AddToClassList("audio-hidden");
            void Move(string category)
            {
                Edit("Move sound to category", () => cue.Category = category);
                _openGroups[category] = true;
                Populate();
            }
            dropdown.RegisterValueChangedCallback(e =>
            {
                if (e.newValue == NewCategory)
                {
                    dropdown.SetValueWithoutNotify(current);
                    create.RemoveFromClassList("audio-hidden");
                    create.Focus();
                }
                else Move(e.newValue);
            });
            create.RegisterValueChangedCallback(e =>
            {
                string category = e.newValue.Trim();
                if (!string.IsNullOrEmpty(category) && category != NewCategory) Move(category);
            });
            container.Add(dropdown);
            container.Add(create);
            return container;
        }

        private static ObjectField ClipField(string label, string elementName)
            => new(label) { objectType = typeof(AudioClip), allowSceneObjects = false, name = elementName };

        private void Edit(string label, Action change)
        {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(Library, label);
            change();
            Library.RebuildIndex();
            EditorUtility.SetDirty(Library);
            serializedObject.Update();
            RefreshStatus();
        }
        private void RefreshStatus()
        {
            if (_status == null) return;
            var errors = Library.ValidateLibrary();
            _status.text = $"{Library.Cues.Count(c => c != null && c.HasClips)} / {Library.Cues.Count} sounds assigned"
                + (errors.Count == 0 ? "" : "\n" + string.Join("\n", errors));
            foreach (var sync in _syncCards) sync();
        }
        private void Preview(AudioCue cue)
        {
            StopPreview();
            if (Application.isPlaying && AudioService.Instance)
            {
                var listener = UnityEngine.Object.FindFirstObjectByType<AudioListener>();
                Vector3 position = listener ? listener.transform.position : Vector3.zero;
                _preview = cue.Channel == AudioChannel.Music
                    ? AudioService.Instance.PlayMusic(cue.Key, 0f, this)
                    : AudioService.Instance.Play(cue.Key, position);
            }
            else if (cue.HasClips && PlayPreview != null)
            {
                PlayPreview.Invoke(null, new object[] { cue.PickClip(), 0, false });
                _editorPreview = true;
            }
        }
        private void StopPreview()
        {
            if (_preview != null && AudioService.Instance) AudioService.Instance.Stop(_preview);
            _preview = null;
            if (_editorPreview) StopEditorPreview?.Invoke(null, null);
            _editorPreview = false;
        }
    }
}


