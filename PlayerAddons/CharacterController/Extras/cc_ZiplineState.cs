using System;
using UnityEngine;

[Serializable]
public class cc_ZiplineState : MovementState
{
    [Header("Definition")]
    public override MovementComponentType ComponentType => MovementComponentType.CharacterController;
    [SerializeField] public const string StateName = "Zipline";

    private Player _player;
    private CharacterController _cc;
    private Transform _characterOrientation;

    public override void LoadState(MovementManager manager, InputDirector director)
    {
        Controller = manager;
        Director = director ?? InputDirector.Instance;

        _player = manager.GetComponent<Player>();
        _cc = manager.GetComponent<CharacterController>();

        var orientation = manager.GetComponentInChildren<CharacterOrientation>();
        if (orientation)
            _characterOrientation = orientation.transform;
    }

    public override void EnterState()
    {
        // kill leftover movement from previous state if needed
    }

    public override void UpdateState()
    {
        // Intentionally block normal movement input while ziplining.
        // Camera still works because TP_CameraState is still active.
    }

    public override void FixedUpdate()
    {
        
    }

    public override void CleanState()
    {
        
    }
}