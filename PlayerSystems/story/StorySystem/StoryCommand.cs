using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
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

public class PlayAnimation : StoryCommand
{
    private Animator _animator;
    private string _animationName;
    private bool _continueStoryWhilePlaying;
    private CountdownTimer _timer;

    private bool _startedPlayingAnimation;
    private bool _finishedAnimation;
    
    public PlayAnimation(Animator animator, string animationName, bool continueStoryWhilePlaying=false)
    {
        _animator = animator;
        _animationName = animationName;
        _continueStoryWhilePlaying = continueStoryWhilePlaying;
        
        _timer = new CountdownTimer(GetAnimationLength(_animationName));
        _timer.OnTimerStart += () => _animator.CrossFade(_animationName, 0.2f);
        _timer.OnTimerStop += () => _finishedAnimation = true;
    }
    
    public bool Execute()
    {
        _timer?.Tick(Time.deltaTime);
        
        // wait for animation to finish
        if (_startedPlayingAnimation)
            return _finishedAnimation || _continueStoryWhilePlaying;
        
        // play animation once
        CrossplayAnimationUsingTimer();
        
        return false;
    }
    
    private void CrossplayAnimationUsingTimer()
    {
        if (_startedPlayingAnimation)
            return;
        _startedPlayingAnimation = true;
        
        _timer.Start();
    }
    
    private float GetAnimationLength(string animationClipName)
    {
        int animationClipHash = Animator.StringToHash(animationClipName);
        
        foreach (AnimationClip clip in _animator.runtimeAnimatorController.animationClips) {
            if (Animator.StringToHash(clip.name) == animationClipHash) {
                Debug.Log("length: " + clip.length);
                return clip.length;
            }
        }

        return -1f;
    }
}

public class DelayFrames : StoryCommand
{
    private int _framesLeft;

    public DelayFrames(int frames)
    {
        _framesLeft = frames;
    }

