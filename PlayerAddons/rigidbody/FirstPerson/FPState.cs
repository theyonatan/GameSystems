using System;
using System.Collections;
using UnityEngine;

[System.Serializable]
public class FpState : MovementState
{
    [Header("Currently Using: FPS MovementState")]
    public const string CurrentStateName = "FPState";
    private float _playerHeight = 2f;

    [Header("Assignables")]
    [SerializeField] private Transform orientation;
    [SerializeField] private Animator character;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Rigidbody rb;
    private AnimationManager _animationManager;

    [Header("InputValues")]
    public float _horizontalMovement;
    public float _verticalMovement;

    private bool _holdingJump = false;
    private bool _holdingSprint = false;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float airMultiplier = 0.4f;
    private float _movementMultiplier = 10f;
    public Vector3 MoveDirection;
    private Vector3 _normalizedMovementDirection;
    private Vector3 _correctedWallMovement = Vector3.zero;

    private Vector3 _slopeMoveDirection;
    private RaycastHit _slopeHit;

    [Header("Sprinting")]
    [SerializeField] private float walkSpeed = 6f;
    [SerializeField] private float sprintSpeed = 10f;
    [SerializeField] private float acceleration = 50f;

    [Header("Jumping")]
    public float JumpForce = 14f;
    public bool IsJumping = false;

    [Header("Drag")]
    [SerializeField] private float groundDrag = 6f;
    [SerializeField] private float airDrag = 2f;

    [Header("Ground Detection")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundDistance = 0.1f;
    [SerializeField] private bool isGrounded;
    [SerializeField] private LayerMask groundLayer;
    
    [Header("Climb Steps")]
    [SerializeField] private float stepHeight = 0.5f;            // max climbable step
    [SerializeField] private float stepCheckDistance = 0.5f;    // how far in front to step-check
    private bool _isStepping = false;
    private Vector3 _stepStartPos;
    private Vector3 _stepTargetPos;
    private float _stepProgress = 0f;
    private float detectedStepHeight;
    private const float StepDuration = 0.1f; // Smooth climb time
    private const float StepThreshold = 0.1f;

    // -------------------------------
    // State Machine
    // -------------------------------


    public override MovementComponentType ComponentType => MovementComponentType.Rigidbody;

    public override void LoadState(MovementManager manager, InputDirector director)
    {
        // Player Family
        Controller = manager;
        Director = InputDirector.Instance;
        _animationManager = AnimationManager.Instance;

        // Assignables
        playerTransform = manager.transform.transform;
        orientation = manager.transform.GetComponentInChildren<Animator>().gameObject.transform;
        
        // Load movement data
        LocalData = Resources.Load<PlayerStateData>("playerStates/normalPlayer");
        groundLayer = LayerMask.NameToLayer("Ground");

        // Input Events`
        Director.OnPlayerMoved += _director_OnPlayerMoved;
        Director.OnPlayerMovedFinished += _director_OnPlayerMovedFinished;
        Director.OnPlayerJumpStarted += _director_OnPlayerJumpStarted;
        Director.OnPlayerJumpStopped += _director_OnPlayerJumpStopped;
        Director.OnPlayerRunStarted += _director_OnPlayerRunStarted;
        Director.OnPlayerRunStopped += _director_OnPlayerRunStopped;

        Director.OnEnablePlayerMovement += _director_OnEnablePlayerMovement;
        Director.OnDisablePlayerMovement += _director_OnDisablePlayerMovement;
        Director.OnPlayerRunEnabled += _director_OnPlayerRunEnabled;
        Director.OnPlayerRunDisabled += _director_OnPlayerRunDisabled;
    }

    public override void EnterState()
    {
        character = Controller.transform.GetComponentInChildren<Animator>();

        // run enter state animations here

        groundCheck = character.gameObject.GetComponentInChildren<PlayerGround>().transform;
        rb = Controller.gameObject.GetComponent<Rigidbody>();

        rb.freezeRotation = true;

        Debug.Log("Starting FPS State!");
    }


    public override void UpdateState()
    {
        UpdateMovementValues();
        ControlDrag();
        ControlSpeed();
        ControlJump();
    }

    public override void FixedUpdate()
    {
        if (!CanMove) return;
        
        HandleStepClimbingBeforeMovement();
        MovePlayer();
    }

    public override void CleanState()
    {
        _animationManager.SetAnimatorValue("MoveX", 0f);
        _animationManager.SetAnimatorValue("MoveY", 0f);
        _animationManager.SetAnimatorValue("Moving", false);

        Vector3 movementVelocity = new(0f, rb.linearVelocity.y, 0f);
        rb.linearVelocity = movementVelocity;

        // unsubscribe from Input Events
        Director.OnPlayerMoved -= _director_OnPlayerMoved;
        Director.OnPlayerMovedFinished -= _director_OnPlayerMovedFinished;
        Director.OnPlayerJumpStarted -= _director_OnPlayerJumpStarted;
        Director.OnPlayerJumpStopped -= _director_OnPlayerJumpStopped;
        Director.OnPlayerRunStarted -= _director_OnPlayerRunStarted;
        Director.OnPlayerRunStopped -= _director_OnPlayerRunStopped;

        Director.OnEnablePlayerMovement -= _director_OnEnablePlayerMovement;
        Director.OnDisablePlayerMovement -= _director_OnDisablePlayerMovement;
        Director.OnPlayerRunEnabled -= _director_OnPlayerRunEnabled;
        Director.OnPlayerRunDisabled -= _director_OnPlayerRunDisabled;
    }


    // -------------------------------
    // Movement Functions
    // -------------------------------

    private bool OnSlope()
    {
        if (Physics.Raycast(playerTransform.position, Vector3.down, out _slopeHit, _playerHeight / 2 + 0.5f))
        {
            if (_slopeHit.normal != Vector3.up)
            {
                return true;
            }
        }
        return false;
    }

    private void ControlJump()
    {
        if (!_holdingJump || !isGrounded || IsJumping) return;
        
        IsJumping = true;
        Controller.StartCoroutine(ResetJump());
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        rb.AddForce(playerTransform.up * JumpForce, ForceMode.Impulse);
    }

    private void ControlSpeed()
    {
        if (CanRun && _holdingSprint && isGrounded)
        {
            moveSpeed = Mathf.Lerp(moveSpeed, sprintSpeed, acceleration * Time.deltaTime);
        }
        else
        {
            moveSpeed = Mathf.Lerp(moveSpeed, walkSpeed, acceleration * Time.deltaTime);
        }
    }

    private void UpdateMovementValues()
    {
        MoveDirection = orientation.forward * _verticalMovement + orientation.right * _horizontalMovement;
        _normalizedMovementDirection = MoveDirection.normalized;
        _slopeMoveDirection = Vector3.ProjectOnPlane(MoveDirection, _slopeHit.normal);
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, ~groundLayer);
    }
    
