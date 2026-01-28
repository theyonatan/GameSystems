using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

public class MultiplayerStoryExample : NetworkBehaviour
{
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private Transform spawnPoint;
    private MultiplayerManager _mm;

    private void Start()
    {
        _mm = MultiplayerManager.Instance;
        _mm.OnClientLoaded += OnClientConnected;
        _mm.OnClientDisconnected += OnClientDisconnected;
    }

    private void OnDestroy()
    {
        _mm.OnClientLoaded -= OnClientConnected;
        _mm.OnClientDisconnected -= OnClientDisconnected;
    }

    private void OnClientConnected(NetworkConnection connection)
    {
        var spawnedPlayer = _mm.SpawnPlayer(
            connection,
            playerPrefab,
            spawnPoint.position,
            Quaternion.identity);
        
        spawnedPlayer.PlayerId = connection.ClientId;
        
        spawnedPlayer.GetComponent<ExtensionMultiplayer>().SetupTPSPlayerRpc(connection);
    }

    private void OnClientDisconnected(NetworkConnection connection)
    {
        var players = FindObjectsByType<Player>(FindObjectsSortMode.InstanceID);
        
        foreach (var player in players)
        {
            if (player.PlayerId != connection.ClientId)
                continue;
            
            _mm.DespawnPlayer(player);
            return;
        }
    }
}
