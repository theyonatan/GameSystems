using System;
using UnityEngine;

public class MovementManager : MonoBehaviour
{
    /// <summary>
    /// manages the different movement states.
    /// 
    /// MovementState will have many inheriting states, each for a different movement mode.
    /// the default will be Normal,
    /// and depending on different story events, (player moved through ski gate, or touched a cave entrance)
    /// I will change the movement state.
    /// 
    /// only update will get called from here.
    /// 
    /// also note,
    /// some events will not be started by input, but by game events.
    /// For that, I will have public functions to change the state when the events happen.
    /// for example:
    /// on a random mountain, a ski sign, on triggerEnter:
    /// other.playerGameObject.getcomponent<movementManager>().onEnterSkiState();
    /// </summary>

    // State
    [SerializeReference]
    public MovementState CurrentState;
    public MovementState StartingState;

    // Assignables
    private CameraManager _cameraManager;
    private InputDirector _director;
    private CapsuleCollider _capsuleCollider;

    // Multiplayer
    public bool IsOwner = true;


    private void Awake()
    {
        _director = InputDirector.Instance;
        _capsuleCollider = GetComponent<CapsuleCollider>();
        
        // if injected custom start state OR default to third person state
        CurrentState = StartingState ?? new DefaultMovementState();

        CurrentState.LoadState(this, _director);
        CurrentState.EnterState();
    }

    public void ChangeState(MovementState newState)
    {
        // verify we need to switch
        if (newState.GetType() == CurrentState.GetType())
            return;
        
        CurrentState.CleanState();
        CurrentState = newState;
        
        EnsureReqiredStateType(newState.ComponentType);
        CurrentState.LoadState(this, _director);
        
        CurrentState.EnterState();
    }

    private void EnsureReqiredStateType(MovementComponentType stateType)
    {
        switch (stateType)
        {
            case MovementComponentType.Rigidbody:
                EnsureOnly<Rigidbody>();
                _capsuleCollider.isTrigger = false;
                break;
            case MovementComponentType.CharacterController:
                EnsureOnly<CharacterController>();
                _capsuleCollider.isTrigger = true;
                break;
            case MovementComponentType.None:
                Remove<Rigidbody>();
                Remove<CharacterController>();
                _capsuleCollider.isTrigger = false;
                break;
        }
    }

    void Update()
    {
        if (!IsOwner)
            return;

        CurrentState.UpdateState();
    }

    private void FixedUpdate()
    {
        if (!IsOwner)
            return;

        CurrentState.FixedUpdate();
    }

    private void OnDestroy()
    {
        CurrentState.CleanState();
    }

    public void EnableMovement()
    {
        CurrentState.CanMove = true;
    }

    public void DisableMovement()
    {
        CurrentState.CanMove = false;
    }
    
    // helpers
    private void EnsureOnly<T>() where T : Component
    {
        // Add required
        if (!TryGetComponent(out T addedComponent))
            addedComponent = gameObject.AddComponent<T>();

        // Remove the other type
        if (typeof(T) == typeof(Rigidbody))
            Remove<CharacterController>();
        else
            Remove<Rigidbody>();
        
        // config if cc
        if (addedComponent is CharacterController cc)
        {
            cc.center = new Vector3(0f, 0.7f, 0f);
            cc.height = 1.4f;
            cc.radius = 0.3f;
            cc.skinWidth = 0.01f;
        }
        
    }

    private void Remove<T>() where T : Component
    {
        if (TryGetComponent(out T existing))
            Destroy(existing);
    }
}
