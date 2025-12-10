using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IPlayerRotator
{
    // normal rotation
    public void RotatePlayer(Vector2 inputRotation);
    // rotate without update
    public void RotatePlayerAsync(Vector2 targetRotation);
}
