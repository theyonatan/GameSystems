using Unity.Cinemachine;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    /// <summary>
    /// manages the different camera states.
    /// 
    /// CameraState will have many inheriting states, each for a different camera mode. <see cref="CameraState"/>
    /// 
    /// In order to change the camera state, a story or extension would call ChargeState.
    /// 
    /// awesome! :)
    /// </summary>

    // States
    [SerializeReference]
    private CameraState _currentState;
    public CameraState StartingState = null;

    // GameObjects:
    public CinemachineCamera CurrentCinemachineComponent;

    // References
    private InputDirector _inputDirector;
    

    // values
    public Vector2 CameraSpeed
    {
        get => _currentState.CameraSpeed;
        set => _currentState.CameraSpeed = value;
    }

    private void Awake()
    {
        // States:
        _currentState = new CameraStateInPlace();
    }

    private void Start()
    {
        // subscribe to all input events:
        _inputDirector = InputDirector.Instance;
        _inputDirector.OnDisablePlayerMovement += DisableCamera;
        _inputDirector.OnEnablePlayerMovement += EnableCamera;

        // player object:
        var cameraObject = GameObject.FindGameObjectWithTag("DefaultCam");
        if (cameraObject != null)
            CurrentCinemachineComponent = cameraObject.GetComponent<CinemachineCamera>();

        // Starting State:
        if (StartingState != null)
            _currentState = StartingState;
        
        _currentState.LoadState(this, _inputDirector);
        _currentState.EnterState();
    }

    /// <summary>
    /// charge the camera with a new state (and load it).
    /// if we use cinemachine on that state, we will instantiate a new object with the cinemachine component.
    /// </summary>
    /// <param name="newState"></param>
    /// <param name="cinemachineCameraToSpawn"></param>
    public void ChargeState(CameraState newState, GameObject cinemachineCameraToSpawn = null)
    {
        // verify we need to switch
        if (newState.GetType() == _currentState.GetType())
            return;

        // clean state
        _currentState?.ClearState();

        // switch cinemachine virtual camera
        cinemachineCameraToSpawn ??= GetResources_DefaultVirtualCamera();
        
        // destroy previous state cinemachine object
        if (CurrentCinemachineComponent &&
            cinemachineCameraToSpawn != CurrentCinemachineComponent.gameObject)
        {
            Destroy(CurrentCinemachineComponent);
        }
        
        // apply new cinemachine object
        if (cinemachineCameraToSpawn)
            spawnNewVirtualCamera(cinemachineCameraToSpawn);
        
        else // no cinemachine object is used for this state
            CurrentCinemachineComponent = null;
        
        // enter new state
        _currentState = newState;
        _currentState.LoadState(this, InputDirector.Instance);
        _currentState.EnterState();
    }

    GameObject GetResources_DefaultVirtualCamera()
    {
        var virtualCamera = Resources.Load<GameObject>("cam");

        if (!virtualCamera)
            Debug.LogWarning("Cannot find virtual camera in resources folder!");
        
        return virtualCamera;
    }

    void spawnNewVirtualCamera(GameObject cinemachineComponent)
    {
        var spawnedCameraObject = Instantiate(cinemachineComponent, transform.parent);
        CurrentCinemachineComponent = spawnedCameraObject.GetComponent<CinemachineCamera>();
        
        // give new camera priority
        if (CurrentCinemachineComponent)
            CutscenesHelper.GiveCameraPriority(CurrentCinemachineComponent);
    }

    void Update()
    {
        _currentState.UpdateState();
    }

    private void OnDestroy()
    {
        if (_currentState != null)
            _currentState.ClearState();

        // clear events
        _inputDirector.OnDisablePlayerMovement -= DisableCamera;
        _inputDirector.OnEnablePlayerMovement -= EnableCamera;
    }

    public void EnableCamera()
    {
        _currentState.CanLookAround = true;
    }

    public void DisableCamera()
    {
        _currentState.CanLookAround = false;
    }
}
