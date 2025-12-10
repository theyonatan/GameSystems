using System;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

public interface StoryCommand
{
    public bool Execute();
}

// -- Story Commands --
public class DebugSay : StoryCommand
{
    private readonly string _text;
    public DebugSay(string text)
    {
        _text = text;
    }

    public bool Execute()
    {
        Debug.Log(_text);
        return true;
    }
}

public class EmptyCommand : StoryCommand
{
    public bool Execute()
    {
        Debug.Log("hello there!");
        return true;
    }
}

public class Say : StoryCommand
{
    private readonly float _letterDelay;
    private readonly string _text;
    private readonly Transform? _characterTransform;
    private bool _initiated;
    
    public Say(string text, Transform? characterTransform=null, float letterDelay=0.04f)
    {
        _text = text;
        _letterDelay = letterDelay;
        _characterTransform = characterTransform;
    }

    public bool Execute()
    {
        if (!_initiated)
        {
            SpeechManager.Instance.LoadDialogue(_text, _characterTransform, _letterDelay);
            _initiated = true;
        }
        
        return SpeechManager.Instance.Finished;
    }
}

public class GoTo : StoryCommand
{
    private readonly Vector3 _targetPosition;
    private readonly Transform _character;
    private readonly float _speed;

    private bool _useNavmesh;
    private NavMeshAgent _agent;

    public GoTo(Transform character, Vector3 position, float speed, NavMeshAgent agent=null)
    {
        _character = character;
        _targetPosition = position;
        _speed = speed;
        _agent = agent;
        _useNavmesh = agent;
    }

    public bool Execute()
    {
        if (_useNavmesh)
        {
            _agent.speed = _speed;
            _agent.SetDestination(_targetPosition);
        
            if (!_agent.pathPending && _agent.remainingDistance < 0.4f && !_agent.hasPath)
                return true;
        }
        else
        {
            _character.position = Vector3.MoveTowards(_character.position, _targetPosition, Time.deltaTime * _speed);
            if (Vector3.Distance(_character.position, _targetPosition) < 0.4f)
                return true;
        }

        return false;
    }
}

public class DelayedStoryAction : StoryCommand
{
    private readonly Action _delayedAction;

    public DelayedStoryAction(Action delayedAction)
    {
        _delayedAction = delayedAction;
    }

    public bool Execute()
    {
        _delayedAction();
        
        return true;
    }
}

public class LookAt : StoryCommand
{
    private Transform _targetLook;
    private readonly Transform _character;
    private readonly float _lookSpeed;

    public LookAt(Transform character, Transform targetLook, float speed=4f)
    {
        _character = character;
        _targetLook = targetLook;
        _lookSpeed = speed;
    }

    public bool Execute()
    {
        Vector3 targetDirection = _targetLook.position - _character.position;
        targetDirection.y = 0f;
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
        
        // smoothly rotate:
        Quaternion newRotation = Quaternion.Slerp(
            _character.rotation,
            targetRotation,
            _lookSpeed * Time.deltaTime);

        // constrain rotation only around Y
        newRotation = Quaternion.Euler(0, newRotation.eulerAngles.y, 0);
        _character.rotation = newRotation;
        
        // check if reached close enough to target rotation
        float angle = Quaternion.Angle(_character.rotation, targetRotation);
        return angle < 1f;
    }
}

public class DisableInput : StoryCommand
{
    public bool Execute()
    {
        InputDirector.Instance.DisableInput();
        return true;
    }
}

public class EnableInput : StoryCommand
{
    public bool Execute()
    {
        InputDirector.Instance.EnableInput();
        return true;
    }
}

public class DisableJumpInput : StoryCommand
{
    public bool Execute()
    {
        InputDirector.Instance.DisableJumpInput();
        return true;
    }
}

public class EnableJumpInput : StoryCommand
{
    public bool Execute()
    {
        InputDirector.Instance.EnableJumpInput();
        return true;
    }
}

public class WaitUntilPlayerNearGameobject : StoryCommand
{
    private readonly Vector3 _targetObject;
    private readonly Transform _player;
    private readonly float _speed;

    public WaitUntilPlayerNearGameobject(Transform player, Vector3 position)
    {
        _player = player;
        _targetObject = position;
    }

    public bool Execute()
    {
        if (Vector3.Distance(_player.position, _targetObject) < 0.4f)
            return true;
        return false;
    }
}

public class SwapPlayerState<TMovementState, TCameraState> : StoryCommand
    where TMovementState : MovementState, new()
    where TCameraState : CameraState, new()
{
    public bool Execute()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (!player)
            return true; // nothing to do, skip
        
        var movementManager = player.GetComponentInChildren<MovementManager>();
        var cameraManager = player.GetComponentInChildren<CameraManager>();

        if (!movementManager || !cameraManager)
            return true; // also skip instead of breaking story

        movementManager.ChangeState(new TMovementState());
        cameraManager.ChargeState(new TCameraState());

        return true;
    }
}

public class SwapCamera : StoryCommand
{
    private readonly CutsceneCamera _cutsceneCamera;
    private readonly float _cameraSpeed;
    private readonly bool _continueStoryOverCamera;
    private readonly Transform _followTarget;

