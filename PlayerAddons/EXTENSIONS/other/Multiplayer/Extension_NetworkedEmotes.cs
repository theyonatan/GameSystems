using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registers and plays owner-triggered emotes.
///
/// Number keys map to the serialized list by order:
/// 1 = element 0, 2 = element 1, and so on.
///
/// NetworkAnimationRelay automatically forwards animations successfully
/// played through AnimationsManager, so this extension needs no emote RPCs.
/// </summary>
[DisallowMultipleComponent]
public sealed class Extension_NetworkedEmotes : MonoBehaviour, IPlayerBehavior
{
    [System.Serializable]
    private class Emote
    {
        public string animationName;
        public bool loops;
        public bool interruptByMovement = true;
    }
    
    [Header("Emotes")]
    [Tooltip("Animator state names in number-key order. Supports keys 1 through 9.")]
    [SerializeField] private List<Emote> emotes = new();

    [SerializeField, Min(0f)] private float entryCrossfade = 0.15f;

    private Player _player;
    private InputDirector _inputDirector;
    private AnimationsManager _animationsManager;
    private bool _subscribed;
    private Emote _activeEmote;

    public void OnEnablePlayer()
    {
        _player = GetComponent<Player>();
        if (!_player.HasAuthority)
            return;

        _inputDirector = GetComponent<InputDirector>();
        _animationsManager = GetComponent<AnimationsManager>();

        if (!_inputDirector || !_animationsManager)
        {
            Debug.LogError(
                $"[{nameof(Extension_NetworkedEmotes)}] Missing InputDirector or AnimationsManager on {name}.",
                this);
            return;
        }

        RegisterAnimations();

        if (!_subscribed)
        {
            _inputDirector.OnEmotePressed += PlayEmote;
            _inputDirector.OnPlayerMoved += OnPlayerMoved;
            _animationsManager.AnimationPlayed += OnAnimationPlayed;
            _subscribed = true;
        }
    }

    public void OnDisablePlayer()
    {
        Unsubscribe();
    }

    public void OnDestroyPlayer()
    {
        Unsubscribe();
    }

    private void RegisterAnimations()
    {
        var builder = new AnimationsManager.Builder();
        var registeredNames = new HashSet<string>();

        foreach (Emote emote in emotes)
        {
            if (string.IsNullOrWhiteSpace(emote.animationName))
                continue;

            if (!registeredNames.Add(emote.animationName))
                continue;

            builder.AddAnimation(
                emote.animationName,
                lockLayer: false,
                loops: emote.loops,
                entryCrossfade: entryCrossfade);
        }

        builder.Build(_animationsManager);
    }

    private void PlayEmote(int index)
    {
        if (!_player || !_player.HasAuthority)
            return;

        if (index < 0 || index >= emotes.Count)
            return;

        Emote emote = emotes[index];
        string animationName = emote.animationName;

        if (string.IsNullOrWhiteSpace(animationName))
            return;
        
        // AnimatorCoder rejects this automatically while another animation
        // has locked layer 0, such as an attack.
        _activeEmote = emote;
        _animationsManager.Play(
            animationName,
            layer: 0,
            customCrossfade: entryCrossfade,
            reason: "PlayerEmote");
    }
    
    private void OnPlayerMoved(Vector2 movement)
    {
        if (_activeEmote == null)
            return;

        if (!_activeEmote.interruptByMovement)
            return;

        if (movement.sqrMagnitude <= 0.001f)
            return;

        _activeEmote = null;
        _animationsManager.SetLocked(false, 0);
        _animationsManager.PlayDefaultAnimation();
    }

    private void OnAnimationPlayed(int stateHash, int layer, float crossfade)
    {
        if (layer != 0 || _activeEmote == null)
            return;

        int activeEmoteHash = Animator.StringToHash(_activeEmote.animationName);

        if (stateHash != activeEmoteHash)
            _activeEmote = null;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || !_inputDirector)
            return;
        
        _inputDirector.OnEmotePressed -= PlayEmote;
        _inputDirector.OnPlayerMoved -= OnPlayerMoved;
        _animationsManager.AnimationPlayed -= OnAnimationPlayed;
        
        _subscribed = false;
        _activeEmote = null;
    }
}
