using UnityEngine;

public class BikeController : MonoBehaviour
{
    [Header("Movement")]
    private float _moveInput, _steerInput;
    public float MaxSpeed, Acceleration, SteerStrength;

    [Header("Rotations")]
    public AnimationCurve TurningCurve;
    
    public float BikeXRotationInterpolationSpeed = 0.09f;
    public float BikeZTiltAngle = 45f;
    private float _currentVelocityOffset;
    private Vector3 _velocity;

    [SerializeField] private GameObject handleObject;
    public float RotationValueHandle = 30f;
    public float RotationSpeedHandle = 0.15f;
    
    [Header("Brake")]
    [Range(1, 10)]
    public float BrakingFactor;
    private bool _isBraking = false;

    [Header("Grounded")]
    public Rigidbody SphereRb, BikeBody;

    public bool IsGroundedObserver => IsGrounded();

    public float GravityValue;
    
    private float _groundCheckDistance;
    private RaycastHit _groundHit;
    public LayerMask GroundLayer;
    
    private void Start()
    {
        // subscribe to input events
        SphereRb.transform.parent = null;
        BikeBody.transform.parent = null;
        
        // ignore collision
        BoxCollider bikeCollider = BikeBody.GetComponent<BoxCollider>();
        SphereCollider sphereCollider = SphereRb.GetComponent<SphereCollider>();
        
        Physics.IgnoreCollision(bikeCollider, sphereCollider);
        
        // ground check
        _groundCheckDistance = sphereCollider.radius + 0.2f;
    }

    private void Update()
    {
        _moveInput = Input.GetAxis("Vertical");
        _steerInput = Input.GetAxis("Horizontal");
        _isBraking = Input.GetKey(KeyCode.Space);
        
        transform.position = SphereRb.transform.position;
        _velocity = BikeBody.transform.InverseTransformDirection(BikeBody.linearVelocity);
        _currentVelocityOffset = _velocity.z / MaxSpeed;
    }

    private void FixedUpdate()
    {
        Movement();
    }

    private void Movement()
    {
        if (IsGrounded())
        {
            if (!_isBraking)
            {
                AccelerateBike();
                RotateBike();
            }
            BrakeBike();
        }
        else
            ApplyGravity();
        TiltForwardBike();
    }

    private void AccelerateBike()
    {
        Vector3 movementValue = MaxSpeed * _moveInput * transform.forward;
        SphereRb.linearVelocity = Vector3.Lerp(SphereRb.linearVelocity, movementValue, Time.fixedDeltaTime * Acceleration);
    }

    private void RotateBike()
    {
        float turningCurve = TurningCurve.Evaluate(Mathf.Abs(_currentVelocityOffset));
        float rotationValue = _steerInput * _moveInput * turningCurve * SteerStrength * Time.fixedDeltaTime;
        transform.Rotate(0, rotationValue, 0, Space.World);
        
        // visual handle rotation
        Quaternion handleLocalRotation = handleObject.transform.localRotation;
        handleObject.transform.localRotation = Quaternion.Slerp(
            handleLocalRotation,
            Quaternion.Euler(handleLocalRotation.x, RotationValueHandle * _steerInput, handleLocalRotation.z),
            RotationSpeedHandle);
    }

    private void TiltForwardBike()
    {
        float xRotation =
            (Quaternion.FromToRotation(BikeBody.transform.up, _groundHit.normal) * BikeBody.transform.rotation)
            .eulerAngles.x;
        
        float zRotation = 0f;
        
        // how much I want to rotate * where I am rotating * am I rotating?
        if (_currentVelocityOffset > 0f)
            zRotation = -BikeZTiltAngle * _steerInput * _currentVelocityOffset;

        Quaternion targetRotation = Quaternion.Slerp(BikeBody.transform.rotation,
            Quaternion.Euler(xRotation, transform.eulerAngles.y, zRotation),
            BikeXRotationInterpolationSpeed);
        
        Quaternion newRotation = Quaternion.Euler(targetRotation.eulerAngles.x, transform.eulerAngles.y, targetRotation.eulerAngles.z);
        
        BikeBody.MoveRotation(newRotation);
    }

    private void BrakeBike()
    {
        if (_isBraking)
            SphereRb.linearVelocity *= BrakingFactor / 10;
    }

    private void ApplyGravity()
    {
        SphereRb.AddForce(GravityValue * Vector3.down, ForceMode.Acceleration);
    }
    
    private bool IsGrounded() => Physics.Raycast(SphereRb.position, Vector3.down, out _groundHit, GroundLayer);
}