    private void ControlDrag()
    {
        if (isGrounded)
        {
            rb.linearDamping = groundDrag;
        }
        else
        {
            rb.linearDamping = airDrag;
        }
    }

    private void MovePlayer()
    {
        MoveDirection = _correctedWallMovement != Vector3.zero ? _correctedWallMovement : MoveDirection;
        
        switch (isGrounded)
        {
            case true when OnSlope():
                rb.AddForce(_slopeMoveDirection.normalized * (moveSpeed * _movementMultiplier), ForceMode.Acceleration);
                break;
            case true when !OnSlope():
                rb.AddForce(MoveDirection.normalized * (moveSpeed * _movementMultiplier), ForceMode.Acceleration);
                break;
            case false:
            {
                Vector3 downfallForce = MoveDirection.normalized * (moveSpeed * _movementMultiplier * airMultiplier);
                rb.AddForce(downfallForce, ForceMode.Acceleration);
                break;
            }
        }
        
        _correctedWallMovement = Vector3.zero;
    }

    /// <summary>
    /// detect and climb steps.
    /// must be called before movement, for this moves on the y-axis before bumping into the step.
    /// </summary>
    private void HandleStepClimbingBeforeMovement()
    {
        DetectStep();
        
        if (_isStepping)
        {
            StepForward();
        }
    }

