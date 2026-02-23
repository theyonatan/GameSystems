using UnityEngine;

public class Float : MonoBehaviour
{
    [Header("Float Settings")]
    [Tooltip("How high the object moves up and down.")]
    public float FloatHeight = 0.5f;

    [Tooltip("How fast the object floats.")]
    public float FloatSpeed = 1f;

    [Tooltip("Optional random offset so multiple objects don't sync.")]
    public bool RandomOffset = true;

    float _startY;
    float _offset;

    void Start()
    {
        _startY = transform.localPosition.y;

        if (RandomOffset)
            _offset = Random.Range(0f, Mathf.PI * 2f);
    }

    void LateUpdate()
    {
        float newY = _startY + Mathf.Sin(Time.time * FloatSpeed + _offset) * FloatHeight;

        Vector3 pos = transform.localPosition;
        pos.y = newY;
        transform.localPosition = pos;
    }
}