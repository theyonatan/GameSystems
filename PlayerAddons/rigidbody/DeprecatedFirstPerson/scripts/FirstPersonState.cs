using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FirstPersonState : MovementState
{
    // Assignables
    public Rigidbody rb;
    public Transform playerTransform;
    public Transform orientation;
    public Animator character;

    // values (input)
    float x;
    float y;
    public float xMovement = 0f;
    public float yMovement = 0f;
    public Vector2 testingMovme = Vector2.zero;
    public bool holdingJump = false;
    public bool holdingCrouch = false;

    // values (static)
    [Header("Movement")]
    public float moveSpeed = 4500;
    public float maxSpeed = 20;

    public float counterMovement = 0.175f;
    private float threshold = 0.01f;

    [Header("Jumping")]
    public bool readyToJump = true;
    private float jumpCooldown = 0.25f;
    public float jumpForce = 550f;

    [Header("Grounded Check")]
    public bool grounded;
    public LayerMask whatIsGround;
    public float maxSlopeAngle = 35f;

    [Header("Crouching")]
    private Vector3 crouchScale = new(1, 0.5f, 1);
    private Vector3 playerScale;

    [Header("Sliding")]
    public float slideForce = 400;
    public float slideCounterMovement = 0.2f;
    private Vector3 normalVector = Vector3.up;
    private Vector3 wallNormalVector;


    // State Machine

    public override MovementComponentType ComponentType => MovementComponentType.Rigidbody;

    public override void LoadState(MovementManager manager, InputDirector director)
    {
        // Player Family
        Controller = manager;
        Director = director;

        // Assignables
        playerTransform = manager.transform.parent.transform;
        orientation = manager.transform.parent.GetComponentInChildren<Animator>().gameObject.transform;

        // load movement data
        LocalData = Resources.Load<PlayerStateData>("playerStates/normalPlayer");
        
        // input events
        Director.OnPlayerMoved += _director_OnPlayerMoved;
        Director.OnPlayerMovedFinished += _director_OnPlayerMovedFinished;
        Director.OnPlayerJumpStarted += _director_OnPlayerJumpStarted;
        Director.OnPlayerJumpStopped += _director_OnPlayerJumpStopped;
        Director.OnPlayerCrouchStarted += _director_OnPlayerCrouchStarted;
        Director.OnPlayerCrouchStopped += _director_OnPlayerCrouchStopped;
    }

    public override void EnterState()
    {
        character = Controller.transform.parent.GetComponentInChildren<Animator>();

        // start enter state animation

        rb = Controller.gameObject.GetComponent<Rigidbody>();

        Debug.Log("Entering FPS state!");
    }

    public override void FixedUpdate()
    {
        grounded = isGrounded();
        Movement();
    }

    public override void UpdateState()
    {
        xMovement = x;
        yMovement = y;
    }

    public override void CleanState()
    {
        AnimationManager.Instance.SetAnimatorValue("MoveX", 0f);
        AnimationManager.Instance.SetAnimatorValue("MoveY", 0f);

        Vector3 movementVelocity = new(0f, rb.linearVelocity.y, 0f);
        rb.linearVelocity = movementVelocity;
    }

    // Logic
    // Fixed Update Movement
    private void Movement()
    {
        //Extra gravity
        rb.AddForce(Vector3.down * (Time.deltaTime * 100));

        //Find actual velocity relative to where player is looking
        Vector2 mag = FindVelRelativeToLook();
        float xMag = mag.x, yMag = mag.y;

        //Counteract sliding and sloppy movement
        CounterMovement(xMovement, yMovement, mag);

        //If holding jump && ready to jump, then jump
        if (readyToJump && holdingJump) jump();

        //Set max speed
        float maxSpeed = this.maxSpeed;

        //If sliding down a ramp, add force down so player stays grounded and also builds speed
        if (holdingCrouch && grounded && readyToJump)
        {
            rb.AddForce(Vector3.down * Time.deltaTime * 3000);
            return;
        }

        //If speed is larger than maxspeed, cancel out the input so you don't go over max speed
        if (xMovement > 0 && xMag > maxSpeed) xMovement = 0;
        if (xMovement < 0 && xMag < -maxSpeed) xMovement = 0;
        if (yMovement > 0 && yMag > maxSpeed) yMovement = 0;
        if (yMovement < 0 && yMag < -maxSpeed) yMovement = 0;

        //Some multipliers
        float multiplier = 1f, multiplierV = 1f;

        // Movement in air
        if (!grounded)
        {
            multiplier = 0.5f;
            multiplierV = 0.5f;
        }

        // Movement while sliding
        if (grounded && holdingCrouch) multiplierV = 0f;

        //Apply forces to move player
        rb.AddForce(orientation.forward * yMovement * moveSpeed * Time.deltaTime * multiplier * multiplierV);
        rb.AddForce(orientation.right * xMovement * moveSpeed * Time.deltaTime * multiplier);
    }

    /// <summary>
    /// Find the velocity relative to where the player is looking
    /// Useful for vectors calculations regarding movement and limiting movement
    /// </summary>
    /// <returns></returns>
    public Vector2 FindVelRelativeToLook()
    {
        float lookAngle = orientation.eulerAngles.y;
        float moveAngle = Mathf.Atan2(rb.linearVelocity.x, rb.linearVelocity.z) * Mathf.Rad2Deg;

        float u = Mathf.DeltaAngle(lookAngle, moveAngle);
        float v = 90 - u;

        float magnitue = rb.linearVelocity.magnitude;
        float yMag = magnitue * Mathf.Cos(u * Mathf.Deg2Rad);
        float xMag = magnitue * Mathf.Cos(v * Mathf.Deg2Rad);

        return new Vector2(xMag, yMag);
    }

    // counter sloppy movement and for sliding
    private void CounterMovement(float x, float y, Vector2 mag)
    {
        if (!grounded || holdingJump) return;

        //Slow down sliding
        if (holdingCrouch)
        {
            rb.AddForce(moveSpeed * Time.deltaTime * -rb.linearVelocity.normalized * slideCounterMovement);
            return;
        }

        //Counter movement
        if (Math.Abs(mag.x) > threshold && Math.Abs(x) < 0.05f || (mag.x < -threshold && x > 0) || (mag.x > threshold && x < 0))
        {
            rb.AddForce(moveSpeed * orientation.right * Time.deltaTime * -mag.x * counterMovement);
        }
        if (Math.Abs(mag.y) > threshold && Math.Abs(y) < 0.05f || (mag.y < -threshold && y > 0) || (mag.y > threshold && y < 0))
        {
            rb.AddForce(moveSpeed * orientation.forward * Time.deltaTime * -mag.y * counterMovement);
        }

        //Limit diagonal running. This will also cause a full stop if sliding fast and un-crouching, so not optimal.
        if (Mathf.Sqrt((Mathf.Pow(rb.linearVelocity.x, 2) + Mathf.Pow(rb.linearVelocity.z, 2))) > maxSpeed)
        {
            float fallspeed = rb.linearVelocity.y;
            Vector3 n = rb.linearVelocity.normalized * maxSpeed;
            rb.linearVelocity = new Vector3(n.x, fallspeed, n.z);
        }
    }

    // jump and ground
    private void jump()
    {
        if (grounded && readyToJump)
        {
            readyToJump = false;

            //Add jump forces
            rb.AddForce(Vector2.up * jumpForce * 1.5f);
            rb.AddForce(normalVector * jumpForce * 0.5f);

            //If jumping while falling, reset y velocity.
            Vector3 vel = rb.linearVelocity;
            if (rb.linearVelocity.y < 0.5f)
                rb.linearVelocity = new Vector3(vel.x, 0, vel.z);
            else if (rb.linearVelocity.y > 0)
                rb.linearVelocity = new Vector3(vel.x, vel.y / 2, vel.z);

            Controller.StartCoroutine(ResetJump());
        }
    }

    private IEnumerator ResetJump()
    {
        yield return new WaitForSeconds(jumpCooldown);
        readyToJump = true;
    }

    bool isGrounded()
    {
        float sphereRadius = 0.1f; // Adjust the radius as needed
        Vector3 rayStart = playerTransform.position - new Vector3(0f, 0.3f, 0f);

        Collider[] hitColliders = new Collider[1];
        int numColliders = Physics.OverlapSphereNonAlloc(rayStart, sphereRadius, hitColliders, ~3);
        return numColliders > 0;
    }
    
    
    
    // Input Events
    private void _director_OnPlayerMovedFinished()
    {
        x = 0f;
        y = 0f;
    }

    private void _director_OnPlayerMoved(Vector2 movementValue)
    {
        x = movementValue.x;
        y = movementValue.y;
    }

    private void _director_OnPlayerJumpStarted()
    {
        holdingJump = true;
    }
    private void _director_OnPlayerJumpStopped()
    {
        holdingJump = false;
    }

    private void _director_OnPlayerCrouchStarted()
    {
        holdingCrouch = true;
        playerTransform.localScale = crouchScale;
        playerTransform.position = new Vector3(playerTransform.position.x, playerTransform.position.y - 0.5f, playerTransform.position.z);
        if (rb.linearVelocity.magnitude > 0.5f)
        {
            if (grounded)
            {
                rb.AddForce(orientation.transform.forward * slideForce);
            }
        }
    }

    private void _director_OnPlayerCrouchStopped()
    {
        holdingCrouch = false;
        playerTransform.localScale = playerScale;
        playerTransform.position = new Vector3(playerTransform.position.x, playerTransform.position.y + 0.5f, playerTransform.position.z);
    }
}