    private bool _gavePriorityOnce;
    private bool _playedCameraOnce;
    private bool _finishedCameraAnimation;

    public SwapCamera(CutsceneCamera cutsceneCamera,
        float cameraSpeed,
        bool continueStoryOverCamera,
        Transform followTarget = null)
    {
        _cutsceneCamera = cutsceneCamera;
        _cameraSpeed = cameraSpeed;
        _continueStoryOverCamera = continueStoryOverCamera;
        _followTarget = followTarget;
        
        _gavePriorityOnce = false;
        _playedCameraOnce = false;
    }
    
    public bool Execute()
    {
        // Block Execution if another camera is running
        if (BlockUntilCutsceneCameraFree())
            return false;
        
        // Start Transition (Blend) to new virtual camera
        if (!_gavePriorityOnce)
            GivePriorityOnce();
                
        // wait for blend to finish
        if (!_cutsceneCamera.IsBlendFinished())
            return false;
        
        // ok, finished, play camera
        if (!_playedCameraOnce)
            PlayCameraOnce();
        
        // can the story continue while the camera is still playing?
        if (_continueStoryOverCamera)
            return true;

        // go to the next story action once the camera finished its animation.
        return _finishedCameraAnimation;
    }
    
    /// <summary>
    /// sometimes we let the camera run while the story keeps playing.
    /// if the story gets to a point where it asks for another camera to play (this one),
    /// we will pause execution until the other camera finishes, only than start.
    /// </summary>
    public bool BlockUntilCutsceneCameraFree()
    {
        // If another camera is playing, don't activate yet
        if (CutscenesHelper.CurrentCutsceneCamera 
            && CutscenesHelper.CurrentCutsceneCamera != _cutsceneCamera 
            && !CutscenesHelper.CurrentCutsceneCamera.IsFinishedPlaying())
        {
            return true;
        }
        
        CutscenesHelper.CurrentCutsceneCamera = _cutsceneCamera;
        return false;
    }
    
    private void GivePriorityOnce()
    {
        _gavePriorityOnce = true;
        _cutsceneCamera.SetAsActiveCamera();
    }
    
    private void PlayCameraOnce()
    {
        _playedCameraOnce = true;

        switch (_cutsceneCamera.GetCameraType())
        {
            case CutsceneCameraType.StaticCamera:
                if (_followTarget)
                    _cutsceneCamera.SetFollowTarget(_followTarget);
                _finishedCameraAnimation = true;  // static: immediately done
                
                // release camera, unblock swap camera execution on the next cutscene camera
                CutscenesHelper.CurrentCutsceneCamera = null;
                break;

            case CutsceneCameraType.TrailCamera:
                _cutsceneCamera.OnCameraReachedTheEnd += () => _finishedCameraAnimation = true;
                _cutsceneCamera.Play(_cameraSpeed);
                break;
        }
    }
}

public class ShowMovieLines : StoryCommand
{
    private float _duration;
    private bool _waitForCompletion;
    private bool _spawned;
    private bool _completed;

    public ShowMovieLines(bool waitForCompletion = false, float duration=0.6f)
    {
        _waitForCompletion = waitForCompletion;
        _duration = duration;
    }
    
    public bool Execute()
    {
        if (_completed)
            return true;
        
        if (_spawned)
            return false;
        
        // look for existing bars
        var existing = Object.FindObjectOfType<MovieBars>();
        if (existing)
        {
            Debug.LogError("Movie bars already shown!");
            return true;
        }

        // Load
        var prefab = Resources.Load<GameObject>("MovieBars");
        if (!prefab)
        {
            Debug.LogError("Movie bars prefab not found!");
            return true;
        }
        
        // Instantiate bars
        var barsObj = Object.Instantiate(prefab);
        var bars = barsObj.GetComponentInChildren<MovieBars>();
        _spawned = true;

        bars.PlayEnterAnimation(_duration, () => {
            _completed = true;
        });

        return !_waitForCompletion;   // keep running until animation ends
        // or continue if not waiting for completion.
    }
}

public class HideMovieLines : StoryCommand
{
    private float _duration;
    private bool _waitForCompletion;
    private bool _found;
    private bool _completed;

    public HideMovieLines(float duration=0.6f)
    {
        _duration = duration;
    }
    
    public bool Execute()
    {
        if (_completed)
            return true;
        
        if (_found)
            return false;
        
        // look for existing bars
        var existing = Object.FindObjectOfType<MovieBars>();
        if (!existing)
        {
            Debug.LogError("Movie bars not found!");
            return true;
        }

        // Load
        var barsInScene = Object.FindFirstObjectByType<MovieBars>();
        if (!barsInScene)
        {
            Debug.LogError("Movie bars prefab not found!");
            return true;
        }
        
        // start bars exit animation
        _found = true;
        barsInScene.PlayExitAnimation(_duration, () => {
            _completed = true;
            Object.Destroy(barsInScene.transform.parent.gameObject);
        });

        return !_waitForCompletion;   // keep running until animation ends
        // or continue if not waiting for completion.
    }
}
