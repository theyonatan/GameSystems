using FishNet.Connection;
using UnityEngine;

#if FISHNET
using FishNet;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine.Events;
#endif

public class MultiplayerManager : NetworkBehaviour
{
#if FISHNET
    #region Singleton
    
    public static MultiplayerManager Instance;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        networkManager = InstanceFinder.NetworkManager;
    }

    #endregion
    
    [SerializeField] private NetworkManager networkManager;
    
    /// <summary> Runs only on the Server </summary>
    public UnityAction<NetworkConnection> OnClientConnected;
    /// <summary> Runs only on the Server </summary>
    public UnityAction<NetworkConnection> OnClientDisconnected; 
    
    #region StartStop

    
    public void StartHost()
    {
        networkManager.ServerManager.StartConnection();
        networkManager.ClientManager.StartConnection();
    }

    public void StartServer()
    {
        networkManager.ServerManager.StartConnection();
    }

    public void JoinAsClient(string address = "localhost")
    {
        networkManager.ClientManager.StartConnection(address);
    }

    public void Leave()
    {
        networkManager.ClientManager.StopConnection();
        networkManager.ServerManager.StopConnection(false);
    }

    #endregion

    #region MultiplayerEvents
    
    private void OnEnable()
    {
        networkManager.ServerManager.OnRemoteConnectionState += RemoteConnectionChanged;
    }

    private void RemoteConnectionChanged(NetworkConnection connection, RemoteConnectionStateArgs args)
    {
        if (!networkManager.IsServerStarted)
            return;
        
        switch (args.ConnectionState)
        {
            case RemoteConnectionState.Started:
                Debug.Log("New Player Connected");
                connection.OnLoadedStartScenes += ConnectionSceneLoaded;
                break;
            case RemoteConnectionState.Stopped:
                Debug.Log("Player Disconnected");
                connection.OnLoadedStartScenes -= ConnectionSceneLoaded;
                OnClientDisconnected?.Invoke(connection);
                break;
        }
    }

    private void ConnectionSceneLoaded(NetworkConnection connection, bool isServer)
    {
        if (!isServer)
            return;
        
        OnClientConnected?.Invoke(connection);
    }

    private void OnDisable()
    {
        networkManager.ServerManager.OnRemoteConnectionState -= RemoteConnectionChanged;
    }

    #endregion

    #region MultiplayerFunctions

    public Player SpawnPlayer(
        NetworkConnection clientConnection,
        NetworkObject playerPrefab,
        Vector3 position,
        Quaternion rotation)
    {
        if (playerPrefab == null || clientConnection == null) return null;
        Debug.Log($"Spawning Player for client {clientConnection.ClientId}");

        NetworkObject spawnedPlayer = Instantiate(playerPrefab, position, rotation);
        networkManager.ServerManager.Spawn(spawnedPlayer, clientConnection);
        
        // Authority is given locally.

        return spawnedPlayer.GetComponent<Player>();
    }

    public void DespawnPlayer(Player player)
    {
        if (player == null) return;
        Debug.Log($"Despawning Player for client {player.PlayerId}");
        
        networkManager.ServerManager.Despawn(player.gameObject);
    }
    
    #endregion
    
#endif
}