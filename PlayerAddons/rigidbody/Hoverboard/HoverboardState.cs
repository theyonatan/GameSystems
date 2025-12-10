using System;
using System.Collections;
using UnityEngine;

[System.Serializable]
public class HoverboardState : MovementState
{
    float playerHeight = 2f;

    [Header("Assignables")]
    [SerializeField] Transform orientation;
    [SerializeField] Animator character;
    [SerializeField] Transform playerTransform;
    [SerializeField] Rigidbody rb;

    [Header("InputValues")]
    float horizontalMovement;
    float verticalMovement;

    bool holdingJump = false;
    bool holdingSprint = false;

    [Header("Movement")]
    [SerializeField] float moveSpeed = 6f;
    [SerializeField] float airMultiplier = 0.4f;
    float movementMultiplier = 10f;
    bool canMove = true;

    [Header("Sprinting")]
    [SerializeField] float walkSpeed = 6f;
    [SerializeField] float sprintSpeed = 10f;
    [SerializeField] float acceleration = 50f;

    [Header("Jumping")]
    public float jumpForce = 14f;
    public bool isJumping = false;
    [SerializeField] private float wallRunGravity = 1f;
    [SerializeField] private float wallRunJumpForce = 6f;

    [Header("Drag")]
    [SerializeField] float groundDrag = 6f;
    [SerializeField] float airDrag = 2f;

    [Header("Ground Detection")]
    [SerializeField] Transform groundCheck;
    [SerializeField] float groundDistance = 0.1f;
    public bool isGrounded { get; private set; }

    public Vector3 moveDirection;
    Vector3 slopeMoveDirection;

    public bool groundedCheck;
    RaycastHit slopeHit;

    // -------------------------------
    // State Machine
    // -------------------------------


    public override MovementComponentType ComponentType => MovementComponentType.Rigidbody;

    public override void LoadState(MovementManager manager, InputDirector director)
    {
        // Player Family
        Controller = manager;
        Director = director;

        // Assignables
        playerTransform = manager.transform.parent.transform;
        orientation = manager.transform.parent.GetComponentInChildren<Animator>().gameObject.transform;

        // Load movement data
        LocalData = Resources.Load<PlayerStateData>("playerStates/normalPlayer");

        // Input Events
        Director.OnPlayerMoved += _director_OnPlayerMoved;
        Director.OnPlayerMovedFinished += _director_OnPlayerMovedFinished;
        Director.OnPlayerJumpStarted += _director_OnPlayerJumpStarted;
        Director.OnPlayerJumpStopped += _director_OnPlayerJumpStopped;
        Director.OnPlayerRunStarted += _director_OnPlayerRunStarted;
        Director.OnPlayerRunStopped += _director_OnPlayerRunStopped;

        Director.OnEnablePlayerMovement += _director_OnEnablePlayerMovement;
        Director.OnDisablePlayerMovement += _director_OnDisablePlayerMovement;
        Director.OnCameraMoved += _director_OnCameraMoved;
    }

    private void _director_OnCameraMoved(Vector2 obj)
    {
        Debug.Log("Not Reloading Anything Anymore!");
    }

    public override void EnterState()
    {
        character = Controller.transform.parent.GetComponentInChildren<Animator>();

        // run enter state animations here

        groundCheck = character.gameObject.GetComponentInChildren<PlayerGround>().transform;
        rb = Controller.gameObject.GetComponent<Rigidbody>();

        rb.freezeRotation = true;

        Debug.Log("Starting HoverBoard State!");
    }


