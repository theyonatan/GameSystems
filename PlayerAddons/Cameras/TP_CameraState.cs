using UnityEngine;
using Unity.Cinemachine;

public class TP_CameraState : CameraState
{
    [Header("Sensitivity")]
    private float _sensitivityX = 150f;
    private float _sensitivityY = 150f;

    [Header("Vertical Clamp")]
    [SerializeField] private float _minYClamp = 48.3f;
    [SerializeField] private float _maxYClamp = 67.5f;
    
    [Header("Camera Configuration Constants")]
    private float _baseArmLength = 0.05f;
    private float _baseCameraDistance = 3.0f;
    private float _influence = 0.8f;

    [Header("Assignables")]
    private Transform _cameraOrientation;     // camera goes around
    private Transform _lookatTarget;
    private CinemachineCamera _virtualCam;
    private CinemachineThirdPersonFollow _thirdPersonFollow;

    private float _mouseX;
    private float _mouseY;

    private readonly float _multiplier = 0.01f;

    private float _xRotation;
    private float _yRotation;

    private readonly bool _allowArrows = true;
    private bool _updatedThisFrameThroughArrows = false;

    // -------------------------------
    // State Machine
    // -------------------------------
    public override void LoadState(CameraManager manager, InputDirector director)
    {
        Manager = manager;
        InputDirector = director;
        
        _cameraOrientation = Manager.GetComponentInChildren<CameraOrientation>().transform;
        _virtualCam = Manager.CurrentCinemachineComponent.GetComponent<CinemachineCamera>();
        _lookatTarget = GameObject.FindGameObjectWithTag("lookat").transform;

        director.OnCameraMoved += _inputDirector_OnCameraMoved;
    }

    public override void EnterState()
    {
        // configure virtual camera
        _thirdPersonFollow = _virtualCam.gameObject.AddComponent<CinemachineThirdPersonFollow>();
        _virtualCam.Follow = _cameraOrientation;
        
        // configure camera third person component
        _thirdPersonFollow.CameraDistance = _baseCameraDistance;
        _thirdPersonFollow.VerticalArmLength = _baseArmLength;
        _thirdPersonFollow.Damping = new Vector3(0f, 0f, 0.2f);
        _thirdPersonFollow.ShoulderOffset = new Vector3(-0.3f, -0.18f, 0f);
        _thirdPersonFollow.AvoidObstacles = new CinemachineThirdPersonFollow.ObstacleSettings()
        {
            Enabled = true,
            CollisionFilter = 1 << LayerMask.NameToLayer("Ground"),
            IgnoreTag = "",
            CameraRadius = 0.001f,
            DampingFromCollision = 0f,
            DampingIntoCollision = 0.5f
        };
        
        // configure cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public override void UpdateState()
    {
        _influence = 0.8f;
        
        _updatedThisFrameThroughArrows = false;

        // Optional debug with arrow keys
        if (_allowArrows)
            CollectArrowsInputDebug();

        ApplyGoldenRatioCameraDistance();
        CameraUpdate();
    }

    public override void FixedUpdate()
    {
        // nothing for now
    }

    public override void ClearState()
    {
        if (!_virtualCam)
            return;
        
        var previousComponent = _virtualCam.GetComponent<CinemachineThirdPersonFollow>();
        if (previousComponent)
            Object.Destroy(previousComponent);
    }

    public override void OnDestroy()
    {
        if (InputDirector != null)
            InputDirector.OnCameraMoved -= _inputDirector_OnCameraMoved;
    }

    // -------------------------------
    // State Functions
    // -------------------------------
    private void CameraUpdate()
    {
        if (!_updatedThisFrameThroughArrows)
        {
            _mouseX = Input.GetAxisRaw("Mouse X");
            _mouseY = Input.GetAxisRaw("Mouse Y");
        }

        if (!CanLookAround)
        {
            _mouseX = 0;
            _mouseY = 0;
        }

        // Horizontal (yaw)
        _yRotation += _mouseX * _sensitivityX * _multiplier;
        
        // Vertical (pitch) – clamped to max and min camera height
        _xRotation -= _mouseY * _sensitivityY * _multiplier;
        _xRotation = Mathf.Clamp(_xRotation, -_minYClamp, _maxYClamp);

        // Rotation of the camera orientation
        _cameraOrientation.rotation = Quaternion.Euler(_xRotation, _yRotation, 0f);
    }

    private void ApplyGoldenRatioCameraDistance()
    {
        // get camera pitch in degrees (-90 looking down, +90 looking up)
        float pitch = _virtualCam.State.RawOrientation.eulerAngles.x;
        if (pitch > 180f) pitch -= 360f;  // convert 0-360 → -180 to 180

        // normalize pitch to -1..1
        float pitchNorm = - Mathf.Clamp(pitch / 70f, -1f, 1f);

        // golden ratio curve
        float golden = 1f + pitchNorm * 0.618f * _influence;

        // apply to arm length
        _thirdPersonFollow.VerticalArmLength = _baseArmLength * golden;
        
        // optionally: apply subtle change to distance
        _thirdPersonFollow.CameraDistance = _baseCameraDistance * (1f + pitchNorm * 0.2f * _influence);
    }

    private void CollectArrowsInputDebug()
    {
        Vector2 camValue = Vector2.zero;

        if (Input.GetKey(KeyCode.UpArrow)
            || Input.GetKey(KeyCode.DownArrow)
            || Input.GetKey(KeyCode.RightArrow)
            || Input.GetKey(KeyCode.LeftArrow))
            _updatedThisFrameThroughArrows = true;

        if (Input.GetKey(KeyCode.UpArrow))
            camValue.y = 1;
        if (Input.GetKey(KeyCode.DownArrow))
            camValue.y = -1;
        if (Input.GetKey(KeyCode.RightArrow))
            camValue.x = 1;
        if (Input.GetKey(KeyCode.LeftArrow))
            camValue.x = -1;

        if (camValue.magnitude > 1)
            camValue.Normalize();

        _mouseX = camValue.x / 2f;
        _mouseY = camValue.y / 2f;
    }

    // -------------------------------
    // Input Events
    // -------------------------------
    private void _inputDirector_OnCameraMoved(Vector2 camValue)
    {
        _mouseX = camValue.x;
        _mouseY = camValue.y;
    }
}
