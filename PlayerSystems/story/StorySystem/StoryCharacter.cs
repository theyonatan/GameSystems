using System;
using System.Collections.Generic;
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
    public void SetUp()
    {
        _storyExecuter = StoryExecuter.Instance;
        _navMeshAgent = GetComponent<NavMeshAgent>(); // can be null
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
    public void GoTo(string storyObjectId, float speed = 4f)
    {
        if (StoryHelper.FindStoryObjectInScene(storyObjectId, out StoryObject gotoObject))
            _storyExecuter.addAction(new GoTo(transform, gotoObject.GetLocation(), speed, _navMeshAgent));
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

    }

    public void TeleportTo(Vector3 position)
    {

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
        bool continueStoryOverCamera=true
        ) => _storyExecuter.addAction(new SwapCamera(
            vcam, speed, continueStoryOverCamera, followTargetTransform));
    
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
    
    public void DelayedAction(Action action)
    {
        _storyExecuter.addAction(new DelayedStoryAction(action));
    }

    public void Delay(float time)
    {
        _storyExecuter.addAction(new Delay(time));
    }

    public static void SpawnCharacter(GameObject character, Vector3 position, Quaternion? rotationDirection = null)
    {
        Quaternion quaternion = Quaternion.identity;
        if (rotationDirection.HasValue)
            quaternion = rotationDirection.Value;

        Instantiate(character, position, quaternion);
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
