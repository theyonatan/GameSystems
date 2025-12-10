using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Events;

// ReSharper disable All

public class MultiplayerManager
{
    // singleton
    private static MultiplayerManager _instance;
    public static MultiplayerManager Instance => _instance ??= new MultiplayerManager();
    
    // personal data
    public int localId;
    
    // events
    public UnityEvent OnNewPlayerRegistered;
    public UnityEvent OnPlayerDisconnected;
    
    // player data object (new one per player)
    public Dictionary<int, IPlayerDataMultiplayer> PlayersDict = new();
    public int CountPlayers = 0;
    
    // functions to get player by data
    public int RegisterNewPlayer(IPlayerDataMultiplayer playerData, bool localPlayer=false)
    {
        // set playerid
        int playerID = CountPlayers;
        CountPlayers++;
        
        // add player to dictionary
        if (PlayersDict.ContainsKey(playerID))
            Debug.LogError($"PlayerID {playerID} is already registered!");
        
        PlayersDict[playerID] = playerData;

        if (localPlayer)
            localId = playerID;
        OnNewPlayerRegistered?.Invoke();
        
        return playerID;
    }
    
    [CanBeNull]
    public IPlayerDataMultiplayer TryGetPlayerByID(int id)
    {
        if (!PlayersDict.ContainsKey(id))
        {
            Debug.LogError($"PlayerID {id} is not registered!");
            return null;
        }
        
        return PlayersDict[id];
    }
    
    [CanBeNull]
    public IPlayerDataMultiplayer TryGetLocalPlayer()
    {
        if (!PlayersDict.ContainsKey(localId))
        {
            Debug.LogError($"Local Player is not registered!");
            return null;
        }
        
        return PlayersDict[localId];
    }

    [CanBeNull]
    public void RemovePlayer(int id)
    {
        if (!PlayersDict.Remove(id))
            Debug.LogError($"Can't Remove PlayerID: {id} is not registered!");
        
        Debug.Log($"Removed player: {id}");
    }
}
