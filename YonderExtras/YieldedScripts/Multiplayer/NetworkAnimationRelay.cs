using FishNet.Object;
using UnityEngine;

/// <summary>
/// FishNet adapter for code-driven animations.
///
/// The owning client reports successful animation playback to the server.
/// The server relays it to observers, which reproduce only the visual
/// Animator state without running AnimatorCoder gameplay logic or callbacks.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AnimationsManager))]
public sealed class NetworkAnimationRelay : NetworkBehaviour
{
    [SerializeField] private Animator visualAnimator;

    private AnimationsManager _animationsManager;

    private void Awake()
    {
        _animationsManager = GetComponent<AnimationsManager>();
        FindVisualAnimator();
    }
    
    public override void OnStartServer()
    {
        base.OnStartServer();

        // Server-owned objects such as enemies have no owning client.
        if (!Owner.IsValid)
            _animationsManager.AnimationTransitionPlayed += OnServerAnimationPlayed;
    }

    public override void OnStopServer()
    {
        if (_animationsManager != null)
            _animationsManager.AnimationTransitionPlayed -= OnServerAnimationPlayed;

        base.OnStopServer();
    }
    
    private void OnServerAnimationPlayed(
        int stateHash,
        int layer,
        float crossfade, bool fixedTime)
    {
        RelayAnimationObserversRpc(stateHash, layer, crossfade, fixedTime);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        FindVisualAnimator();

        if (IsOwner)
            _animationsManager.AnimationTransitionPlayed += OnOwnerAnimationPlayed;
    }

    public override void OnStopClient()
    {
        if (_animationsManager != null)
            _animationsManager.AnimationTransitionPlayed -= OnOwnerAnimationPlayed;

        base.OnStopClient();
    }

    private void OnOwnerAnimationPlayed(int stateHash, int layer, float crossfade, bool fixedTime)
    {
        RelayAnimationServerRpc(stateHash, layer, crossfade, fixedTime);
    }

    [ServerRpc(RequireOwnership = true)]
    private void RelayAnimationServerRpc(int stateHash, int layer, float crossfade, bool fixedTime)
    {
        RelayAnimationObserversRpc(stateHash, layer, crossfade, fixedTime);
    }

    [ObserversRpc(ExcludeOwner = true)]
    private void RelayAnimationObserversRpc(int stateHash, int layer, float crossfade, bool fixedTime)
    {
        // On a host, the server already played this animation locally.
        if (IsServerStarted && !Owner.IsValid)
            return;
        
        if (!FindVisualAnimator())
            return;

        if (layer < 0 || layer >= visualAnimator.layerCount)
        {
            Debug.LogWarning(
                $"[NetworkAnimationRelay] Ignoring layer {layer}; " +
                $"Animator has {visualAnimator.layerCount} layers.",
                this);
            return;
        }

        if (fixedTime)
            visualAnimator.CrossFadeInFixedTime(stateHash, Mathf.Max(0f, crossfade), layer);
        else
            visualAnimator.CrossFade(stateHash, Mathf.Max(0f, crossfade), layer);
    }

    private bool FindVisualAnimator()
    {
        if (visualAnimator)
            return true;

        visualAnimator = GetComponentInChildren<Animator>(true);

        if (!visualAnimator)
            Debug.LogError(
                "[NetworkAnimationRelay] No child Animator was found.",
                this);

        return visualAnimator;
    }
}
