using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class InputDirector : MonoBehaviour, IPlayerBehavior
{
    /// <summary>
    /// The director of all input related events.
    /// 
    /// input events run just before "update()" does.
    /// 
    /// input is sent through events, and is passed to it's neccesary functions through the different managers.
    /// notice every "started" event is called before "updated".
    /// </summary>
    

    // master
    private ActionsMaster _playerInput;
    private readonly HashSet<object> _movementAbilityLocks = new();
    private readonly HashSet<object> _equipmentInputLocks = new();
    private readonly HashSet<object> _gameplayInputLocks = new();
    private bool _runRequested = true;
    private bool _jumpRequested = true;

    public bool GameplayInputEnabled => _gameplayInputLocks.Count == 0;
    public bool CanRun => GameplayInputEnabled && _runRequested && _movementAbilityLocks.Count == 0;
    public bool CanJump => GameplayInputEnabled && _jumpRequested && _movementAbilityLocks.Count == 0;
    public bool CanUseEquipment => GameplayInputEnabled && _equipmentInputLocks.Count == 0;
    public static InputDirector Instance;

    // events
    public event Action OnInputReady;
    public event Action OnDisablePlayerMovement;
    public event Action OnEnablePlayerMovement;

    public event Action OnFireStarted;
    public event Action OnFirePressed;
    public event Action OnFireReleased;
    public event Action OnFireClicked;
    public event Action OnInteractPressed;
    public event Action OnCombatPressed;
    public event Action OnInventoryPressed;
    public event Action OnMainMenuPressed;
    public event Action OnPausePressed;
    public event Action OnPressedTimeChange;
    public event Action<int> OnEmotePressed;
    
    [SerializeField] private float clickDragThreshold = 8f;

    private Vector2 _mouseDownPosition;
    private bool _mouseWasDragged;

    private Action<Vector2> _onPlayerMoved;
    public event Action<Vector2> OnPlayerMoved
    { // if someone subscribes while input is already pressed, send them the event as well.
        add
        {
            _onPlayerMoved += value;
            value?.Invoke(MovementValue); // Send latest movement immediately
        }
        remove
        {
            _onPlayerMoved -= value;
        }
    }
    public event Action OnPlayerMovedStarted;
    public event Action OnPlayerMovedFinished;

    public event Action<Vector2> OnCameraMoved;
    public event Action<float> OnCameraZoomChanged;
    public event Action OnLockSwitchPressed;

    public event Action OnPlayerRunStarted;
    public event Action OnPlayerRunStopped;
    public event Action OnPlayerRunDisabled;
    public event Action OnPlayerRunEnabled;

    public event Action OnPlayerJumpStarted;
    public event Action OnPlayerJumpStopped;

    public event Action OnPlayerCrouchStarted;
    public event Action OnPlayerCrouchStopped;

    public event Action OnPlayerFlameThrowerStart;
    public event Action OnPlayerFlameThrowerStop;
    
    public event Action OnMouseDragStarted;
    public event Action<float> OnMouseDragged;
    public event Action OnMouseDragFinished;

    public event Action OnConfirmPressed;
    public event Action OnBackPressed;

    // values
    public Vector2 MovementValue;

    private bool _isMouseDragging;
    private float _lastMouseX;
    
    // data
    private Player _localPlayer;
    public bool ShouldDisable; // only disable when we're done with player, when this flag is on.
    
    public void AwakePlayer()
    {
        // Multiplayer Guard
        _localPlayer = GetComponent<Player>();
        Debug.Log("new InputDirector awake. got authority? "
                  + $"{(_localPlayer.HasAuthority ? "yes, setting up" : "no, skipping setup.")}");
        
        if (!_localPlayer.HasAuthority)
            return;
        
        // Singleton
        if (!Instance)
            Instance = this;
        else
            Debug.LogWarning("input director already exists.");
    }

    public void OnEnablePlayer()
    {
        // Multiplayer Guard
        if (!_localPlayer.HasAuthority)
            return;
        
        // Reinitializing the owner must not leave a second enabled action map
        // delivering the same key press (e.g. setup replay after a skin swap).
        _playerInput?.Disable();
        _playerInput?.Dispose();
        _playerInput = new ActionsMaster();
        
        // actions
        _playerInput.Player.Fire1.started += _ => { if (CanUseEquipment) OnFireStarted?.Invoke(); };
        _playerInput.Player.Fire1.performed += _ => { if (CanUseEquipment) OnFirePressed?.Invoke(); };
        _playerInput.Player.Fire1.canceled += _ => OnFireReleased?.Invoke();
        _playerInput.Player.Pause.performed += _ => OnPausePressed?.Invoke();
        _playerInput.Player.Interact.performed += _ => { if (GameplayInputEnabled) OnInteractPressed?.Invoke(); };
        _playerInput.Player.Inventory.performed += _ => { if (GameplayInputEnabled) OnInventoryPressed?.Invoke(); };
        _playerInput.Player.MainMenu.performed += _ => { if (GameplayInputEnabled) OnMainMenuPressed?.Invoke(); };
        _playerInput.Player.TimeSwap.performed += _ => { if (GameplayInputEnabled) OnPressedTimeChange?.Invoke(); };
        
        _playerInput.Player.Confirm.performed += _ => { if (GameplayInputEnabled) OnConfirmPressed?.Invoke(); };
        _playerInput.Player.Back.performed += _ => { if (GameplayInputEnabled) OnBackPressed?.Invoke(); };

        // combat
        _playerInput.Player.Combat.performed += _ => { if (CanUseEquipment) OnCombatPressed?.Invoke(); };

        _playerInput.Player.FlameThrower.performed += _ => { if (CanUseEquipment) OnPlayerFlameThrowerStart?.Invoke(); };
        _playerInput.Player.FlameThrower.canceled += _ => OnPlayerFlameThrowerStop?.Invoke();

        // camera
        _playerInput.Player.Look.performed += ctx => { if (GameplayInputEnabled) OnCameraMoved?.Invoke(ctx.ReadValue<Vector2>()); };
        _playerInput.Player.Zoom.performed += ctx => { if (GameplayInputEnabled) OnCameraZoomChanged?.Invoke(ctx.ReadValue<float>()); };
        _playerInput.Player.LockSwitch.performed += _ => { if (GameplayInputEnabled) OnLockSwitchPressed?.Invoke(); };

        // movement
        _playerInput.Player.Movement.performed += x => { if (!GameplayInputEnabled) return; MovementValue = x.ReadValue<Vector2>(); _onPlayerMoved?.Invoke(MovementValue); };
        _playerInput.Player.Movement.started += x => { if (!GameplayInputEnabled) return; MovementValue = x.ReadValue<Vector2>(); OnPlayerMovedStarted?.Invoke();  _onPlayerMoved?.Invoke(MovementValue); };
        _playerInput.Player.Movement.canceled += x => { MovementValue = x.ReadValue<Vector2>(); OnPlayerMovedFinished?.Invoke(); };

        _playerInput.Player.Running.started += _ => { if (CanRun) OnPlayerRunStarted?.Invoke(); };
        _playerInput.Player.Running.canceled += _ => OnPlayerRunStopped?.Invoke();

        // jumping
        _playerInput.Player.Jumping.started += _ => { if (CanJump) OnPlayerJumpStarted?.Invoke(); };
        _playerInput.Player.Jumping.canceled += _ => OnPlayerJumpStopped?.Invoke();

        // crouching
        _playerInput.Player.Crouch.started += _ => { if (GameplayInputEnabled) OnPlayerCrouchStarted?.Invoke(); };
        _playerInput.Player.Crouch.canceled += _ => OnPlayerCrouchStopped?.Invoke();

        // plugins
        Cursor.visible = false;

        // Director
        _playerInput.Enable();
        RefreshMovementAbilities();
        OnInputReady?.Invoke();
        
        ShouldDisable = true;
    }

    public void OnDisablePlayer()
    {
        if (!ShouldDisable)
            return;

        if (Instance == this) Instance = null;

        // plugins

        // Unsubscribe from everything & Disable Director
        if (_playerInput == null) return;
        _playerInput.Player.Disable();
        _playerInput.Disable();
        _playerInput.Dispose();
        _playerInput = null;
    }
    
    private void Update()
    {
        if (!_localPlayer || !_localPlayer.HasAuthority || !GameplayInputEnabled)
            return;

        UpdateEmoteInput();
        UpdateMouseDrag();
    }

    private void UpdateEmoteInput()
    {
        if (Keyboard.current == null || _playerInput == null)
            return;

        // Keep emote input tied to the Player action map's enabled state.
        if (!_playerInput.Player.enabled)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame) OnEmotePressed?.Invoke(0);
        else if (Keyboard.current.digit2Key.wasPressedThisFrame) OnEmotePressed?.Invoke(1);
        else if (Keyboard.current.digit3Key.wasPressedThisFrame) OnEmotePressed?.Invoke(2);
        else if (Keyboard.current.digit4Key.wasPressedThisFrame) OnEmotePressed?.Invoke(3);
        else if (Keyboard.current.digit5Key.wasPressedThisFrame) OnEmotePressed?.Invoke(4);
        else if (Keyboard.current.digit6Key.wasPressedThisFrame) OnEmotePressed?.Invoke(5);
        else if (Keyboard.current.digit7Key.wasPressedThisFrame) OnEmotePressed?.Invoke(6);
        else if (Keyboard.current.digit8Key.wasPressedThisFrame) OnEmotePressed?.Invoke(7);
        else if (Keyboard.current.digit9Key.wasPressedThisFrame) OnEmotePressed?.Invoke(8);
    }

    public void EnableInput()
    {
        OnEnablePlayerMovement?.Invoke();
    }

    public void DisableInput()
    {
        OnDisablePlayerMovement?.Invoke();
    }
    
    public void EnableMovementAction()
    {
        var movement = _playerInput.Player.Movement;
        if (!movement.enabled)
            movement.Enable();
    }
    
    public void DisableMovementAction()
    {
        MovementValue = Vector2.zero;
        _onPlayerMoved?.Invoke(Vector2.zero);
        OnPlayerMovedFinished?.Invoke();

        var movement = _playerInput.Player.Movement;
        if (movement.enabled)
            movement.Disable();
    }

    public void EnableMouseUIInput()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void DisableMouseUIInput()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void DisableJumpInput()
    {
        _jumpRequested = false;
        ApplyJumpAvailability();
    }
    
    public void EnableJumpInput()
    {
        _jumpRequested = true;
        ApplyJumpAvailability();
    }

    public void ToggleRun(bool canRun)
    {
        _runRequested = canRun;
        ApplyRunAvailability();
    }

    /// <summary>Locks sprint and jump without overwriting story or other owners' restrictions.</summary>
    public void SetMovementAbilityLock(object source, bool blocked)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        bool changed = blocked ? _movementAbilityLocks.Add(source) : _movementAbilityLocks.Remove(source);
        if (changed) RefreshMovementAbilities();
    }

    /// <summary>Temporarily blocks equipment input without disabling interaction or camera controls.</summary>
    public void SetEquipmentInputLock(object source, bool blocked)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        bool wasAvailable = CanUseEquipment;
        if (blocked) _equipmentInputLocks.Add(source);
        else _equipmentInputLocks.Remove(source);
        if (wasAvailable && !CanUseEquipment)
        {
            OnFireReleased?.Invoke();
            OnPlayerFlameThrowerStop?.Invoke();
        }
    }

    /// <summary>Blocks gameplay while leaving Pause active and preserving existing story/action locks.</summary>
    public void SetGameplayInputLock(object source, bool blocked)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        bool wasEnabled = GameplayInputEnabled;
        if (blocked) _gameplayInputLocks.Add(source);
        else _gameplayInputLocks.Remove(source);
        if (wasEnabled == GameplayInputEnabled) return;
        if (!GameplayInputEnabled)
        {
            MovementValue = Vector2.zero;
            _onPlayerMoved?.Invoke(Vector2.zero);
            OnPlayerMovedFinished?.Invoke();
            OnCameraMoved?.Invoke(Vector2.zero);
            OnFireReleased?.Invoke();
            OnPlayerFlameThrowerStop?.Invoke();
            OnPlayerCrouchStopped?.Invoke();
            if (_isMouseDragging) OnMouseDragFinished?.Invoke();
            _isMouseDragging = false;
        }
        RefreshMovementAbilities();
    }

    // Also called after changing movement state so newly subscribed states inherit the lock.
    public void RefreshMovementAbilities()
    {
        // Exit ghoul form/cancel its charge before disabling Jump invokes its canceled callback.
        ApplyRunAvailability();
        ApplyJumpAvailability();
    }

    private void ApplyRunAvailability()
    {
        if (CanRun)
            OnPlayerRunEnabled?.Invoke();
        else
        {
            OnPlayerRunDisabled?.Invoke();
            OnPlayerRunStopped?.Invoke();
        }
    }

    private void ApplyJumpAvailability()
    {
        if (_playerInput == null) return;
        var jump = _playerInput.Player.Jumping;
        if (!CanJump && jump.enabled) jump.Disable();
        else if (CanJump && !jump.enabled) jump.Enable();
    }
    
    private void UpdateMouseDrag()
    {
        if (Mouse.current == null)
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            _isMouseDragging = true;
            _mouseWasDragged = false;

            _mouseDownPosition = Mouse.current.position.ReadValue();
            _lastMouseX = _mouseDownPosition.x;

            OnMouseDragStarted?.Invoke();
        }

        if (_isMouseDragging && Mouse.current.leftButton.isPressed)
        {
            Vector2 currentMousePosition = Mouse.current.position.ReadValue();

            if (Vector2.Distance(_mouseDownPosition, currentMousePosition) > clickDragThreshold)
                _mouseWasDragged = true;

            float currentMouseX = currentMousePosition.x;
            float deltaX = currentMouseX - _lastMouseX;
            _lastMouseX = currentMouseX;

            if (Mathf.Abs(deltaX) > 0.01f)
                OnMouseDragged?.Invoke(deltaX);
        }

        if (_isMouseDragging && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            _isMouseDragging = false;
            OnMouseDragFinished?.Invoke();

            if (!_mouseWasDragged && CanUseEquipment)
                OnFireClicked?.Invoke();
        }
    }

    public void KillSelf()
    {
        OnDisablePlayer();
    }
}