    private void DetectStep()
    {
        // check if we got a step forward:
        Vector3 playerFootOrigin = playerTransform.position + Vector3.up * StepThreshold;
        
        // raycast low step:
        if (!Physics.Raycast(playerFootOrigin, _normalizedMovementDirection, out RaycastHit hit, stepCheckDistance,
                ~groundLayer)) return;
        
        // detect if step is climbable:
        Vector3 maxstepCheckOrigin = hit.point + Vector3.up * stepHeight;
        if (!Physics.Raycast(maxstepCheckOrigin, Vector3.down, out RaycastHit stepHit, stepHeight,
                ~groundLayer)) return;
        
        // check if step is worthy of snapping onto:
        detectedStepHeight = stepHit.point.y - playerTransform.position.y;
        if (!(detectedStepHeight > StepThreshold) || !(detectedStepHeight <= stepHeight)) return;
        
        // prepare jump onto step:
        _stepStartPos = rb.position;
        Vector3 forwardOffset = Vector3.ProjectOnPlane(_normalizedMovementDirection, Vector3.up) * 0.1f;
        _stepTargetPos = stepHit.point + forwardOffset;
        _stepProgress = 0f;
        _isStepping = true;
    }

    private void StepForward()
    {
        // lerp on the y-axis onto the step:
        _stepProgress += Time.fixedDeltaTime / StepDuration;
        // Scale factor: 0 for 0.5 height (full lerp), 1 for 0 height (instant jump)
        float stepFactor = Mathf.Clamp01(1f - (detectedStepHeight / stepHeight));
        // Make a non-linear influence (optional, use Mathf.Pow to exaggerate effect)
        float sharpness = Mathf.Lerp(_stepProgress, 1f, stepFactor);
        // final lerp
        float newYPosition = Mathf.Lerp(_stepStartPos.y, _stepTargetPos.y, sharpness);
        
        // move the player vertically to the step
        Vector3 nextPosition = rb.position;
        nextPosition.y = newYPosition;
        rb.MovePosition(nextPosition);

        // I finished stepping up, I'm a big man now.
        if (_stepProgress >= 1f)
            _isStepping = false;
    }
    
    // /// <summary>
    // /// events for detecting collision:
    // /// - wall check (walking into walls)
    // /// </summary>
    // /// <param name="collision"></param>
    // public override void OnCollisionStay(Collision collision)
    // {
    //     // foreach wall I collide with
    //     foreach (ContactPoint contact in collision.contacts)
    //     {
    //         // get the normal, and the projection to it
    //         Vector3 wallNormal = contact.normal;
    //         Vector3 projectedMoveDirection = Vector3.ProjectOnPlane(_normalizedMovementDirection, wallNormal);
    //
    //         // add movement to that projection
    //         _correctedWallMovement += projectedMoveDirection.normalized;
    //     }
    // }

    private IEnumerator ResetJump()
    {
        yield return new WaitForSeconds(0.5f);
        IsJumping = false;
    }

    // -------------------------------
    // Input Events
    // -------------------------------
    private void _director_OnPlayerMoved(Vector2 movementValue)
    {
        _horizontalMovement = movementValue.x;
        _verticalMovement = movementValue.y;

        MoveDirection = orientation.forward * _verticalMovement + orientation.right * _horizontalMovement;
    }

    private void _director_OnPlayerMovedFinished()
    {
        _horizontalMovement = 0f;
        _verticalMovement = 0f;

        MoveDirection = orientation.forward * _verticalMovement + orientation.right * _horizontalMovement;
        
        _animationManager.SetAnimatorValue("Moving", false);
    }

    private void _director_OnPlayerJumpStarted()
    {
        _holdingJump = true;
    }

    private void _director_OnPlayerJumpStopped()
    {
        _holdingJump = false;
    }

    private void _director_OnPlayerRunStarted()
    {
        _holdingSprint = true;
        _animationManager.SetAnimatorValue("Moving", true);
    }

    private void _director_OnPlayerRunStopped()
    {
        _holdingSprint = false;
        
        _animationManager.SetAnimatorValue("Moving", false);
    }

    private void _director_OnEnablePlayerMovement()
    {
        CanMove = true;
    }

    private void _director_OnDisablePlayerMovement()
    {
        CanMove = false;
        _animationManager.SetAnimatorValue("Moving", false);
    }

    private void _director_OnPlayerRunEnabled()
    {
        CanRun = true;
    }

    private void _director_OnPlayerRunDisabled()
    {
        CanRun = false;
    }
}