    public bool Execute()
    {
        if (_framesLeft <= 0)
            return true;
        
        _framesLeft--;
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

public class DelayedCustomStoryAction : StoryCommand
{
    private readonly Func<bool> _delayedAction;

    public DelayedCustomStoryAction(Func<bool> delayedAction)
    {
        _delayedAction = delayedAction;
    }

    public bool Execute()
    {
        return _delayedAction();
    }
}

public class Delay : StoryCommand
{
    private readonly float _time;
    private bool _startedTimer;
    private bool _finishedTimer;
    private CountdownTimer _timer;
    
    public Delay(float time)
    {
        _time = time;
    }

    public bool Execute()
    {
        if (_startedTimer)
        {
            _timer.Tick(Time.deltaTime);
            return _finishedTimer;
        }
        
        _timer = new CountdownTimer(_time);
        _timer.OnTimerStart += () => _startedTimer = true;
        _timer.OnTimerStop += () => _finishedTimer = true;
        
        _timer.Start();
        
        return false;
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

public class ShowCursor : StoryCommand
{
    public bool Execute()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        return true;
    }
}

public class HideCursor : StoryCommand
{
    public bool Execute()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
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
        
        // if uses animations, refresh them
        var animationsManager = player.GetComponentInChildren<AnimationsManager>();
        if (animationsManager && animationsManager.Initialized)
            animationsManager.RefreshPlayerAnimator();

        return true;
    }
}

public class SwapCamera : StoryCommand
{
    private readonly CutsceneCamera _cutsceneCamera;
    private readonly float _cameraSpeed;
    private readonly bool _continueStoryOverCamera;
    private readonly bool _instantCut;
    private readonly Transform _followTarget;
    
    private bool _instantCutStarted;
    private int _instantCutWaitFrames;
    
    private bool _gavePriorityOnce;
    private bool _playedCameraOnce;
    private bool _finishedCameraAnimation;
    
    public SwapCamera(CutsceneCamera cutsceneCamera,
        float cameraSpeed,
        bool continueStoryOverCamera,
        bool instantCut=false,
        Transform followTarget=null)
    {
        _cutsceneCamera = cutsceneCamera;
        _cameraSpeed = cameraSpeed;
        _continueStoryOverCamera = continueStoryOverCamera;
        _followTarget = followTarget;
        _instantCut = instantCut;
        
        _gavePriorityOnce = false;
        _playedCameraOnce = false;
    }
    
    public bool Execute()
    {
        // Block Execution if another camera is running
        if (BlockUntilCutsceneCameraFree())
            return false;
        
        // Start Transition (Blend/Cut) to new virtual camera
        if (!_gavePriorityOnce)
            GivePriorityOnce();
        
        // wait for Cut to finish (if instant cut)
        if (_instantCut && !IsCutFinished())
            return false;
        
        // wait for blend to finish (if not instant cut)
        if (!_instantCut && !_cutsceneCamera.IsBlendFinished())
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
    /// Wait 4 frames for instant cut to init new, and restore original blend
    /// </summary>
    private bool IsCutFinished()
    {
        if (!_instantCutStarted)
        {
            _instantCutStarted = true;
            _instantCutWaitFrames = 4;
        }

        if (_instantCutWaitFrames > 0)
        {
            _instantCutWaitFrames--;
            return false;
        }

        return true;
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
        _cutsceneCamera.SetAsActiveCamera(_instantCut);
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

public class SwapPlayerSkin : StoryCommand
{
    private readonly int _playerId;
    private readonly string _skinSourceName;
    
    // -1 = offline, single player id
    public SwapPlayerSkin(string skinSourceName, int playerId)
    {
        _playerId = playerId;
        _skinSourceName = skinSourceName;
    }
    
    public bool Execute()
    {
        var player = Player.GetPlayer(_playerId);
        if (!player)
        {
            Debug.LogError($"SwapPlayerSkin StoryCommand: Couldn't find player with id {_playerId}!");
            return true;
        }
        
        player.SwapSkin(_skinSourceName);

        // continue over story, should only take 3 frames so it's fine.
        return true;
    }
}

public class ToggleEquipItem : StoryCommand
{
    // equip or unequip
    private readonly bool _toggleEquip;
    
    // equip filters
    private readonly int _playerId;
    private readonly string _equipType;
    private readonly string _equipLocation;
    
    public ToggleEquipItem(
        bool toggleEquip,
        int playerId,
        string equipType = null,
        string equipLocation = null)
    {
        _toggleEquip = toggleEquip;
        _playerId = playerId;
        _equipType = equipType;
        _equipLocation = equipLocation;
    }
    
    public bool Execute()
    {
        // get player
        var player = Player.GetPlayer(_playerId);
        if (!player)
        {
            Debug.LogError($"EquipHandItem StoryCommand: Couldn't find player with id {_playerId}!");
            return true;
        }

        // find all matching equipables on this player
        bool foundAny = false;
        var equipables = player.GetComponents<IEquipableHeldItem>();
        foreach (var equipable in equipables)
        {
            // check type (weapon, armour, hats...)
            if (_equipType != null && equipable.EquipType != _equipType)
                continue;

            // check location (hands, backpack, hats...)
            if (_equipLocation != null && equipable.EquipLocation != _equipLocation)
                continue;

            // found an item, toggle equip it
            if (_toggleEquip)
                equipable.Equip();
            else
                equipable.Unequip();

            foundAny = true;
        }
        
        // nothing matched the requested filters
        if (!foundAny)
            Debug.LogWarning($"No equipable item found. Type: {_equipType ?? "Any"}, Location: {_equipLocation ?? "Any"}");
        
        return true;
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

public class LoadScene : StoryCommand
{
    private readonly string _targetSceneName;
    private readonly GameObject _loadingScreenPrefab;
    private readonly bool _unloadActiveScene;

    private bool _started;
    private bool _finished;

    private GameObject _spawnedLoadingScreen;
    private LoadingBar _loadingBar;

    private AsyncOperation _loadLoadingScreenOp;
    private AsyncOperation _loadTargetSceneOp;
    private AsyncOperation _unloadOldSceneOp;

    private string _oldSceneName;

    /// <summary>
    /// In the next scene, hook to OnChapterFinished.
    /// </summary>
    public LoadScene(string targetSceneName, GameObject loadingScreenPrefab=null, bool unloadActiveScene = true)
    {
        _targetSceneName = targetSceneName;
        _loadingScreenPrefab = loadingScreenPrefab;
        _unloadActiveScene = unloadActiveScene;
    }

    public bool Execute()
    {
        // when done loading - go on with the story
        if (_finished)
            return true;

        // spawn loading screen when starting to load
        if (!_started)
        {
            _started = true;
            _oldSceneName = SceneManager.GetActiveScene().name;

            if (!_loadingScreenPrefab)
            {
                Debug.LogError($"LoadSceneCommand: loading screen prefab is null.");
                _finished = true;
                return true;
            }

            _spawnedLoadingScreen = Object.Instantiate(_loadingScreenPrefab);
            Object.DontDestroyOnLoad(_spawnedLoadingScreen);

            _loadingBar = _spawnedLoadingScreen.GetComponentInChildren<LoadingBar>(true);
            if (!_loadingBar)
            {
                Debug.LogError("LoadSceneCommand: no LoadingBar found in loading screen children.");
                Object.Destroy(_spawnedLoadingScreen);
                _finished = true;
                return true;
            }
        }
        
        // kill old scene player
        Player player = Player.GetPlayer(-1);
        player?.KillSelf();

        // load new scene
        if (_loadTargetSceneOp == null)
        {
            _loadTargetSceneOp = SceneManager.LoadSceneAsync(_targetSceneName, LoadSceneMode.Additive);
            if (_loadTargetSceneOp != null) _loadTargetSceneOp.allowSceneActivation = false;
            _loadingBar.SetProgress(0f);
            return false;
        }

        // Unity async scene progress goes 0..0.9 before activation
        float normalizedProgress = Mathf.Clamp01(_loadTargetSceneOp.progress / 0.9f);
        _loadingBar.SetProgress(normalizedProgress);

        if (_loadTargetSceneOp.progress >= 0.9f)
        {
            _loadingBar.SetProgress(1f);
            _loadTargetSceneOp.allowSceneActivation = true;

            // wait until scene fully loaded
            if (!_loadTargetSceneOp.isDone)
                return false;

            Scene loadedScene = SceneManager.GetSceneByName(_targetSceneName);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
                SceneManager.SetActiveScene(loadedScene);

            if (_unloadActiveScene && !string.IsNullOrEmpty(_oldSceneName) && _oldSceneName != _targetSceneName)
            {
                if (_unloadOldSceneOp == null)
                {
                    _unloadOldSceneOp = SceneManager.UnloadSceneAsync(_oldSceneName);
                    return false;
                }
                
                if (!_unloadOldSceneOp.isDone)
                    return false;
            }

            if (_spawnedLoadingScreen)
                Object.Destroy(_spawnedLoadingScreen);
            
            _finished = true;
            return true;
        }
        
        return false;
    }
}

/// <summary>
/// NEVER Call instantiate for story characters!!!
/// LIKE IN PBB: First put them in the scene, disable them, and use this command to activate them.
/// </summary>
public class SpawnCharacter : StoryCommand
{
    private readonly StoryCharacter _character;
    private readonly Transform _spawnPoint;
    private readonly Transform _parent;

    public SpawnCharacter(StoryCharacter character, Transform spawnPoint=null, Transform parent = null)
    {
        _character = character;
        _spawnPoint = spawnPoint;
        _parent = parent;
    }

    public bool Execute()
    {
        if (!_character)
        {
            Debug.LogError("[SpawnCharacter] Character is null!");
            return true;
        }

        if (_parent)
            _character.transform.SetParent(_parent);

        if (_spawnPoint)
        {
            _character.transform.SetPositionAndRotation(
                _spawnPoint.position,
                _spawnPoint.rotation
            );
        }

        _character.gameObject.SetActive(true);

        return true;
    }
}

public class DespawnCharacter : StoryCommand
{
    private readonly StoryCharacter _character;

    public DespawnCharacter(StoryCharacter character)
    {
        _character = character;
    }

    public bool Execute()
    {
        if (!_character)
        {
            Debug.LogError("[DespawnCharacter] Character is null!");
            return true;
        }

        _character.gameObject.SetActive(false);

        return true;
    }
}