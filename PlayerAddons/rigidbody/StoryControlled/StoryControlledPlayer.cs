using System;
using UnityEngine;

[System.Serializable]
public class StoryControlledPlayer : MovementState
{

    public override MovementComponentType ComponentType => MovementComponentType.None;

    public override void LoadState(MovementManager manager, InputDirector director)
    {
        throw new System.NotImplementedException();
    }

    public override void EnterState()
    {
        throw new System.NotImplementedException();
    }

    public override void UpdateState()
    {
        throw new System.NotImplementedException();
    }

    public override void FixedUpdate()
    {
        throw new System.NotImplementedException();
    }

    public override void CleanState()
    {
        throw new System.NotImplementedException();
    }
}
