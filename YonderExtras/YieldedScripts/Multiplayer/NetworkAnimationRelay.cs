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

    public override void OnStartClient()
    {
        base.OnStartClient();

        FindVisualAnimator();

        if (IsOwner)
            _animationsManager.AnimationPlayed += OnOwnerAnimationPlayed;
    }

    public override void OnStopClient()
    {
        if (_animationsManager != null)
            _animationsManager.AnimationPlayed -= OnOwnerAnimationPlayed;

        base.OnStopClient();
    }

    private void OnOwnerAnimationPlayed(int stateHash, int layer, float crossfade)
    {
        RelayAnimationServerRpc(stateHash, layer, crossfade);
    }

    [ServerRpc(RequireOwnership = true)]
    private void RelayAnimationServerRpc(int stateHash, int layer, float crossfade)
    {
        RelayAnimationObserversRpc(stateHash, layer, crossfade);
    }

    [ObserversRpc(ExcludeOwner = true)]
    private void RelayAnimationObserversRpc(int stateHash, int layer, float crossfade)
    {
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
