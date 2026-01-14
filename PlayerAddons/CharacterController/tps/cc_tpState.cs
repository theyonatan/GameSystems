using System;
using SHG.AnimatorCoder;
using UnityEngine;

[Serializable]
public class cc_tpState : MovementState
{
    [Header("Definition")]
    public override MovementComponentType ComponentType => MovementComponentType.CharacterController;
    [SerializeField] public const string StateName = "cc ThirdPerson";
    
    [Header("Assignables")]
    [SerializeField] private CharacterController cc;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform characterOrientation;
    private Player _player;
    private AnimationsManager _animator;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float acceleration = 12f;
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Jump & Gravity")]
    [SerializeField] private float jumpHeight = 1.4f;
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float minGravity = -30f;
    [SerializeField] private float maxGravity = 20f;
    [SerializeField] private float groundedOffset = -0.15f;
    [SerializeField] private float groundedRadius = 0.25f;
    [SerializeField] private LayerMask groundLayers;

    private Vector2 _moveInput;
    private bool _holdingJump;
    private bool _holdingSprint;
    private bool IsGrounded => cc.isGrounded;
    private bool _lockSwitch;

    private float _verticalVelocity;
    private Vector3 _movementVelocity;

    // -------------------------------
    // State Machine
    // -------------------------------
    public override void LoadState(MovementManager manager, InputDirector director)
    {
        _player = manager.GetComponent<Player>();
        _animator = manager.GetComponent<AnimationsManager>();
        cc = manager.GetComponent<CharacterController>();
        
        if (!_player.HasAuthority)
            return;
        
        Controller = manager;
        Director = director ?? InputDirector.Instance;

        
        if (!cc)
            Debug.LogError("Character Controller not found for this person state, should be added via MovementManager");

        characterOrientation = manager.GetComponentInChildren<CharacterOrientation>().transform;
        cameraTransform = _player.GetCamera().transform;

        LocalData = _player.GetData("Walking");
        acceleration = LocalData.acceleration;
        walkSpeed = LocalData.walkingSpeed;
        sprintSpeed = LocalData.runningSpeed;
        
        // Input events
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
        
        // Setup Animations
        new AnimationsManager.Builder("tps_animator")
            .AddAnimation("Idle", connections: new [] {
                Connection.To("Walking", customCrossfade: 0.06f).When("Walking", true),
            })
            .AddAnimation("Walking", connections: new [] {
                Connection.To("Idle", customCrossfade: 0.2f).When("Walking", false),
                Connection.To("Running", customCrossfade: 0.16f).When("Running", true)
            })
            .AddAnimation("Running", connections: new []
            {
                Connection.To("Walking", customCrossfade: 0.2f).When("Running", false),
                Connection.To("Idle", customCrossfade: 0.2f).When("Walking", false)
            })
            .AddAnimation("Jump", entryCrossfade: 0f, autoNextAnimation: "Fall", lockLayer: true, loops: false)
            .AddAnimation("Fall", connections:
                Connection.To("Idle", customCrossfade: 0.03f).When("Falling", false))
            .AddParameter("Walking")
            .AddParameter("Falling")
            .AddParameter("Running")
            .SetDefaultAnimation(DefaultAnimation)
            .Build(_animator);
    }

    public override void EnterState()
    {
        // dear future self, looking for optimizations, no need to reset values here because:
        // A. they are already defaulted to 0, we make a new tpstate each time (we don't swap states often so it's fine)
        // B. this causes a bug where the player would stop moving because this overrides the inputvalue.
        // EnterState happens after OnPlayerMoved.
        // _moveInput = Vector2.zero;
        // _currentVelocity = Vector3.zero;
        
        _verticalVelocity = -2f;
    }

    public override void UpdateState()
    {
        HandleMovementInput();
        HandleJumpAndGravity();
        MovePlayer();
    }

    public override void FixedUpdate()
    {
        
    }

