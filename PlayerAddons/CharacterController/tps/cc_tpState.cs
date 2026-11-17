using System;
using SHG.AnimatorCoder;
using UnityEngine;

[Serializable]
public class cc_tpState : MovementState
{
    /// <summary>Accepted takeoff, after grounded/input checks; presentation only.</summary>
    public static event Action<Player> Jumped;
    public static event Action<Player> Landed;
    [Header("Definition")]
    public override MovementComponentType ComponentType => MovementComponentType.CharacterController;
    [SerializeField] public const string StateName = "cc ThirdPerson";
    
    [Header("Assignables")]
    [SerializeField] private CharacterController cc;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform characterOrientation;
    private Player _player;
    private AnimationsManager _animator;
    private ICharacterControllerTraversal _traversal;
    private IMovementSpeedModifier[] _speedModifiers;
    private IWalkingSpeedOverride[] _walkingSpeedOverrides;
    private ICharacterAirJump _airJump;
    private bool _jumpPressed;
    private float _airJumpRiseLimit;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 2.8f;
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
    private bool _lockSwitch = false; // Free orbit until the player explicitly toggles camera lock.

    private ICharacterGravityModifier[] _gravityModifiers;
    private ICharacterControllerVelocityOverride _velocityOverride;
    private bool _retainTraversalMomentum;
    public Vector3 CurrentVelocity => _movementVelocity;
    public Vector3 ResolvedVelocity { get; private set; }
    private float _verticalVelocity;
    private Vector3 _movementVelocity;

    // Actual controller travel from our movement step, excluding external knockback.
    public float HorizontalSpeed { get; private set; }

    // -------------------------------
    // State Machine
    // -------------------------------
    public override void LoadState(MovementManager manager, InputDirector director)
    {
        _player = manager.GetComponent<Player>();
        _animator = manager.GetComponent<AnimationsManager>();
        cc = manager.GetComponent<CharacterController>();
        _traversal = manager.GetComponent<ICharacterControllerTraversal>();
        _velocityOverride = manager.GetComponent<ICharacterControllerVelocityOverride>();
        _speedModifiers = manager.GetComponents<IMovementSpeedModifier>();
        _walkingSpeedOverrides = manager.GetComponents<IWalkingSpeedOverride>();
        _airJump = manager.GetComponent<ICharacterAirJump>();
        _gravityModifiers = manager.GetComponents<ICharacterGravityModifier>();
        
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
        Director.OnLockSwitchPressed += OnLockSwitchPressed;
        Director.OnEnablePlayerMovement += OnEnablePlayerMovement;
        Director.OnDisablePlayerMovement += OnDisablePlayerMovement;
        Director.OnPlayerRunEnabled += OnPlayerRunEnabled;
        Director.OnPlayerRunDisabled += OnPlayerRunDisabled;
        
        // Subscribe to player data changes
        LocalData.OnAccelerationChanged += newAcceleration => acceleration = newAcceleration;
        LocalData.OnWalkingSpeedChanged += newWalkingSpeed => walkSpeed = newWalkingSpeed;
        LocalData.OnRunningSpeedChanged += newRunningSpeed => sprintSpeed = newRunningSpeed;
        
        // Setup Animations
        new AnimationsManager.Builder()
            .AddAnimation("Idle", connections: new [] {
                Connection.To("Fall").When("Falling", true),
                Connection.To("Walking", customCrossfade: 0.06f).When("Walking", true),
            })
            .AddAnimation("Walking", connections: new [] {
                Connection.To("Fall", customCrossfade: 0.24f).When("Falling", true),
                Connection.To("Idle", customCrossfade: 0.2f).When("Walking", false),
                Connection.To("Running", customCrossfade: 0.22f).When("Running", true)
            })
            .AddAnimation("Running", connections: new []
            {
                Connection.To("Fall", customCrossfade: 0.24f).When("Falling", true),
                Connection.To("Walking", customCrossfade: 0.2f).When("Running", false),
                Connection.To("Idle", customCrossfade: 0.2f).When("Walking", false)
            })
            .AddAnimation("Jump", entryCrossfade: 0f, autoNextCrossfade: 4f, autoNextAnimation: "Fall", lockLayer: true, loops: false)
            .AddAnimation("Fall", connections:
                Connection.To("Idle", customCrossfade: 0.1f).When("Falling", false))
            .AddParameter("Walking")
            .AddParameter("Falling")
            .AddParameter("Running")
            .UseFixedTimeCrossfades("Idle", "Walking", "Running")
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
        _airJump?.UpdateAirJumpState(IsGrounded, CanMove);
        HandleMovementInput();
        HandleJumpAndGravity();
        _jumpPressed = false;
        if (CanMove && _velocityOverride != null && _velocityOverride.TryGetTraversalVelocity(Time.deltaTime, out var traversalVelocity))
        {
            _movementVelocity = traversalVelocity;
            _verticalVelocity = traversalVelocity.y;
            _retainTraversalMomentum = true;
        }
        MovePlayer();
        UpdateLocomotionAnimation();
    }

