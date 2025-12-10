using UnityEngine;
using Unity.Cinemachine;

public class ThirdPersonCameraState : CameraState
{
    // Const
    private CinemachineFreeLook _freelookCamera;
    private GameObject _player;
    private Transform _lookat;

    public override void ClearState()
    {

    }

    public override void FixedUpdate()
    {

    }

    public override void LoadState(CameraManager manager, InputDirector director)
    {
        InputDirector = director;
        _freelookCamera = manager.transform.parent.GetComponentInChildren<CinemachineFreeLook>();

        PlayerStateData localData = Resources.Load<PlayerStateData>("playerStates/normalPlayer");
        CameraSpeed = localData.CameraSpeed;
        
        _player = manager.transform.parent.gameObject;
        _lookat = _player.GetComponentInChildren<LookatSign>().transform;
        
        _freelookCamera.Follow = _player.transform;
        _freelookCamera.LookAt = _lookat;
    }

    public override void EnterState()
    {
        //freelookCamera.m_XAxis.m_MaxSpeed = cameraSpeed.x;
        //freelookCamera.m_YAxis.m_MaxSpeed = cameraSpeed.y;
    }

    public override void UpdateState()
    {
        
    }

    public override void OnDestroy()
    {
        
    }
}
