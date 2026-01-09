#if FISHNET
using FishNet.Connection;
using FishNet.Object;
#endif
using System;
using UnityEngine;

public class ExtensionMultiplayer : NetworkBehaviour, IPlayerBehavior
{
    [TargetRpc]
    public void SetupTPSPlayerRpc(NetworkConnection conn)
    {
        // give authority
        var player = GetComponent<Player>();
        player.SetAuthority(true);
        player.EnablePlayerBehaviors();
        
        // Load State
        player.Awake();
        player.OnEnable();
        player.Start();
        
        player.SwapPlayerState<cc_tpState, TP_CameraState>();
    }
}
