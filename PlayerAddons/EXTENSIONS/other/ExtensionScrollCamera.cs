using UnityEngine;
using Unity.Cinemachine;

public class ExtensionScrollCamera : MonoBehaviour
{
    [Header("Managers")] public MovementManager movementManager;
    public CameraManager cameraManager;

    [Header("Cameras")] private CinemachineCamera _activeCam;
    private CinemachineThirdPersonFollow _tpFollow;

    [Header("Zoom Settings")] public float minDistance = 0.5f; // closest in TP
    public float maxDistance = 6f; // farthest in TP
    public float scrollSpeed = 2f;
    public float firstPersonSwitchDistance = 0.6f; // when reaching this, switch to FP

    private bool _isFirstPerson = false;

    private void Awake()
    {
        if (movementManager == null)
            movementManager = GetComponent<MovementManager>();

        if (cameraManager == null)
            cameraManager = GetComponent<CameraManager>();

        if (_activeCam != null)
            _activeCam = movementManager.GetComponentInChildren<CinemachineCamera>();
    }

    private void Update()
    {
        //     float scroll = Input.mouseScrollDelta.y;
        //     if (Mathf.Abs(scroll) < 0.01f)
        //         return;
        //
        //     if (!_isFirstPerson)
        //     {
        //         float newDist = _tpFollow.CameraDistance - scroll * scrollSpeed;
        //         newDist = Mathf.Clamp(newDist, minDistance, maxDistance);
        //         _tpFollow.CameraDistance = newDist;
        //
        //         // fully zoomed in -> switch to FP
        //         if (newDist <= firstPersonSwitchDistance)
        //         {
        //             SwitchToFirstPerson();
        //         }
        //     }
        //     else
        //     {
        //         // In FP, any scroll OUT switches back to TP
        //         if (scroll < 0f)
        //         {
        //             SwitchToThirdPerson();
        //         }
        //     }
        // }

        // private void SwitchToFirstPerson()
        // {
        //     if (_isFirstPerson) return;
        //     _isFirstPerson = true;
        //
        //     if (firstPersonCam != null)
        //         firstPersonCam.Priority = 20;
        //     if (thirdPersonCam != null)
        //         thirdPersonCam.Priority = 10;
        //
        //     // switch movement + camera state
        //     if (movementManager != null)
        //         movementManager.ChangeState(new FirstPersonCCState());
        //
        //     if (cameraManager != null)
        //         cameraManager.ChangeState(new FirstPersonCameraState());
        // }
        //
        // private void SwitchToThirdPerson()
        // {
        //     if (!_isFirstPerson) return;
        //     _isFirstPerson = false;
        //
        //     if (thirdPersonCam != null)
        //         thirdPersonCam.Priority = 20;
        //     if (firstPersonCam != null)
        //         firstPersonCam.Priority = 10;
        //
        //     if (_tpFollow == null && thirdPersonCam != null)
        //         _tpFollow = thirdPersonCam.GetCinemachineComponent<CinemachineThirdPersonFollow>();
        //
        //     if (_tpFollow != null)
        //         _tpFollow.CameraDistance = Mathf.Clamp(3f, minDistance, maxDistance);
        //
        //     if (movementManager != null)
        //         movementManager.ChangeState(new ThirdPersonCCState());
        //
        //     if (cameraManager != null)
        //         cameraManager.ChangeState(new ThirdPersonCameraState());
        // }
    }
}
