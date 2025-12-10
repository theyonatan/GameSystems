using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerRotator : IPlayerRotator
{
    GameObject _character;
    Transform _camTransform;

    Vector3 _currentTargetRotation = Vector3.zero;
    Vector3 _timeToReachTargetRotation = Vector3.zero;

    Vector3 _dumpedVelocity = Vector3.zero;
    Vector3 _dumpedVelocityPassedTime = Vector3.zero;

    float _directionAngle;

    // Constractor
    public PlayerRotator(Transform cam, Vector3 timeToReachTargetRotation)
    {
        _character = GameObject.FindGameObjectWithTag("Character");
        _camTransform = cam;
        this._timeToReachTargetRotation = timeToReachTargetRotation;
    }

    // normal rotation - gets called every update.
    public void RotatePlayer(Vector2 inputRotation)
    {
        // calculate the new rotation
        CalculateTargetRotation(inputRotation);
        // rotate the player
        SmoothlyRotatePlayer();
    }

    // rotate without update (asynchronously to Unity)
    public void RotatePlayerAsync(Vector2 targetRotation)
    {
        // set targetRotationValues

        // smoothly rotate the player
        SmoothlyRotatePlayer();
    }

    void CalculateTargetRotation(Vector2 inputRotation)
    {
        // calculate the rotation angle
        CalculateDirectionAngle(inputRotation);

        // add the rotation of the camera
        AddCameraRotation();

        // if the player has a new "target" for a new rotation (new target rotation),
        if (_directionAngle != _currentTargetRotation.y)
        {
            // ...than reset the dumping.
            _currentTargetRotation.y = _directionAngle;
            _dumpedVelocityPassedTime.y = 0f;
        }
    }
    void CalculateDirectionAngle(Vector2 inputRotation)
    {
        _directionAngle = Mathf.Atan2(inputRotation.x, inputRotation.y) * Mathf.Rad2Deg; // get the angle (in degrees)

        if (_directionAngle < 0f)
            _directionAngle += 360f;
    }
    void AddCameraRotation()
    {
        _directionAngle += _camTransform.eulerAngles.y;

        if (_directionAngle > 360f)
            _directionAngle -= 360f;
    }

    void SmoothlyRotatePlayer()
    {
        // current rotation
        float currentYAngle = _character.transform.rotation.eulerAngles.y;

        // no need to rotate
        if (currentYAngle == _currentTargetRotation.y)
            return;

        // get the smooth angle rotation
        float smoothYAngle = Mathf.SmoothDampAngle(currentYAngle, _currentTargetRotation.y, ref _dumpedVelocity.y, _timeToReachTargetRotation.y - _dumpedVelocityPassedTime.y);
        _dumpedVelocity.y += Time.deltaTime; // with dumping

        // final target rotation
        Quaternion targetRotation = Quaternion.Euler(0f, smoothYAngle, 0f);

        // rotate the player
        _character.transform.rotation = targetRotation;
    }

}
