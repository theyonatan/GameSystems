using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class Player : MonoBehaviour
{
    private Camera _cam;
    private string _currentState;
    private PlayerStateData _playerStateData;
    public int PlayerId = -1;
    /// <summary> Make sure ownsAuthority Starts Disabled on: multiplayer - "Player Prefabs" on Multiplayer games! </summary>
    [SerializeField] private bool ownsAuthority = true;
    public bool HasAuthority => ownsAuthority;
    public bool PlayerEnabled = true;

    public Camera GetCamera()
    {
        if (!_cam)
            _cam = Camera.main;
        return _cam;
    }

    public CinemachineCamera GetCameraController()
    {
        return GetComponent<CameraManager>().CurrentCinemachineComponent;
    }

    public static Player GetPlayer(int playerId)
    {
        foreach (var player in FindObjectsByType<Player>(FindObjectsSortMode.InstanceID))
            if (player.PlayerId == playerId)
                return player;
        
        return FindFirstObjectByType<Player>();
    }

    public static Player GetSelf()
    {
        foreach (var player in FindObjectsByType<Player>(FindObjectsSortMode.InstanceID))
            if (player.HasAuthority)
                return player;
        
        return FindFirstObjectByType<Player>();
    }

    /// <summary>
    /// Kills self. resets all static items.
    /// use at the end of scene
    ///
    /// does not kill the StoryExecuter
    /// </summary>
    public void KillSelf()
    {
        GetComponent<InputDirector>().KillSelf();
    }

    public void SetAuthority(bool value)
    {
        ownsAuthority = value;
    }

    private void SelfStart()
    {
        Load("WalkingPlayer");
    }

    public ref PlayerStateData GetData(string stateName)
    {
        switch (stateName)
        {
            case "Walking":
                Load("WalkingPlayer");
                return ref _playerStateData;
            case "WaterTurbo":
                Load("WaterTurboPlayer");
                return ref _playerStateData;
            default:
                Load("WalkingPlayer");
                return ref _playerStateData;
        }
    }

    public Transform GetEyes() => GetComponentInChildren<CameraOrientation>().transform;

    private void Load(string stateName)
    {
        if (stateName != _currentState)
            _playerStateData = Resources.Load<PlayerStateData>($"playerStates/{stateName}");
        
        _currentState = stateName;
    }
    
    public void SwapPlayerState<TMovementState, TCameraState>()
        where TMovementState : MovementState, new()
        where TCameraState : CameraState, new()
    {
        var movementManager = gameObject.GetComponent<MovementManager>();
        var cameraManager = gameObject.GetComponent<CameraManager>();

        if (!movementManager || !cameraManager)
            return;

        movementManager.ChangeState(new TMovementState());
        cameraManager.ChargeState(new TCameraState());
    }

    public void SwapSkin(string skinName)
    {
        // disable player for a frame
        DisablePlayerBehaviors();

        // Find old skin and save its rotation
        var currentSkin = GetComponentInChildren<Skin>();
        if (!currentSkin)
        {
            Debug.LogError("current skin doesn't support swapping!");
            return;
        }
        
        var oldSkinRotation = currentSkin.transform.rotation;
        
        // Spawn New skin and apply old rotation
        Skin newSkin = Resources.Load<Skin>($"Skins/{skinName}");
        if (!newSkin)
        {
            Debug.LogError($"new skin wasn't found! Skins/{skinName}");
            return;
        }

        var spawnedSkin = Instantiate(newSkin, transform);
        spawnedSkin.transform.rotation = oldSkinRotation;
        
        // Destroy old skin - now all states are tuned to the new references.
        Destroy(currentSkin.gameObject);
        
        StartCoroutine(ApplySkinCoroutine());
    }

    private IEnumerator ApplySkinCoroutine()
    {
        // wait for the changes to apply, destroy old player and fully instantiate the new one
        yield return null;
        
        // refresh assignables
        foreach (var refreshableReference in GetComponents<IRefreshPlayerReferences>())
            refreshableReference.RefreshPlayerReferences();
        GetComponent<MovementManager>().RefreshPlayerReferences();
        GetComponent<CameraManager>().RefreshPlayerReferences();
        
        // refresh animations assignables
        GetComponent<AnimationsManager>().RefreshPlayerAnimator();
        
        // Refresh Equipped Items
        foreach (var equipable in GetComponents<IEquipableHeldItem>())
            equipable.RefreshEquippedVisuals();
        
        // reenable player after everything is finished collecting
        EnablePlayerBehaviors();
    }

    // MonoBehavior Events
    // Multiplayer: DON'T FORGET TO ENABLE PLAYER BEHAVIOURS() BEFORE RUNNING THESE MANUALLY
    IPlayerBehavior[] _playerBehaviors;

    public void DisablePlayerBehaviors()
    {
        PlayerEnabled = false;
    }
    public void EnablePlayerBehaviors()
    {
        PlayerEnabled = true;
        
        
    }
    
    public void Awake()
    {
        _playerBehaviors = GetComponents<IPlayerBehavior>();
        
        if (!HasAuthority || !PlayerEnabled)
            return;
        
        foreach (var behavior in _playerBehaviors)
            behavior.AwakePlayer();
    }

    public void OnEnable()
    {
        if (!HasAuthority || !PlayerEnabled)
            return;
        
        foreach (var behavior in _playerBehaviors)
            behavior.OnEnablePlayer();
    }

    public void Start()
    {
        if (!HasAuthority || !PlayerEnabled)
            return;
        
        SelfStart();
        
        foreach (var behavior in _playerBehaviors)
            behavior.StartPlayer();
    }

    public void Update()
    {
        if (!HasAuthority || !PlayerEnabled)
            return;
        
        foreach (var behavior in _playerBehaviors)
            behavior.UpdatePlayer();
    }

    public void FixedUpdate()
    {
        if (!HasAuthority || !PlayerEnabled)
            return;
        
        foreach (var behavior in _playerBehaviors)
            behavior.FixedUpdatePlayer();
    }

    public void OnDisable()
    {
        if (!HasAuthority || !PlayerEnabled)
            return;
        
        foreach (var behavior in _playerBehaviors)
            behavior.OnDisablePlayer();
    }
    
    public void OnDestroy()
    {
        if (!HasAuthority || !PlayerEnabled)
            return;
        
        foreach (var behavior in _playerBehaviors)
            behavior.OnDestroyPlayer();
    }
}
