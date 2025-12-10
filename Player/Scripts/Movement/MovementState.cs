using UnityEngine;

[System.Serializable]
public abstract class MovementState
{
    /// <summary>
    /// /// MovementState will have many inheriting states, each for a different movement mode.
    /// the default will be Idle,
    /// and depending on different input events, (player clicked run button or started moving with WASD)
    /// I will change the movement state.
    /// 
    /// EnterState() - loading the state data at the beginning of the game.
    /// </summary>
    protected InputDirector Director;
    protected MovementManager Controller;
    
    /// <summary>
    /// Shared between all states.
    /// so if this changes in one, it applies to all of them.
    /// </summary>
    protected PlayerStateData LocalData;

    public bool CanMove = true;
    public bool CanRun = true;
    
    /// <summary>
    /// a state uses either a Rigidbody or a CharacterController.
    /// this alerts the movement manager which to use.
    /// </summary>a
    public abstract MovementComponentType ComponentType { get; }

    // State machine
    public abstract void LoadState(MovementManager manager, InputDirector director);
    public abstract void EnterState();
    public abstract void UpdateState();
    public abstract void FixedUpdate();
    public abstract void CleanState();


    // Extra calls

    public virtual void OnCollisionStay(Collision collision)
    {
        
    }
}
