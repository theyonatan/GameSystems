using System;
using UnityEngine;

[Serializable]
[CreateAssetMenu(fileName = "PlayerStateData", menuName = "Zero/PlayerStateData", order = 1)]
public class PlayerStateData : ScriptableObject
{
    public float Acceleration = 2f;
    public float WalkingSpeed = 5f;
    public float RunningSpeed = 10f;
    
    public float JumpSpeed = 10f;
    public float JumpHeight = 4f;

    public Vector2 CameraSpeed;

    public Vector3 TimeToReachTargetRotation = new(0f, 0.14f, 0f);
    
    #region Changeables

    public float acceleration
    {
        get => Acceleration;
        set
        {
            if (Mathf.Approximately(Acceleration, value)) return;
            Acceleration = value;
            OnAccelerationChanged?.Invoke(value);
        }
    }

    public float walkingSpeed
    {
        get => WalkingSpeed;
        set
        {
            if (Mathf.Approximately(WalkingSpeed, value)) return;
            WalkingSpeed = value;
            OnWalkingSpeedChanged?.Invoke(value);
        }
    }

    public float runningSpeed
    {
        get => RunningSpeed;
        set
        {
            if (Mathf.Approximately(RunningSpeed, value)) return;
            RunningSpeed = value;
            OnRunningSpeedChanged?.Invoke(value);
        }
    }

    // Public events
    public event Action<float> OnAccelerationChanged;
    public event Action<float> OnWalkingSpeedChanged;
    public event Action<float> OnRunningSpeedChanged;

    #endregion
}

