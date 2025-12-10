using Unity.Cinemachine;
using UnityEngine;

public class FP_CameraState : CameraState
{
    [Header("Assignables")]

    [SerializeField] private float _sensitivityX = 150f;
    [SerializeField] private float _sensitivityY = 150f;

    [SerializeField] private Transform _playerOrientation;
    [SerializeField] private CinemachineVirtualCameraBase _cameraOrientation;

    private readonly bool _allowArrows = true;

    [Header("Input")] private float _mouseX;
    private float _mouseY;

    private readonly float _multiplier = 0.01f;

    private float _xRotation;
    private float _yRotation;

    public float Tilt { get; private set; }

    // -------------------------------
    // State Machine
    // -------------------------------
    public override void LoadState(CameraManager manager, InputDirector director)
    {
        InputDirector = director;
        Manager = manager;

        // subscribe to input events
        director.OnCameraMoved += _inputDirector_OnCameraMoved;
    }

    public override void EnterState()
    {
        _playerOrientation = Manager.transform;
        _cameraOrientation = Manager.CurrentCinemachineComponent.GetComponent<CinemachineCamera>();

        Transform characterOrientation = Manager.GetComponentInChildren<CharacterOrientation>().transform;
        _cameraOrientation.gameObject.AddComponent<CinemachineHardLockToTarget>();
        _cameraOrientation.Follow = Manager.GetComponentInChildren<CameraOrientation>().transform;
        
        characterOrientation.rotation = _playerOrientation.rotation;
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private bool _updatedThisFrameThroughArrows = false;
    public override void UpdateState()
    {
        _updatedThisFrameThroughArrows = false;
        if (_allowArrows)
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

            // normalize for consistant speed
            if (camValue.magnitude > 1)
                camValue.Normalize();

            _mouseX = camValue.x / 2f;
            _mouseY = camValue.y / 2f;
        }

        CameraUpdate();
    }
    

    public override void FixedUpdate()
    {
        
    }


    public override void ClearState()
    {
        var previousComponent = _cameraOrientation.GetComponent<CinemachineHardLockToTarget>();
        if (previousComponent)
            Object.Destroy(previousComponent);
    }

    public override void OnDestroy()
    {
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

        _yRotation += _mouseX * _sensitivityX * _multiplier;
        _xRotation -= _mouseY * _sensitivityY * _multiplier;

        _xRotation = Mathf.Clamp(_xRotation, -90f, 90f);

        _cameraOrientation.transform.rotation = Quaternion.Euler(_xRotation, _yRotation, Tilt);
        _playerOrientation.transform.rotation = Quaternion.Euler(0, _yRotation, 0);
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
