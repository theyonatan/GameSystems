using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class Sensor : MonoBehaviour
{
    [SerializeField] private string detectionTag = "Player";
    [SerializeField] private float detectionRadius = 5f;
    [SerializeField] private float timerInterval = 1f;
    [SerializeField] private Color sensorColor = Color.green;
    public bool ResetGoapOnTargetChange = true;

    private SphereCollider _detectionRange;
    private readonly HashSet<GameObject> _targetsInRange = new();

    public event Action OnTargetChanged = delegate { };

    public Vector3 TargetPosition => _target ? _target.transform.position : Vector3.zero;
    public bool IsTargetInRange => TargetPosition != Vector3.zero;

    private GameObject _target;
    private Vector3 _lastKnownPosition;
    private CountdownTimer _timer;


    private void Awake()
    {
        _detectionRange = GetComponent<SphereCollider>();
        _detectionRange.isTrigger = true;
        _detectionRange.radius = detectionRadius;
        transform.localScale = Vector3.one;
    }

    private void Start()
    {
        _timer = new CountdownTimer(timerInterval);
        _timer.OnTimerStop += () =>
        {
            UpdateTargetPosition(_target.OrNull());
            _timer.Start();
        };
        _timer.Start();
    }

    private void Update()
    {
        _timer.Tick(Time.deltaTime);
    }

    private void UpdateTargetPosition(GameObject target = null)
    {
        bool targetChanged = _target != target;

        _target = target;

        Vector3 currentPosition = TargetPosition;
        bool positionChanged = _lastKnownPosition != currentPosition;

        if (!targetChanged && !positionChanged)
            return;

        _lastKnownPosition = currentPosition;
        OnTargetChanged.Invoke();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(detectionTag))
            return;

        _targetsInRange.Add(other.gameObject);

        // Preserve the existing behaviour: newest player becomes the target.
        UpdateTargetPosition(other.gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(detectionTag))
            return;

        _targetsInRange.Remove(other.gameObject);

        // A different player left; keep chasing the current target.
        if (other.gameObject != _target)
            return;

        UpdateTargetPosition(GetFallbackTarget());
    }
    
    private GameObject GetFallbackTarget()
    {
        _targetsInRange.RemoveWhere(target => !target);

        foreach (GameObject target in _targetsInRange)
            return target;

        return null;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = IsTargetInRange ? Color.red : sensorColor;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
