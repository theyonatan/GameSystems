using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class CameraState
{
    /// <summary>
    /// /// CameraState will have many inheriting states, each for a different camera mode.
    /// What is a camera mode?
    /// 
    /// glad you asked!
    /// example for 2 camera modes:
    /// - a camera going around the player, for a third person shooter
    /// - a camera stuck on a 2d grid, following the player (a sideview, 2d game camera)
    /// 
    /// there is no default here, the camera's spawn state is completely dependent on the scene we spawn at.
    /// also the camera states are controlled by the camera manager, it charges a state and than activates it.
    /// the only thing that can change a state is the camera manager, and the only one who can access it is the movement manager and
    /// in rare cases, the story manager.
    /// 
    /// 
    /// EnterState() - loads the current camera state.
    /// ClearState() - clears the current camera state so the camera is ready for switching.
    /// UpdateState() - I don't use update a lot with cameras since I use cinemachine, but it's good to have, just in case. :)
    /// 
    /// Receiving input:
    /// Every camera state will have a reference to the InputManager,
    /// and will create it's own functions upon receving input.
    /// usually, these functions will just be assigining the input to values so the update() can use them later.
    /// </summary>


    // references
    protected InputDirector InputDirector;

    // Settings
    public Vector2 CameraSpeed = Vector2.one;
    public float HorizontalSensitivity = 1.0f;
    public float VerticalSensitivity = 1.0f;
    public float ZoomSensitivity = 1.0f;

    // State data
    public bool CanLookAround = true;
    protected CameraManager Manager;
    
    // State machine
    public abstract void LoadState(CameraManager manager, InputDirector director);
    public abstract void EnterState();
    public abstract void UpdateState();
    public abstract void FixedUpdate();
    public abstract void ClearState();
    public abstract void OnDestroy();
}
