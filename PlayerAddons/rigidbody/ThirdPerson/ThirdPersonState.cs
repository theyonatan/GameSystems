using System;
using UnityEngine;

[System.Serializable]
public class ThirdPersonState : MovementState
{
    // rigidbody based
    private Rigidbody _rb;
    private Transform _camTransform;
    private Transform _playerTransform;

    // using a rotator
    private IPlayerRotator _playerRotator;

    // movement:
    private Vector2 _inputValue;
    Vector3 _moveDirection;

    public float MovementSpeed = 0f;

    // values (static)
    public float WalkingSpeed = 0f;
    public float RunningSpeed = 0f;
    public float JumpHeight = 0f;



    public override MovementComponentType ComponentType => MovementComponentType.Rigidbody;

    public override void LoadState(MovementManager manager, InputDirector director)
    {
        Controller = manager;

        _camTransform = Camera.main.transform;
        _playerTransform = GameObject.FindGameObjectWithTag("Player").transform;

        // load movement data
        LocalData = Resources.Load<PlayerStateData>("playerStates/normalPlayer");
        WalkingSpeed = LocalData.WalkingSpeed;
        RunningSpeed = LocalData.RunningSpeed;
        JumpHeight = LocalData.JumpHeight;

        _playerRotator = new PlayerRotator(_camTransform, LocalData.TimeToReachTargetRotation);
        MovementSpeed = WalkingSpeed;
        
        // Input Events
        Director = InputDirector.Instance;
        
        Director.OnPlayerMoved += _director_OnPlayerMoved;
        Director.OnPlayerMovedFinished += _director_OnPlayerMovedFinished;
        Director.OnPlayerJumpStarted += Jump;
        Director.OnPlayerRunStarted += RunStart;
        Director.OnPlayerRunStopped += RunStop;
        
        Director.OnEnablePlayerMovement += _director_OnEnablePlayerMovement;
        Director.OnDisablePlayerMovement += _director_OnDisablePlayerMovement;
        Director.OnPlayerRunEnabled += _director_OnPlayerRunEnabled;
        Director.OnPlayerRunDisabled += _director_OnPlayerRunDisabled;
    }

    public override void EnterState()
    {
        // start enter state animation HERE
        
        _rb = Controller.gameObject.GetComponent<Rigidbody>();
        
        Debug.Log("Entering Third Person state!");
    }

    public override void FixedUpdate()
    {
        // Calculate movement
        _moveDirection = new Vector3(_camTransform.forward.x, 0f, _camTransform.forward.z) * _inputValue.y;
        _moveDirection += _camTransform.right * _inputValue.x;
        _moveDirection.Normalize();
        _moveDirection *= MovementSpeed;
        _moveDirection.y = _rb.linearVelocity.y;

        // Apply animations
        AnimationManager.Instance.SetAnimatorValue("MoveY", _inputValue.y != 0 || _inputValue.x != 0 ? 1: 0);

        // Move the player.
        Vector3 movementVelocity = _moveDirection;
        _rb.linearVelocity = movementVelocity;
    }

    public override void UpdateState()
    {
        _playerRotator.RotatePlayer(_inputValue);
        
        // Debug.Log("is grounded? " + isGrounded() + (isGrounded() ? playerTransform.position.y : ""));
        //Debug.DrawLine(playerTransform.position, playerTransform.position - new Vector3(0f, 0.2f, 0f));
    }

    private void MovePlayer(Vector3 dir)
    {
        Vector3 targetPositon = _rb.position + dir * MovementSpeed * Time.fixedDeltaTime;
        _rb.MovePosition(targetPositon);
    }

    bool IsGrounded()
    {
        float sphereRadius = 0.1f; // Adjust the radius as needed
        Vector3 rayStart = _playerTransform.position - new Vector3(0f, 0.3f, 0f);

        Collider[] hitColliders = new Collider[1];
        int numColliders = Physics.OverlapSphereNonAlloc(rayStart, sphereRadius, hitColliders, ~3);
        return numColliders > 0;
    }





    public void Jump()
    {
        if (!IsGrounded())
            return;
        Debug.Log("Aight lets jump!");
        _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, JumpHeight, _rb.linearVelocity.z);
    }

    public void RunStart()
    {
        MovementSpeed = RunningSpeed;
    }
    public void RunStop()
    {
        MovementSpeed = WalkingSpeed;
    }
    public override void CleanState()
    {
        AnimationManager.Instance.SetAnimatorValue("MoveX", 0f);
        AnimationManager.Instance.SetAnimatorValue("MoveY", 0f);

        Vector3 movementVelocity = new(0f, _rb.linearVelocity.y, 0f);
        _moveDirection.x = 0;
        _moveDirection.z = 0;
        _rb.linearVelocity = movementVelocity;
        
        // unsubscribe from Input Events
        Director = InputDirector.Instance;
        
        Director.OnPlayerMoved -= _director_OnPlayerMoved;
        Director.OnPlayerMovedFinished -= _director_OnPlayerMovedFinished;
        Director.OnPlayerJumpStarted -= Jump;
        Director.OnPlayerRunStarted -= RunStart;
        Director.OnPlayerRunStopped -= RunStop;
        
        Director.OnEnablePlayerMovement -= _director_OnEnablePlayerMovement;
        Director.OnDisablePlayerMovement -= _director_OnDisablePlayerMovement;
        Director.OnPlayerRunEnabled -= _director_OnPlayerRunEnabled;
        Director.OnPlayerRunDisabled -= _director_OnPlayerRunDisabled;
    }
    
    // -------------------------------
    // Input Events
    // -------------------------------
    private void _director_OnPlayerMoved(Vector2 movementValue)
    {
        _inputValue.x = movementValue.x;
        _inputValue.y = movementValue.y;
    }

    private void _director_OnPlayerMovedFinished()
    {
        _inputValue.x = 0f;
        _inputValue.y = 0f;
    }

    private void _director_OnEnablePlayerMovement()
    {
        CanMove = true;
    }

    private void _director_OnDisablePlayerMovement()
    {
        CanMove = false;
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
