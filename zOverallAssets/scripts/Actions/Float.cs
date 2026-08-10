using UnityEngine;

public class Float : MonoBehaviour
{
    [Header("Float Settings")]
    [Tooltip("How high the object moves above and below its starting position.")]
    public float FloatHeight = 0.5f;

    [Tooltip("How fast the object floats.")]
    public float FloatSpeed = 1f;

    [Tooltip("Controls the speed throughout each movement. Default is linear.")]
    public AnimationCurve FloatCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Tooltip("Optional random offset so multiple objects don't sync.")]
    public bool RandomOffset = true;

    private float _startY;
    private float _offset;

    private void Start()
    {
        _startY = transform.localPosition.y;

        if (RandomOffset)
            _offset = Random.Range(0f, 2f);
    }

    private void LateUpdate()
    {
        // Repeatedly moves from 0 → 1 → 0.
        float progress = Mathf.PingPong(
            Time.time * FloatSpeed + _offset,
            1f
        );

        float curvedProgress = FloatCurve.Evaluate(progress);

        float newY = _startY +
                     Mathf.Lerp(-FloatHeight, FloatHeight, curvedProgress);

        Vector3 position = transform.localPosition;
        position.y = newY;
        transform.localPosition = position;
    }
}