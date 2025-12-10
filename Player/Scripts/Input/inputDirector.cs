using UnityEngine;
using System;
using UnityEngine.InputSystem;

public class InputDirector : MonoBehaviour
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
    public event Action OnInteractPressed;
    public event Action OnCombatPressed;
    public event Action OnInventoryPressed;
    public event Action OnMainMenuPressed;
    public event Action OnPressedTimeChange;

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
    

    // values
    public Vector2 MovementValue;

    // data
    public bool ShouldDisable; // only disable when we're done with player, when this flag is on.
    public bool IsOwner = true;


    private void Awake()
    {
        if (!IsOwner)
            return;

        if (InputDirector.Instance == null)
            InputDirector.Instance = this;
        else
        {
            Debug.LogWarning("input director already exists.");
            //Destroy(gameObject);
        }

        _playerInput = new ActionsMaster();
    }

    private void OnEnable()
    {
        if (!IsOwner)
            return;

        // actions
        _playerInput.Player.Fire1.started += _ => OnFireStarted?.Invoke();
        _playerInput.Player.Fire1.performed += _ => OnFirePressed?.Invoke();
        _playerInput.Player.Fire1.canceled += _ => OnFireReleased?.Invoke();
        _playerInput.Player.Interact.performed += _ => OnInteractPressed?.Invoke();
        _playerInput.Player.Inventory.performed += _ => OnInventoryPressed?.Invoke();
        _playerInput.Player.MainMenu.performed += _ => OnMainMenuPressed?.Invoke();
        _playerInput.Player.TimeSwap.performed += _ => OnPressedTimeChange?.Invoke();

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

    private void OnDisable()
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

    public void EnableInput()
    {
        OnEnablePlayerMovement?.Invoke();
    }

    public void DisableInput()
    {
        OnDisablePlayerMovement?.Invoke();
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
}
