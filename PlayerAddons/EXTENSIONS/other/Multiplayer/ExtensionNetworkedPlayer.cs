#if NETCODE

using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
// ReSharper disable All

public class ExtensionNetworkedPlayer : NetworkBehaviour
{
    [SerializeField] private InputDirector inputDirector;
    [SerializeField] private MovementManager movementManager; // maybe remove this (I disable input)
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private Rigidbody rb;

    public UnityEvent<GameObject> OnNetworkSpawned;
    
    public override void OnNetworkSpawn()
    {
        inputDirector = GetComponentInChildren<InputDirector>();
        movementManager = GetComponentInChildren<MovementManager>();
        cameraManager = GetComponentInChildren<CameraManager>();

        // default to "you don't own this"
        movementManager.IsOwner = false;

        if (IsOwner)
        {
            ConfigPlayer_IfOwner();
            
            ConfigPlayerExtensions();
        }
        else
        {
            ConfigPlayer_NotOwner();
        }
    }

    private void FixedUpdate()
    {
        if (IsOwner)
        {
            // Local player will control this.
            // (through the movement manager and input director stuff)
        }
        else
        {
            // maybe if I will need smoothing later.
        }
    }

    /// <summary>
    /// if I am the owner of the object (this is the local player's object)
    /// </summary>
    private void ConfigPlayer_NotOwner()
    {
        // set "isOwner" in relavant scripts
        inputDirector.IsOwner = false;
        movementManager.IsOwner = false;

        // disable relavant objects:
        // input
        inputDirector.gameObject.SetActive(false);

        // camera
        GameObject localCamObject = GetComponentInChildren<CinemachineCamera>().gameObject;
        localCamObject.SetActive(false);

        // visibillity Layer
        GameObject localSelfObject = GetComponentInChildren<SelfForDisable>().gameObject;
        int defaultLayer = 0;
        localSelfObject.layer = defaultLayer;
    }

    /// <summary>
    /// if I am not the owner of the object (this is running on a different player)
    /// </summary>
    private void ConfigPlayer_IfOwner()
    {
        // set "isOwner" in relavant scripts
        movementManager.IsOwner = true;
        inputDirector.IsOwner = true;

        // initiate the player movement components
        InputDirector.Instance = inputDirector;
        movementManager.StartingState = new FpState();
        cameraManager.StartingState = new FP_CameraState();
    }

    /// <summary>
    /// if I am the owner:
    /// add relavant extensions.
    /// </summary>
    private void ConfigPlayerExtensions()
    {
        GameObject playerObject = inputDirector.gameObject;

        OnNetworkSpawned?.Invoke(playerObject);
    }
}
#endif
