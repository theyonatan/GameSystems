using System;
using System.Collections.Generic;
using TransitionsPlus;
using UnityEngine;
using UnityEngine.AI;

public class StoryCharacter : MonoBehaviour
{
    public StoryCharacterPrefab CharacterStory;
    public string CutsceneId;
    [SerializeField] private Transform headPosition;
    private StoryExecuter _storyExecuter;
    private NavMeshAgent _navMeshAgent;

    /// <summary>
    /// setup self (story character)
    /// adds script variables
    /// </summary>
    public StoryCharacter SetUp()
    {
        _storyExecuter = StoryExecuter.Instance;
        _navMeshAgent = GetComponent<NavMeshAgent>(); // can be null

        return this;
    }

    /// <summary>
    /// setup all story characters
    /// </summary>
    public void SetUp(Dictionary<Characters, StoryCharacter> characters)
    {
        foreach (StoryCharacter character in characters.Values)
            character.SetUp();
    }

    public void DebugSay(string text)
    {
        _storyExecuter.addAction(new DebugSay(text));
    }

    public void Say(string text, bool speakWhatHeSays=false)
    {
        Transform characterTransform = CharacterStory.ShowTailWhenTalking
            ? GetCharacterHeadTransform() : null;

        _storyExecuter.addAction(new Say(text, characterTransform));
    }
    
    public void GoTo(Vector3 targetPosition, float speed = 4f)
    {
        _storyExecuter.addAction(new GoTo(transform, targetPosition, speed, _navMeshAgent));
    }
    public void GoTo(GameObject targetObject, float speed = 4f)
    {
        _storyExecuter.addAction(new GoTo(transform, targetObject.transform.position, speed, _navMeshAgent));
    }
    public void GoTo(StoryObject targetObject, float speed = 4f)
    {
        _storyExecuter.addAction(new GoTo(transform, targetObject.GetLocation(), speed, _navMeshAgent));
    }
    public void GoTo(string storyObjectId, float speed = 4f, bool withRotation=false, bool withAnimation=true, float gotoDistance=0.4f)
    {
        var animator = GetComponent<Animator>();
        if (animator == null)
            animator = gameObject.GetComponentInChildren<Animator>();
        if (!withAnimation)
            animator = null;

        if (!StoryHelper.FindStoryObjectInScene(storyObjectId, out StoryObject gotoObject))
            return;
        
        _storyExecuter.addAction(new GoTo(transform, gotoObject.GetLocation(), speed, _navMeshAgent, animator, gotoDistance));
        if (withRotation)
            _storyExecuter.addAction(new RotateTo(transform, gotoObject.transform.rotation));
    }

    public void LookAt(Transform targetTransform, float speed = 4f)
    {
        _storyExecuter.addAction(new LookAt(transform, targetTransform, speed));
    }
    
    public void LookAt(GameObject targetObject, float speed = 4f)
    {
        _storyExecuter.addAction(new LookAt(transform, targetObject.transform, speed));
    }
    
    public void LookAt(StoryObject targetTransform, float speed = 4f)
    {
        _storyExecuter.addAction(new LookAt(transform, targetTransform.transform, speed));
    }
    
    public void LookAtActiveCamera(float speed = 4f)
    {
        var activeCamera = CutscenesHelper.GetActive();
        _storyExecuter.addAction(new LookAt(transform, activeCamera, speed: speed));
    }

    public void WalkToPositionWithoutRotating(Vector3 position, Vector3? lookTo = null)
    {
        lookTo ??= Vector3.zero;

        Debug.LogError("I think I forgot to do this one");
        Debug.Log("going (without rotating) to " + position);
    }

    public void RotateTo(Quaternion rotation)
    {
        _storyExecuter.addAction(new RotateTo(transform, rotation));
    }
    
    public void RotateTo(Transform targetTransform)
    {
        _storyExecuter.addAction(new RotateTo(transform, targetTransform.rotation));
    }
    
    public void RotateTo(string storyObjectId)
    {
        if (StoryHelper.FindStoryObjectInScene(storyObjectId, out StoryObject rotateObject))
            _storyExecuter.addAction(new RotateTo(transform, rotateObject.transform.rotation));
    }