    public override void FixedUpdate()
    {
        
    }

    public override void CleanState()
    {
        // no need to clear velocity, read more in enterstate.
        // _currentVelocity = Vector3.zero;
        // _verticalVelocity = 0f;
        
        _airJump?.CancelAirJump();
        _airJumpRiseLimit = 0f;
        _jumpPressed = false;
        // clear input
        Director.OnPlayerMoved -= OnPlayerMoved;
        Director.OnPlayerMovedFinished -= OnPlayerMovedFinished;
        Director.OnPlayerJumpStarted -= OnPlayerJumpStarted;
        Director.OnPlayerJumpStopped -= OnPlayerJumpStopped;
        Director.OnPlayerRunStarted -= OnPlayerRunStarted;
        Director.OnPlayerRunStopped -= OnPlayerRunStopped;
        Director.OnLockSwitchPressed -= OnLockSwitchPressed;
        Director.OnEnablePlayerMovement -= OnEnablePlayerMovement;
        Director.OnDisablePlayerMovement -= OnDisablePlayerMovement;
        Director.OnPlayerRunEnabled -= OnPlayerRunEnabled;
        Director.OnPlayerRunDisabled -= OnPlayerRunDisabled;
        
        // clear animatins
        _animator.SetBool("Walking", false);
        _animator.SetBool("Falling", false);
        _animator.SetBool("Running", false);
        HorizontalSpeed = 0f;
    }

    public override void RefreshPlayerReferences()
    {
        characterOrientation = Controller.GetComponentInChildren<CharacterOrientation>().transform;
    }

    // -------------------------------
    // Core Movement
    // -------------------------------
    private void MovePlayer()
    {
        HorizontalSpeed = 0f;
        if (!CanMove)
            return;
        
        bool groundedBefore = cc.isGrounded;
        Vector3 before = cc.transform.position;
        cc.Move(_movementVelocity * Time.deltaTime);
        Vector3 travelled = cc.transform.position - before;
        ResolvedVelocity = travelled / Mathf.Max(Time.deltaTime, .0001f);
        if (_retainTraversalMomentum)
        {
            // Keep collision-resolved travel, not the requested pull speed or a release boost.
            _movementVelocity = travelled / Mathf.Max(Time.deltaTime, .0001f);
            _verticalVelocity = _movementVelocity.y;
            if (cc.isGrounded && _verticalVelocity <= 0f) _retainTraversalMomentum = false;
        }
        HorizontalSpeed = new Vector2(travelled.x, travelled.z).magnitude / Mathf.Max(Time.deltaTime, .0001f);
        if (!groundedBefore && cc.isGrounded) Landed?.Invoke(_player);
    }

    private void UpdateLocomotionAnimation()
    {
        // Sprint presentation responds to input without waiting for acceleration.
        bool running = CanMove && CanRun && _holdingSprint && _moveInput.sqrMagnitude > .0001f;
        // Preserve the travel-based walk/braking behaviour when sprint is not requested.
        bool moving = running || HorizontalSpeed > (_animator.GetBool("Walking") ? .12f : .2f);
        _animator.SetBool("Walking", moving);
        _animator.SetBool("Running", running);
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

        // Use camera yaw so pitch cannot reduce horizontal speed or skew diagonals.
        // Clamp only inputs above unit length; preserve partial analog movement.
        Vector2 input = Vector2.ClampMagnitude(_moveInput, 1f);
        Quaternion cameraYaw = Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);
        Vector3 camForward = cameraYaw * Vector3.forward;
        Vector3 moveDir = cameraYaw * new Vector3(input.x, 0f, input.y);

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
        float controlAcceleration = acceleration;
        if (!IsGrounded && _traversal != null &&
            _traversal.TryGetAirControl(out float airSpeed, out float airAcceleration))
        {
            targetSpeed = airSpeed;
            controlAcceleration = airAcceleration;
        }
        if (_walkingSpeedOverrides != null)
            foreach (var speedOverride in _walkingSpeedOverrides)
                if (speedOverride.TryGetWalkingSpeedOverride(walkSpeed, out float overrideSpeed))
                    targetSpeed = overrideSpeed;
        // Derive from the current base speed; never mutate the shared Walking data asset.
        foreach (var modifier in _speedModifiers)
            targetSpeed *= modifier.MovementSpeedMultiplier;
        Vector3 targetHorizontalVelocity = moveDir * targetSpeed;
        Vector3 currentHorizontal = new Vector3(_movementVelocity.x, 0f, _movementVelocity.z);
        
