using System;
using UnityEngine;

public class CharacterOrientation : MonoBehaviour
{
    [SerializeField] private bool lockOrientationToZero = true;

    private void Update()
    {
        if (lockOrientationToZero)
            transform.localPosition = Vector3.zero;
    }
}
