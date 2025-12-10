using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraStateMount : CameraState
{
    public override void ClearState()
    {
        throw new System.NotImplementedException();
    }

    public override void FixedUpdate()
    {
        throw new System.NotImplementedException();
    }

    public override void LoadState(CameraManager manager, InputDirector director)
    {
        InputDirector = director;
    }

    public override void EnterState()
    {
        throw new System.NotImplementedException();
    }

    public override void UpdateState()
    {
        throw new System.NotImplementedException();
    }

    public override void OnDestroy()
    {
        throw new System.NotImplementedException();
    }
}