        if (_retainTraversalMomentum && !IsGrounded)
        {
            // No input means ballistic carry. Input steers without deleting launch momentum.
            float limit = Mathf.Max(currentHorizontal.magnitude, targetSpeed);
            currentHorizontal = Vector3.ClampMagnitude(currentHorizontal + moveDir * controlAcceleration * Time.deltaTime, limit);
        }
        else
            currentHorizontal = Vector3.Lerp(currentHorizontal, targetHorizontalVelocity,
                controlAcceleration * Time.deltaTime);

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
        if (_traversal != null && _traversal.TryUpdateVerticalVelocity(
                ref _verticalVelocity, _holdingJump, IsGrounded, CanMove, Time.deltaTime))
        {
            _animator.SetBool("Falling", !IsGrounded || _verticalVelocity > 0f);
            _movementVelocity.y = _verticalVelocity;
            return;
        }
        float gravityScale = 1f, jumpVelocityScale = 1f, fallLimit = minGravity;
        if (_gravityModifiers != null)
            foreach (var modifier in _gravityModifiers)
                if (modifier.TryGetGravityModifiers(out float g, out float j, out float fall))
                {
                    gravityScale *= Mathf.Clamp(g, .01f, 5f);
                    jumpVelocityScale *= Mathf.Clamp(j, .1f, 3f);
                    fallLimit = Mathf.Max(fallLimit, -Mathf.Max(.1f, fall));
                }
        // constant but limited force if on ground
        if (IsGrounded && _verticalVelocity < 0f)
        {
            _verticalVelocity = -2f;
            _airJumpRiseLimit = 0f;
            
            _animator.SetBool("Falling", false);
        }
        else
            _animator.SetBool("Falling", true);
        
        // jump
        if (IsGrounded && _holdingJump && CanMove)
        {
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity) * jumpVelocityScale;
            _animator.Play("Jump");
            Jumped?.Invoke(_player);
        }

        // Only a fresh airborne press can spend a charge. Hook's pre-jump consumer
        // retains priority, and holding the first jump cannot trigger the second.
        if (!IsGrounded && _jumpPressed && CanMove && Director.CanJump && _airJump != null &&
            _airJump.TryAirJump(-gravity * gravityScale, out float airJumpVelocity))
        {
            _verticalVelocity = airJumpVelocity;
            _airJumpRiseLimit = airJumpVelocity;
            _retainTraversalMomentum = false;
            _animator.Play("Jump");
            Jumped?.Invoke(_player);
        }

        // new velocity, clamp if reached max
        _verticalVelocity += gravity * gravityScale * Time.deltaTime;
        _verticalVelocity = Mathf.Clamp(_verticalVelocity, fallLimit, Mathf.Max(maxGravity, _airJumpRiseLimit));
        
        // apply new velocity
        _movementVelocity.y = _verticalVelocity;
    }
    
    // for knockback
    public void AddVerticalVelocity(float knockbackUpForce)
    {
        _verticalVelocity += knockbackUpForce;
    }

    public void ResetAfterTeleport()
    {
        _airJump?.CancelAirJump();
        _airJumpRiseLimit = 0f;
        _jumpPressed = false;
        _velocityOverride?.CancelTraversalVelocity();
        _retainTraversalMomentum = false;
        _verticalVelocity = -2f;
        _movementVelocity = Vector3.zero;
        ResolvedVelocity = Vector3.zero;
        HorizontalSpeed = 0f;
    }

    /// <summary>
    /// Default Animation Logic
    /// </summary>
    private void DefaultAnimation()
    {
        if (_animator.GetBool("Falling"))
            _animator.Play("Fall");
        else if (_animator.GetBool("Walking"))
            _animator.Play(_animator.GetBool("Running") ? "Running" : "Walking");
        else
            _animator.Play("Idle");
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

    private void OnPlayerJumpStarted()
    {
        _jumpPressed = !_holdingJump;
        _holdingJump = true;
    }
    private void OnPlayerJumpStopped() => _holdingJump = false;

    private void OnPlayerRunStarted()
    {
        _holdingSprint = true;
    }

    private void OnLockSwitchPressed()
    {
        if (CanMove)
            _lockSwitch = !_lockSwitch;
    }

    private void OnPlayerRunStopped()
    {
        _holdingSprint = false;
    }

    private void OnEnablePlayerMovement() => CanMove = true;
    private void OnDisablePlayerMovement()
    {
        CanMove = false;
        _jumpPressed = false;
        _airJump?.CancelAirJump();
    }

    private void OnPlayerRunEnabled() => CanRun = true;
    private void OnPlayerRunDisabled() => CanRun = false;
}