    public override void CleanState()
    {
        // _currentVelocity = Vector3.zero;
        // _verticalVelocity = 0f;

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

    private void HandleMovementInput()
    {
        // don't move if input disabled
        if (!CanMove)
        {
            _movementVelocity.x = 0f;
            _movementVelocity.z = 0f;
            return;
        }

        // get inputs from player
        Vector3 inputDir = new Vector3(_moveInput.x, 0f, _moveInput.y);
        Vector3 moveDir = Vector3.zero;

        // set move direction towards where camera looks
        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        moveDir = (camForward * inputDir.z) + (camRight * inputDir.x);
        moveDir.Normalize();

        // character orientation -> rotate just the model towards camera
        Quaternion targetRotation;

        if (_lockSwitch)
        {
            targetRotation = Quaternion.LookRotation(new Vector3(camForward.x, 0f, camForward.z));
            RotateCharacterImmediatly(targetRotation);
        }
        else if (_moveInput.magnitude > 0.01f)
        {
            // rotate character model towards movement direction
            targetRotation = Quaternion.LookRotation(new Vector3(moveDir.x, 0f, moveDir.z));
            RotateCharacterTowards(targetRotation);
        }
        
        // move towards new velocity
        float targetSpeed = _holdingSprint && CanRun ? sprintSpeed : walkSpeed;
        Vector3 targetHorizontalVelocity = moveDir * targetSpeed;
        Vector3 currentHorizontal = new Vector3(_movementVelocity.x, 0f, _movementVelocity.z);
        
        currentHorizontal = Vector3.Lerp(currentHorizontal, targetHorizontalVelocity,
            acceleration * Time.deltaTime);

        _movementVelocity.x = currentHorizontal.x;
        _movementVelocity.z = currentHorizontal.z;
    }

    private void RotateCharacterTowards(Quaternion targetRot)
    {
        characterOrientation.rotation = Quaternion.Slerp(
            characterOrientation.rotation,
            targetRot,
            rotationSpeed * Time.deltaTime);
    }

    private void RotateCharacterImmediatly(Quaternion targetRot)
    {
        characterOrientation.rotation = targetRot;
    }

    private void HandleJumpAndGravity()
    {
        // constant but limited force if on ground
        if (IsGrounded && _verticalVelocity < 0f)
        {
            _verticalVelocity = -2f;
            
            _animator.SetBool("Falling", false);
        }
        else
            _animator.SetBool("Falling", true);
        
        // jump
        if (IsGrounded && _holdingJump && CanMove)
        {
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _animator.Play("Jump");
        }

        // new velocity, clamp if reached max
        _verticalVelocity += gravity * Time.deltaTime;
        _verticalVelocity = Mathf.Clamp(_verticalVelocity, minGravity, maxGravity);
        
        // apply new velocity
        _movementVelocity.y = _verticalVelocity;
    }
    
    // for knockback
    public void AddVerticalVelocity(float knockbackUpForce)
    {
        _verticalVelocity += knockbackUpForce;
    }

    /// <summary>
    /// Default Animation Logic
    /// </summary>
    private void DefaultAnimation()
    {
        if (_animator.GetBool("Falling"))
            _animator.Play("Fall");
        else
            _animator.Play("Idle");
    }
    
    // -------------------------------
    // Input Events
    // -------------------------------
    private void OnPlayerMoved(Vector2 movementValue)
    {
        _moveInput = movementValue;
        _animator.SetBool("Walking", true);
    }

    private void OnPlayerMovedFinished()
    {
        _moveInput = Vector2.zero;
        _animator.SetBool("Walking", false);
    }

    private void OnPlayerJumpStarted() => _holdingJump = true;
    private void OnPlayerJumpStopped() => _holdingJump = false;

    private void OnPlayerRunStarted()
    {
        _holdingSprint = true;
        _lockSwitch = !_lockSwitch;
        
        _animator.SetBool("Running", true);
    }

    private void OnPlayerRunStopped()
    {
        _holdingSprint = false;
        
        _animator.SetBool("Running", false);
    }

    private void OnEnablePlayerMovement() => CanMove = true;
    private void OnDisablePlayerMovement() => CanMove = false;

    private void OnPlayerRunEnabled() => CanRun = true;
    private void OnPlayerRunDisabled() => CanRun = false;
}