    public override void UpdateState()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, ~3);

        groundedCheck = isGrounded;

        ControlDrag();
        ControlSpeed();

        if (holdingJump && isGrounded && !isJumping)
        {
            isJumping = true;
            Controller.StartCoroutine(ResetJump());
            jump();
        }

        slopeMoveDirection = Vector3.ProjectOnPlane(moveDirection, slopeHit.normal);
    }

    public override void FixedUpdate()
    {
        if (canMove)
            MovePlayer();
    }

    public override void CleanState()
    {
        AnimationManager.Instance.SetAnimatorValue("MoveX", 0f);
        AnimationManager.Instance.SetAnimatorValue("MoveY", 0f);

        Vector3 movementVelocity = new(0f, rb.linearVelocity.y, 0f);
        rb.linearVelocity = movementVelocity;
    }


    // -------------------------------
    // Movement Functions
    // -------------------------------

    private bool OnSlope()
    {
        if (Physics.Raycast(playerTransform.position, Vector3.down, out slopeHit, playerHeight / 2 + 0.5f))
        {
            if (slopeHit.normal != Vector3.up)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        return false;
    }

    void jump()
    {
        if (isGrounded)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            rb.AddForce(playerTransform.up * jumpForce, ForceMode.Impulse);
        }
    }

    void ControlSpeed()
    {
        if (holdingSprint && isGrounded)
        {
            moveSpeed = Mathf.Lerp(moveSpeed, sprintSpeed, acceleration * Time.deltaTime);
        }
        else
        {
            moveSpeed = Mathf.Lerp(moveSpeed, walkSpeed, acceleration * Time.deltaTime);
        }
    }

    void ControlDrag()
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

    void MovePlayer()
    {   
        moveDirection = orientation.forward * verticalMovement + orientation.right * horizontalMovement;

        if (isGrounded && !OnSlope())
        {
            rb.AddForce(moveDirection.normalized * moveSpeed * movementMultiplier, ForceMode.Acceleration);
        }
        else if (isGrounded && OnSlope())
        {
            rb.AddForce(slopeMoveDirection.normalized * moveSpeed * movementMultiplier, ForceMode.Acceleration);
        }
        else if (!isGrounded)
        {
            Vector3 downfallForce = moveDirection.normalized * moveSpeed * movementMultiplier * airMultiplier;
            rb.AddForce(downfallForce, ForceMode.Acceleration);
        }
    }

    public void StartWallRun(bool wallLeft, bool wallRight, RaycastHit leftWallHit, RaycastHit rightWallHit)
    {
        rb.useGravity = false;

        rb.AddForce(Vector3.down * wallRunGravity, ForceMode.Force);

        if (holdingJump)
        {
            if (wallLeft)
            {
                Vector3 wallRunJumpDirection = playerTransform.up + leftWallHit.normal;
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                rb.AddForce(wallRunJumpDirection * wallRunJumpForce * 100, ForceMode.Force);
            }
            else if (wallRight)
            {
                Vector3 wallRunJumpDirection = playerTransform.up + rightWallHit.normal;
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                rb.AddForce(wallRunJumpDirection * wallRunJumpForce * 100, ForceMode.Force);
            }
        }
    }

    public void StopWallRun()
    {
        rb.useGravity = true;
    }

    private IEnumerator ResetJump()
    {
        yield return new WaitForSeconds(0.5f);
        isJumping = false;
    }

    // -------------------------------
    // Input Events
    // -------------------------------
    private void _director_OnPlayerMoved(Vector2 movementValue)
    {
        horizontalMovement = movementValue.x;
        verticalMovement = movementValue.y;

        moveDirection = orientation.forward * verticalMovement + orientation.right * horizontalMovement;
    }

    private void _director_OnPlayerMovedFinished()
    {
        horizontalMovement = 0f;
        verticalMovement = 0f;

        moveDirection = orientation.forward * verticalMovement + orientation.right * horizontalMovement;
    }

    private void _director_OnPlayerJumpStarted()
    {
        holdingJump = true;
    }

    private void _director_OnPlayerJumpStopped()
    {
        holdingJump = false;
    }

    private void _director_OnPlayerRunStarted()
    {
        holdingSprint = true;
    }

    private void _director_OnPlayerRunStopped()
    {
        holdingSprint = false;
    }

    private void _director_OnEnablePlayerMovement()
    {
        canMove = true;
    }

    private void _director_OnDisablePlayerMovement()
    {
        canMove = false;
    }
}
