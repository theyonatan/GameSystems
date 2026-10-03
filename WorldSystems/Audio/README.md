# GameSystems Audio

Local audio library and playback service. The core has no game or networking dependencies; `Integrations/Feel` requires FEEL's `MoreMountains.Tools` assembly and uses `MMSoundManager` pooling. Omit that integration and implement `AudioPlaybackBackend` if another game uses a different player.

## Assigning sounds

Create **GameSystems → Audio → Audio Library** from the Assets/Create menu. At the top of its Inspector, drop a clip into **Add a sound**, choose its channel and click **Add sound**. Names and unique keys are generated automatically; new entries appear under **New sounds**. Adding an entry does not connect a new gameplay event.

To replace a recording, drag onto the existing sound's **Clip** field. Channel, Volume and Preview are immediately available. Rename freely; the internal key stays fixed. The Category dropdown moves a sound or creates a new category. **Advanced** holds variations, pitch, looping, distance, voice limits, cooldown, notes and the key. All edits support Undo; replacing the main clip preserves other variations. Empty entries stay silent.

**Preview** auditions the raw recording outside Play Mode. In Play Mode with the audio service running, it uses the library's volume, pitch and mixer. **Stop** ends the preview. Search and the unassigned filter help navigate larger catalogs.

## Another game

1. Create that game's own library, AudioMixer and Audio Mix Settings assets. Bind each channel to its mixer group and exposed volume parameter; use a unique preferences prefix.
2. Create one root prefab with `AudioService` and `FeelAudioBackend`, assigning the library, mix and backend. Instantiate it once at startup; it survives scene changes and rejects duplicates. Keep exactly one active camera AudioListener per client.
3. Play local sounds with `AudioService.Instance.Play("cue.key", position, optionalEmitter)`. The emitter supplies following and cooldown identity. Keep the returned handle to stop a loop or cancel a sound with `Stop(handle)`. For ordered or network-selected variations, use `PlayVariation(key, zeroBasedIndex, position, emitter)`; invalid/empty slots stay silent and normal mixer, pooling, cooldown and voice limits still apply.
4. Add `SceneMusic` to scenes needing music. Assign the audio prefab for direct scene entry, then a music key **or** a direct clip. Key playback uses the library's volume/loop settings; direct clips use the component settings. Tracks crossfade and repeated requests do not restart them.

`AudioVolumeControl.SetVolume(float)` binds a UI slider to Master or a channel; values are 0–1 and saved per game. `AudioSourceRouting` routes an existing source (for example voice) through the same mixer.

## Multiplayer contract

Playback is local only. The game decides recipients and issues one playback request per event per client. Shared events should use one unbuffered observer RPC, without an additional server/local playback path. Private UI stays local. Predicted owner audio requires excluding that owner from confirmation playback. Do not attach the service to every player or add a NetworkObject to it.

The service handles source recycling, scene-owned effects, destroyed loop emitters and music crossfades. It does not synchronize ongoing loops for late joiners; games must restore those from replicated state when they add them.

## Editor audio review

**Tools → GameSystems → Audio → Audio Review** works with any AudioLibrary. Play the
real scene; select a captured cue to audition recording/volume/mute and per-clip start
offset. Save explicitly or discard. Solo is session-only; flags and CSV help review.
AudioDiagnostics and all audition overrides are `UNITY_EDITOR` only. Action markers
are optional and must also be behind that define. Engine timing does not measure
speaker latency; onset analysis is a quiet-lead-in estimate, not an automatic fix.
Runtime AudioCue.Muted and ClipStarts are reusable; FEEL seeks before Play. Existing
entries default to unmuted with zero offset. Music/loops should keep zero offset.
