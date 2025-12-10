using System;
using UnityEngine;
using Unity.Cinemachine;

public class CutsceneCamera : MonoBehaviour
{
    public string CutsceneCameraName;
    public CinemachineCamera VirtualCamera;
    public Action OnCameraReachedTheEnd;
    private CutsceneCameraType _cameraType;
    private bool _cameraFinishedAnimation;
    private bool _blendStarted;
    private bool _waitingForBlend;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        if (TryGetComponent(out CinemachineCamera virtualCamera))
        {
            VirtualCamera = virtualCamera;
            _cameraType = CutsceneCameraType.StaticCamera;
        }
        else if (GetComponentInChildren<CinemachineCamera>())
        {
            VirtualCamera = GetComponentInChildren<CinemachineCamera>();
            _cameraType = CutsceneCameraType.TrailCamera;
        }
        else
            Debug.LogError("Virtual camera not found on cutscene camera object");
    }

    private void Update()
    {
        if (_cameraFinishedAnimation)
            return;
        
        if (_cameraType == CutsceneCameraType.TrailCamera && VirtualCamera.TryGetComponent<CinemachineSplineDolly>(out var dollyCamera))
            if (dollyCamera.CameraPosition >= 1f)
            {
                _cameraFinishedAnimation = true;
                OnCameraReachedTheEnd?.Invoke();
                
                // release camera, unblock swap camera execution on the next cutscene camera
                CutscenesHelper.CurrentCutsceneCamera = null;
            }
    }

    public void Play(float cameraSpeed=0.2f)
    {
        switch (_cameraType)
        {
            case CutsceneCameraType.StaticCamera:
                break;
            case CutsceneCameraType.TrailCamera:
                PlayDollyCamera(cameraSpeed);
                break;
            default:
                Debug.LogError("if this case happens God is real and I'm a fool doomed to Grok");
                break;
        }
    }
    
    private void PlayDollyCamera(float cameraSpeed)
    {
        CinemachineSplineDolly cameraDolly = VirtualCamera.GetComponent<CinemachineSplineDolly>();
        if (!cameraDolly)
        {
            Debug.LogError("Dolly not found on camera of type TrailCamera!");
            return;
        }

        cameraDolly.CameraPosition = 0f;
        cameraDolly.AutomaticDolly = new SplineAutoDolly
        {
            Enabled = true,
            Method = new SplineAutoDolly.FixedSpeed { Speed = cameraSpeed }
        };
    }

    /// <summary>
    /// Gives priority to this cutscene camera.
    /// this means it will be the active camera, a blend will start from the previous camera to this one.
    /// </summary>
    public void SetAsActiveCamera()
    {
        var brain = CutscenesHelper.GetActive().GetComponent<CinemachineBrain>();
        bool isAlreadyActive = ReferenceEquals(brain.ActiveVirtualCamera, VirtualCamera);
        
        CutscenesHelper.GiveCameraPriority(VirtualCamera);
        _blendStarted = true;
        
        if (isAlreadyActive)
            _waitingForBlend = true;
    }
    
    public void SetFollowTarget(Transform target) => VirtualCamera.Follow = target;

    /// <summary>
    /// Function to check if blend finished
    /// 
    /// IMPORTANT NOTE:
    /// When the very first cutscene camera becomes active, there is no previous
    /// virtual camera to blend from, so Cinemachine will NOT create an ActiveBlend.
    /// Without a blend, our story logic would get stuck waiting for something
    /// that never starts.
    ///
    /// To solve this, we detect if this is the first time this camera is becoming
    /// active (meaning it was already the brain's ActiveVirtualCamera). In that case
    /// we mark the "blend" as already completed.
    ///
    /// Later transitions (camera A → camera B) WILL produce real blends, and
    /// _waitingForBlend will be handled normally.
    /// </summary>
    public bool IsBlendFinished()
    {
        var brain = CutscenesHelper.GetActive().GetComponent<CinemachineBrain>();
        if (!brain) return false;

        var blend = brain.ActiveBlend;

        // If blend never started, we’re not done yet
        if (_blendStarted && !_waitingForBlend && blend == null)
            return false;
        
        // mark that blending actually started
        if (blend != null)
            _waitingForBlend = true;
        
        if (_waitingForBlend)
            return blend == null;
        
        return false;
    }
    
    public bool IsFinishedPlaying() => _cameraFinishedAnimation;

    public CutsceneCameraType GetCameraType() => _cameraType;
}

public enum CutsceneCameraType
{
    StaticCamera,
    TrailCamera
}
