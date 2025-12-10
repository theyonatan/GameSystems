using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class firstPersonStoryExample : MonoBehaviour
{
    private void Awake()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;
        MovementManager movementManager = player.GetComponentInChildren<MovementManager>();
        CameraManager cameraManager = player.GetComponentInChildren<CameraManager>();

        movementManager.StartingState = new FirstPersonState();
        cameraManager.StartingState = new CameraStateFirstPerson();
    }
}
