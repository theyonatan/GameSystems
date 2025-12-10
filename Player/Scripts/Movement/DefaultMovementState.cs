using System;
using UnityEngine;

public class DefaultMovementState : MovementState
{
    [Header("Currently Using: DefaultMovementState")]
    public const string CurrentStateName = "Default MovementState";


    public override MovementComponentType ComponentType => MovementComponentType.None;

    public override void LoadState(MovementManager manager, InputDirector director)
    {
        Controller = manager;
    }

    public override void EnterState()
    {
        Debug.Log("Entering Default (Empty) Movement state!");
    }

    public override void FixedUpdate()
    {
        
    }

    public override void UpdateState()
    {
        
    }
    
    public override void CleanState()
    {
        
    }
}