    public void WaitForPlayerToGetTo(GameObject targetObject)
    {
        GameObject player = GameObject.FindWithTag("Player");
        _storyExecuter.addAction(new WaitUntilPlayerNearGameobject(player.transform, targetObject.transform.position));
    }

    /// <summary>
    /// plays given animation
    /// </summary>
    public void Behave(string animationName, bool continueStoryWhilePlaying=false)
    {
        var animator = GetComponent<Animator>();
        if (animator == null)
            animator = gameObject.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogError("no animator found when requested behave!");
            return;
        }
                
        _storyExecuter.addAction(new PlayAnimation(animator, animationName, continueStoryWhilePlaying));
    }
    
    public void BehaveBool(string parameterName, bool boolValue=true, float delayStoryForAnimation=0f)
    {
        var animator = GetComponent<Animator>();
        if (animator == null)
            animator = gameObject.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogError("no animator found when requested behave!");
            return;
        }
        
        _storyExecuter.addAction(new Behave(animator, parameterName, true, boolValue));
        
        if (delayStoryForAnimation > 0f)
            _storyExecuter.addAction(new Delay(delayStoryForAnimation));
    }
    
    public void BehaveTrigger(string parameterName, float delayStoryForAnimation=0f)
    {
        var animator = GetComponent<Animator>();
        if (animator == null)
            animator = gameObject.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogError("no animator found when requested behave!");
            return;
        }
        
        _storyExecuter.addAction(new Behave(animator, parameterName, false));
        
        if (delayStoryForAnimation > 0f)
            _storyExecuter.addAction(new Delay(delayStoryForAnimation));
    }
    
    public void TeleportTo(Vector3 position)
    {
        _storyExecuter.addAction(new Teleport(transform, position));
    }

    public void TeleportTo(Transform targetTransform)
    {
        _storyExecuter.addAction(new Teleport(transform, targetTransform.position));
    }

    public void TeleportTo(string storyObjectId)
    {
        if (StoryHelper.FindStoryObjectInScene(storyObjectId, out StoryObject teleportObject))
            _storyExecuter.addAction(new Teleport(transform, teleportObject.GetLocation()));
    }
    
    public void TeleportPlayer(Vector3 position)
        => _storyExecuter.addAction(new TeleportPlayer(position));

    public void TeleportPlayer(Transform targetTransform)
        => _storyExecuter.addAction(new TeleportPlayer(targetTransform.position));

    public void TeleportPlayer(string storyObjectId)
    {
        if (StoryHelper.FindStoryObjectInScene(storyObjectId, out StoryObject teleportObject))
            _storyExecuter.addAction(new TeleportPlayer(teleportObject.GetLocation()));
    }

    public void HidePlayer()
        => _storyExecuter.addAction(new HidePlayer());

    public void ShowPlayer()
        => _storyExecuter.addAction(new ShowPlayer());

    /// <summary>
    /// Camera & Cutscene Controls
    /// </summary>
    
    public void SwapPlayerState<TNewMovementState, TNewCameraState>()
    where TNewMovementState : MovementState, new()
    where TNewCameraState : CameraState, new()
    {
        _storyExecuter.addAction(new SwapPlayerState<TNewMovementState, TNewCameraState>());
    }
    
    public void SwapCamera(
        CutsceneCamera vcam,
        Transform followTargetTransform=null,
        float speed=0.2f,
        bool instantCut=false,
        bool continueStoryOverCamera=true
        ) => _storyExecuter.addAction(new SwapCamera(
            vcam, speed, continueStoryOverCamera, instantCut, followTargetTransform));

    public void SwapCamera(
        string vcamName,
        Transform followTargetTransform = null,
        float speed = 0.2f,
        bool instantCut = false,
        bool continueStoryOverCamera = true
    )
    {
        var vcam = CutscenesHelper.LocateCamera(vcamName);
        
        _storyExecuter.addAction(new SwapCamera(
            vcam, speed, continueStoryOverCamera, instantCut, followTargetTransform));
    }

    public void SwapSkin(string skinSourceName, int playerId = -1)
        => _storyExecuter.addAction(new SwapPlayerSkin(skinSourceName, playerId));

    public void EquipItem(
        int playerId=-1,
        string equipType = null,
        string equipLocation = null)
        => _storyExecuter.addAction(new ToggleEquipItem(true, playerId, equipType, equipLocation));
    
    public void UnEquipItem(
        int playerId=-1,
        string equipType = null,
        string equipLocation = null)
        => _storyExecuter.addAction(new ToggleEquipItem(false, playerId, equipType, equipLocation));
    
    public void ShowMovieBars(bool waitForCompletion = false, float duration = 0.6f)
        => _storyExecuter.addAction(new ShowMovieLines(
            waitForCompletion, duration));
    
    public void HideMovieBars(float duration = 0.6f) 
        => _storyExecuter.addAction(new HideMovieLines(
            duration));
    
    /// <summary>
    /// System Story Commands
    /// </summary>

    public void EnableInput() => _storyExecuter.addAction(new EnableInput());
    
    public void DisableInput() => _storyExecuter.addAction(new DisableInput());
    
    public void ShowCursor() => _storyExecuter.addAction(new ShowCursor());
    
    public void HideCursor() => _storyExecuter.addAction(new HideCursor());

    public void EnableJump() => _storyExecuter.addAction(new EnableJumpInput());
    
    public void DisableJump() => _storyExecuter.addAction(new DisableJumpInput());

    public void DelayFrame() => DelayFrames(1);
    
    public void DelayFrames(int frames) => _storyExecuter.addAction(new DelayFrames(frames));
    
    public void DelayedAction(Action action)
    {
        _storyExecuter.addAction(new DelayedStoryAction(action));
    }

    public void DelayedCustomAction(Func<bool> action)
    {
        _storyExecuter.addAction(new DelayedCustomStoryAction(action));
    }

    public void Delay(float time)
    {
        _storyExecuter.addAction(new Delay(time));
    }

    /// <summary>
    /// In the next scene, hook to OnChapterFinished.
    /// </summary>
    public void LoadScene(string sceneName, GameObject loadingScreen, bool unloadActiveScene = true)
    {
        _storyExecuter.addAction(new LoadScene(sceneName, loadingScreen, unloadActiveScene));
    }

    public void StartTransition(TransitionProfile transitionProfile)
    {
        _storyExecuter.addAction(new StartTransition(transitionProfile));
    }

    public void KillTransition()
    {
        _storyExecuter.addAction(new KillTransition());
    }
    
    /// <summary>
    /// NEVER use this!
    /// instead, place the character in the scene from ahead, spawn it, and then use it.
    /// otherwise you can't call story actions on it!
    /// </summary>
    [Obsolete("Use SpawnCharacter instead!")]
    public static void SpawnCharacter(GameObject character, Vector3 position, Quaternion? rotationDirection = null)
    {
        Quaternion quaternion = Quaternion.identity;
        if (rotationDirection.HasValue)
            quaternion = rotationDirection.Value;

        Instantiate(character, position, quaternion);
    }
    
    public void SpawnCharacter(StoryCharacter character, Transform spawnPoint=null, Transform parent = null)
        => _storyExecuter.addAction(new SpawnCharacter(character, spawnPoint, parent));

    public void DespawnCharacter()
        => _storyExecuter.addAction(new DespawnCharacter(this));
    
    public void DespawnCharacter(StoryCharacter character)
        => _storyExecuter.addAction(new DespawnCharacter(character));

    public void SetActive(GameObject inactiveGameObject, bool activeState=true)
    => DelayedAction(() => inactiveGameObject.SetActive(activeState));

    public void SetActiveStoryObject(string inactiveStoryObject, bool activeState = true)
    {
        if (StoryHelper.FindStoryObjectInScene(inactiveStoryObject, out StoryObject storyObject))
            DelayedAction(() => storyObject.gameObject.SetActive(activeState));
    }
    
    /// <summary>
    /// Non-Story actions
    /// </summary>
    private Transform GetCharacterHeadTransform()
    {
        if (headPosition)
            return headPosition;
        return transform;
    }
}
