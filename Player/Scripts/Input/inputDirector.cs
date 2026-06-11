using UnityEngine;
using System;
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
    public event Action OnPressedTimeChange;
    
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
        
        _playerInput = new ActionsMaster();
        
        // actions
        _playerInput.Player.Fire1.started += _ => OnFireStarted?.Invoke();
        _playerInput.Player.Fire1.performed += _ => OnFirePressed?.Invoke();
        _playerInput.Player.Fire1.canceled += _ => OnFireReleased?.Invoke();
        _playerInput.Player.Interact.performed += _ => OnInteractPressed?.Invoke();
        _playerInput.Player.Inventory.performed += _ => OnInventoryPressed?.Invoke();
        _playerInput.Player.MainMenu.performed += _ => OnMainMenuPressed?.Invoke();
        _playerInput.Player.TimeSwap.performed += _ => OnPressedTimeChange?.Invoke();
        
        _playerInput.Player.Confirm.performed += _ => OnConfirmPressed?.Invoke();
        _playerInput.Player.Back.performed += _ => OnBackPressed?.Invoke();

        // combat
        _playerInput.Player.Combat.performed += _ => OnCombatPressed?.Invoke();

        _playerInput.Player.FlameThrower.performed += _ => OnPlayerFlameThrowerStart?.Invoke();
        _playerInput.Player.FlameThrower.canceled += _ => OnPlayerFlameThrowerStop?.Invoke();

        // camera
        _playerInput.Player.Look.performed += ctx => OnCameraMoved?.Invoke(ctx.ReadValue<Vector2>());
        _playerInput.Player.Zoom.performed += ctx => OnCameraZoomChanged?.Invoke(ctx.ReadValue<float>());

        // movement
        _playerInput.Player.Movement.performed += x => { MovementValue = x.ReadValue<Vector2>(); _onPlayerMoved?.Invoke(MovementValue); };
        _playerInput.Player.Movement.started += x => { MovementValue = x.ReadValue<Vector2>(); OnPlayerMovedStarted?.Invoke();  _onPlayerMoved?.Invoke(MovementValue); };
        _playerInput.Player.Movement.canceled += x => { MovementValue = x.ReadValue<Vector2>(); OnPlayerMovedFinished?.Invoke(); };

        _playerInput.Player.Running.started += _ => OnPlayerRunStarted?.Invoke();
        _playerInput.Player.Running.canceled += _ => OnPlayerRunStopped?.Invoke();

        // jumping
        _playerInput.Player.Jumping.started += _ => OnPlayerJumpStarted?.Invoke();
        _playerInput.Player.Jumping.canceled += _ => OnPlayerJumpStopped?.Invoke();

        // crouching
        _playerInput.Player.Crouch.started += _ => OnPlayerCrouchStarted?.Invoke();
        _playerInput.Player.Crouch.canceled += _ => OnPlayerCrouchStopped?.Invoke();

        // plugins
        Cursor.visible = false;

        // Director
        _playerInput.Enable();
        OnInputReady?.Invoke();
        
        ShouldDisable = true;
    }

    public void OnDisablePlayer()
    {
        if (!ShouldDisable)
            return;

        Instance = null;

        // plugins

        // Unsubscribe from everything & Disable Director
        _playerInput.Player.Disable();
        _playerInput.Disable();
        _playerInput.Dispose();
    }
    
    private void Update()
    {
        if (!_localPlayer || !_localPlayer.HasAuthority)
            return;

        UpdateMouseDrag();
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
        // Disables the Jumping action so no callbacks fire
        var jump = _playerInput.Player.Jumping;
        if (jump is { enabled: true })
            jump.Disable();
    }
    
    public void EnableJumpInput()
    {
        // Enables the Jumping action
        var jump = _playerInput.Player.Jumping;
        if (jump is { enabled: false })
            jump.Enable();
    }

    public void ToggleRun(bool canRun)
    {
        if (canRun)
            OnPlayerRunEnabled?.Invoke();
        else
            OnPlayerRunDisabled?.Invoke();
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

            if (!_mouseWasDragged)
                OnFireClicked?.Invoke();
        }
    }

    public void KillSelf()
    {
        OnDisablePlayer();
    }
}
