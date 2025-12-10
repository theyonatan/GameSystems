using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkiState : MovementState
{

    public override MovementComponentType ComponentType => MovementComponentType.Rigidbody;

    public override void LoadState(MovementManager manager, InputDirector director)
    {
        Controller = manager;
    }

    public override void EnterState()
    {
        Debug.Log("Starting to ski");
    }

    public override void FixedUpdate()
    {
        Debug.Log("Skiing!");
    }

    public override void UpdateState()
    {
        Debug.Log("Skiing!");
    }

    public override void CleanState()
    {

    }
}
