using UnityEngine;

public class FPStoryExample : MonoBehaviour
{
    [SerializeField] private GameObject CinemachineCamera;
    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        MovementManager movementManager = player.GetComponent<MovementManager>();
        CameraManager cameraManager = player.GetComponent<CameraManager>();

        movementManager.ChangeState(new FpState());
        cameraManager.ChargeState(new FP_CameraState(), CinemachineCamera);
    }
}
