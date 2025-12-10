#if NETCODE
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Regular stories control the game. this is a host only version,
/// Responsible for starting the different connecting players.
/// And, this is the story of the server.
/// </summary>
public class MultiplayerStoryExample : NetworkBehaviour
{
    // this should change for each game story!
    [SerializeField] GameObject playerPrefab;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        SpawnPlayers(); // Spawn the already connected players

        NetworkManager.Singleton.OnClientConnectedCallback += Singleton_OnClientConnectedCallback;
    }

    private void Singleton_OnClientConnectedCallback(ulong clientId)
    {
        if (!IsServer) return;

        SpawnPlayer(clientId);
    }

    void SpawnPlayers()
    {
        // called on host, spawns 1 prefab per client
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            SpawnPlayer(clientId);
        }
    }

    void SpawnPlayer(ulong clientId)
    {
        GameObject player = Instantiate(playerPrefab, new Vector3(0f, 2f, 0f), Quaternion.identity);

        var netObj = player.GetComponent<NetworkObject>();
        netObj.SpawnAsPlayerObject(clientId);

        Debug.Log($"[Netcode] Spawning player prefab with hash: {netObj.PrefabIdHash}");
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= Singleton_OnClientConnectedCallback;
    }
}
#endif
