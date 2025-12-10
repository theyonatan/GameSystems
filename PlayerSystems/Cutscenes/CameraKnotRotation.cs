using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.Splines;
using Unity.Mathematics;

public class CameraKnotRotation : MonoBehaviour
{
    [SerializeField] CinemachineCamera vcam;
    CinemachineSplineDolly dolly;
    SplineContainer spline;

    private void Start()
    {
        if (!vcam)
            vcam = transform.parent.GetComponentInChildren<CinemachineCamera>();

        dolly = vcam.GetComponent<CinemachineSplineDolly>();
        spline = dolly?.Spline;
    }

    private void LateUpdate()
    {
        if (!dolly || !spline) return;
        
        // normalized position along spline
        float cameraPosition = dolly.CameraPosition;

        var knots = spline.Spline.ToArray();
        int count = knots.Length;
        if (count < 2) return;
        
        // compute knot indices
        float closestKnotPosition = cameraPosition * (count - 1);
        int closestKnotIndex = Mathf.FloorToInt(closestKnotPosition);
        int lastKnotIndex = Mathf.Clamp(closestKnotIndex + 1, 0, count - 1);

        float difference = closestKnotPosition - closestKnotIndex;

        if (closestKnotIndex < 0 || closestKnotIndex >= knots.Length 
                                 || lastKnotIndex < 0 || lastKnotIndex >= knots.Length) return;
        
        quaternion startRotation = knots[closestKnotIndex].Rotation;
        quaternion endRotation = knots[lastKnotIndex].Rotation;

        quaternion newRotation = math.slerp(startRotation, endRotation, difference);

        // Set camera rotation
        vcam.transform.rotation = newRotation;
    }
}
