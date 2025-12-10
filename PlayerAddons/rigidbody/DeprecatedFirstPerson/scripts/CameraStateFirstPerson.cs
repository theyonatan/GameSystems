using UnityEngine;

public class CameraStateFirstPerson : CameraState
{
    // CamObject
    GameObject _virtualCam;
    Transform orientation; // Character is orientation.

    // values (input)
    private float mouseX = 0f;
    private float mouseY = 0f;

    // values (static)
    private float xRotation;
    private float sensitivityX = 5f;
    private float sensitivityY = 7f;
    private float sensMultiplier = 1f;
    private float desiredX;


    // Init functions
    public override void LoadState(CameraManager manager, InputDirector director)
    {
        InputDirector = director;
        Manager = manager;

        // subscribe to input events
        InputDirector.OnCameraMoved += _inputDirector_OnCameraMoved;
    }

    public override void EnterState()
    {
        orientation = Manager.transform.GetComponentInChildren<Animator>().gameObject.transform;
        _virtualCam = Manager.CurrentCinemachineComponent.gameObject;
    }


    // input events
    private void _inputDirector_OnCameraMoved(Vector2 camValue)
    {
        mouseX = camValue.x;
        mouseY = camValue.y;
    }

    public override void UpdateState()
    {
        CameraLogic();
    }

    public override void FixedUpdate()
    {
        
    }

    // Logic
    public void CameraLogic()
    {
        // Calculate value change
        mouseX = mouseX * sensitivityX * Time.fixedDeltaTime * sensMultiplier;
        mouseY = mouseY * sensitivityY * Time.fixedDeltaTime * sensMultiplier;

        // Find current look rotation
        Vector3 rot = _virtualCam.transform.localRotation.eulerAngles;
        desiredX = rot.y + mouseX;

        // Rotate, and also make sure we dont over- or under-rotate.
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // Perform the rotations
        _virtualCam.transform.localRotation = Quaternion.Euler(xRotation, desiredX, 0);
        orientation.localRotation = Quaternion.Euler(0, desiredX, 0);
    }


    // Destructions
    public override void ClearState()
    {
        
    }

    public override void OnDestroy()
    {
        InputDirector.OnCameraMoved -= _inputDirector_OnCameraMoved;
    }
}
