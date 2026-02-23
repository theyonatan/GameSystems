using UnityEngine;

public class LookatMainCamera : MonoBehaviour
{
    private Transform _cameraTransform;
    private bool _cameraFound;

    private void Start()
    {
        if (Camera.main == null)
        {
            Debug.Log("Can't find main camera to lookat!");
            return;
        }
        
        _cameraFound = true;
        _cameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        if (!_cameraFound)
            return;
        
        Vector3 targetPos = _cameraTransform.position;
        targetPos.y = transform.position.y;
        
        transform.LookAt(targetPos);
        transform.Rotate(Vector3.up, 180f);
    }
}
