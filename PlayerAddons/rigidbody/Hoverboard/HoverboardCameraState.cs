using UnityEngine;

public class HoverboardCameraState : CameraState
{
    [Header("Assignables")]
    [SerializeField] HoverboardState hoverboardState;

    [SerializeField] private float sensX = 150f;
    [SerializeField] private float sensY = 150f;

    [SerializeField] Camera cam = null;
    [SerializeField] Transform orientation = null;
    [SerializeField] Transform playerTransform;
    [SerializeField] GameObject _virtualCam;

    [Header("Input")]
    float mouseX;
    float mouseY;

    float multiplier = 0.01f;

    float xRotation;
    float yRotation;


    // -------------------------------
    // State Machine
    // -------------------------------
    public override void LoadState(CameraManager manager, InputDirector director)
    {
        InputDirector = director;
        Manager = manager;

        playerTransform = manager.transform.parent.transform;

        // subscribe to input events
        InputDirector.OnCameraMoved += _inputDirector_OnCameraMoved;
    }

    public override void EnterState()
    {
        orientation = Manager.transform.parent.GetComponentInChildren<Animator>().gameObject.transform;
        _virtualCam = Manager.CurrentCinemachineComponent.gameObject;
        cam = Camera.main;

        MovementManager movementManager = Manager.transform.parent.GetComponentInChildren<MovementManager>();
        hoverboardState = (HoverboardState)movementManager.CurrentState;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public override void UpdateState()
    {
        CameraUpdate();
    }
    

    public override void FixedUpdate()
    {
        
    }


    public override void ClearState()
    {
        
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
        mouseX = Input.GetAxisRaw("Mouse X");
        mouseY = Input.GetAxisRaw("Mouse Y");

        yRotation += mouseX * sensX * multiplier;
        xRotation -= mouseY * sensY * multiplier;

        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        _virtualCam.transform.rotation = Quaternion.Euler(xRotation, yRotation, 0f);
        orientation.transform.rotation = Quaternion.Euler(0, yRotation, 0);
    }

    // -------------------------------
    // Input Events
    // -------------------------------
    private void _inputDirector_OnCameraMoved(Vector2 camValue)
    {
        mouseX = camValue.x;
        mouseY = camValue.y;
    }
}
