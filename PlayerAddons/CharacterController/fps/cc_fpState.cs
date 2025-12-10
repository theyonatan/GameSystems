using System;
using UnityEngine;

/// <summary>
/// First Person State - Character Controller
/// </summary>
[Serializable]
public class cc_fpState : MovementState
{
    [Header("Definition")]
    public override MovementComponentType ComponentType => MovementComponentType.CharacterController;
    [SerializeField] public string StateName = "cc FirstPerson";
    
    [Header("Assignables")]
    [SerializeField] private CharacterController cc;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform orientation; // animator root
    [SerializeField] private Player player;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float acceleration = 12f;
    
    [Header("Jump & Gravity")]
    [SerializeField] private float jumpHeight = 1.6f;
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float minGravity = -30f;
    [SerializeField] private float maxGravity = 20f;
    [SerializeField] private float groundedOffset = -0.15f;
    [SerializeField] private float groundedRadius = 0.25f;
    
    private bool _holdingJump;
    private bool IsGrounded => cc.isGrounded;
    private bool _holdingSprint;
    
    private Vector2 _moveInput = new Vector2(0f, 0f);
    private Vector3 _movementVelocity;
    private float _verticalVelocity;
    
    // -------------------------------
    // State Machine
    // -------------------------------
    public override void LoadState(MovementManager manager, InputDirector director)
    {
        Controller = manager;
        Director = director ?? InputDirector.Instance;
        playerTransform = manager.transform;
        
        cc = manager.GetComponent<CharacterController>();
        player = manager.GetComponent<Player>();
        
        var animator = manager.GetComponentInChildren<Animator>();
        orientation = animator ? animator.transform : manager.transform;

        LocalData = player.GetData("Walking");
        acceleration = LocalData.acceleration;
        walkSpeed = LocalData.walkingSpeed;
        sprintSpeed = LocalData.runningSpeed;
        
        // Subscribe to input events
        Director.OnPlayerMoved += OnPlayerMoved;
        Director.OnPlayerMovedFinished += OnPlayerMovedFinished;
        Director.OnPlayerJumpStarted += OnPlayerJumpStarted;
        Director.OnPlayerJumpStopped += OnPlayerJumpStopped;
        Director.OnPlayerRunStarted += OnPlayerRunStarted;
        Director.OnPlayerRunStopped += OnPlayerRunStopped;
        Director.OnEnablePlayerMovement += OnEnablePlayerMovement;
        Director.OnDisablePlayerMovement += OnDisablePlayerMovement;
        Director.OnPlayerRunEnabled += OnPlayerRunEnabled;
        Director.OnPlayerRunDisabled += OnPlayerRunDisabled;
        
        // Subscribe to player data changes
        LocalData.OnAccelerationChanged += newAcceleration => acceleration = newAcceleration;
        LocalData.OnWalkingSpeedChanged += newWalkingSpeed => walkSpeed = newWalkingSpeed;
        LocalData.OnRunningSpeedChanged += newRunningSpeed => sprintSpeed = newRunningSpeed;
    }

    public override void EnterState()
    {
        _verticalVelocity = -2f;
    }

    public override void UpdateState()
    {
        HandleHorizontalMovement();
        HandleJumpAndGravity();
        MovePlayer();
    }

    public override void FixedUpdate()
    {
        
    }

    public override void CleanState()
    {
        // Reset velocity
        _movementVelocity = Vector3.zero;
        _verticalVelocity = 0f;

        // Unsubscribe
        Director.OnPlayerMoved -= OnPlayerMoved;
        Director.OnPlayerMovedFinished -= OnPlayerMovedFinished;
        Director.OnPlayerJumpStarted -= OnPlayerJumpStarted;
        Director.OnPlayerJumpStopped -= OnPlayerJumpStopped;
        Director.OnPlayerRunStarted -= OnPlayerRunStarted;
        Director.OnPlayerRunStopped -= OnPlayerRunStopped;
        Director.OnEnablePlayerMovement -= OnEnablePlayerMovement;
        Director.OnDisablePlayerMovement -= OnDisablePlayerMovement;
        Director.OnPlayerRunEnabled -= OnPlayerRunEnabled;
        Director.OnPlayerRunDisabled -= OnPlayerRunDisabled;
    }

    // -------------------------------
    // Core Movement
    // -------------------------------
    private void MovePlayer()
    {
        if (!CanMove)
            return;
        
        cc.Move(_movementVelocity * Time.deltaTime);
    }

    private void HandleHorizontalMovement()
    {
        if (!CanMove)
        {
            _movementVelocity.x = 0f;
            _movementVelocity.z = 0f;
            return;
        }

        // FP: move relative to orientation (which is usually synced with camera yaw)
        Vector3 forward = orientation.forward;
        Vector3 right = orientation.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 inputDir = new Vector3(_moveInput.x, 0f, _moveInput.y);
        Vector3 targetDirection = (forward * inputDir.z + right * inputDir.x).normalized;

        float targetSpeed = 0f;
        if (_moveInput.magnitude > 0.01f)
        {
            targetSpeed = _holdingSprint && CanRun ? sprintSpeed : walkSpeed;
        }

        Vector3 targetHorizontalVelocity = targetDirection * targetSpeed;
        Vector3 currentHorizontal = new Vector3(_movementVelocity.x, 0f, _movementVelocity.z);

        currentHorizontal = Vector3.Lerp(currentHorizontal, targetHorizontalVelocity,
            acceleration * Time.deltaTime);

        _movementVelocity.x = currentHorizontal.x;
        _movementVelocity.z = currentHorizontal.z;
    }

    private void HandleJumpAndGravity()
    {
        // constant but limited force if on ground
        if (IsGrounded)
            _verticalVelocity = gravity * Time.deltaTime;
        
        // jump
        if (IsGrounded && _holdingJump && CanMove)
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        
        // new velocity, clamp if reached max
        _verticalVelocity += gravity * Time.deltaTime;
        _verticalVelocity = Mathf.Clamp(_verticalVelocity, minGravity, maxGravity);
        
        // apply new velocity
        _movementVelocity.y = _verticalVelocity;
    }
    
    // -------------------------------
    // Input Events
    // -------------------------------
    private void OnPlayerMoved(Vector2 movementValue)
    {
        _moveInput = movementValue;
    }

    private void OnPlayerMovedFinished()
    {
        _moveInput = Vector2.zero;
    }

    private void OnPlayerJumpStarted() => _holdingJump = true;
    private void OnPlayerJumpStopped() => _holdingJump = false;

    private void OnPlayerRunStarted() => _holdingSprint = true;
    private void OnPlayerRunStopped() => _holdingSprint = false;

    private void OnEnablePlayerMovement() => CanMove = true;
    private void OnDisablePlayerMovement() => CanMove = false;

    private void OnPlayerRunEnabled() => CanRun = true;
    private void OnPlayerRunDisabled() => CanRun = false;
}
