using System;
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
