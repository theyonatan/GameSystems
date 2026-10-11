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

    // Sensor can now detect multiple targets and follow one of them!
    private SphereCollider _detectionRange;
    private readonly HashSet<GameObject> _targetsInRange = new();

    public event Action OnTargetChanged = delegate { };

    public Vector3 TargetPosition => _target ? _target.transform.position : Vector3.zero;
    public GameObject Target => _target;
    public bool IsTargetInRange => _targetValidator != null ? IsValidTarget(_target) : TargetPosition != Vector3.zero;
    private Func<GameObject, bool> _targetValidator;

    // Opt-in: existing sensors keep their legacy policy unless their agent configures one.
    public void ConfigureValidatedRange(float worldRadius, Func<GameObject, bool> validator)
    {
        _targetValidator = validator;
        var scale = transform.lossyScale;
        detectionRadius = worldRadius / Mathf.Max(.001f, Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z))));
        _detectionRange.radius = detectionRadius;
        RefreshTargets();
    }

    private bool IsValidTarget(GameObject target) => target && target.activeInHierarchy &&
        (_targetValidator == null || _targetValidator(target)) &&
        (target.transform.position - transform.position).sqrMagnitude <= WorldRadius * WorldRadius;

    private float WorldRadius
    {
        get { var scale = transform.lossyScale; return _detectionRange.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z))); }
    }

    public void RefreshTargets()
    {
        if (_targetValidator == null) { UpdateTargetPosition(_target.OrNull()); return; }
        _targetsInRange.RemoveWhere(target => !IsValidTarget(target));
        var next = IsValidTarget(_target) ? _target : GetFallbackTarget();
        if (next != _target) UpdateTargetPosition(next);
    }

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
            RefreshTargets();
            UpdateTargetPosition(_target.OrNull());
            _timer.Start();
        };
        _timer.Start();
    }

    private void Update()
    {
        // Trigger exits are not guaranteed on death/disable, and revenge targets may never have entered.
        if (_targetValidator != null) RefreshTargets();
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

        if (_targetValidator != null && !IsValidTarget(other.gameObject)) return;

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
        _targetsInRange.RemoveWhere(target => !target || _targetValidator != null && !IsValidTarget(target));

        foreach (GameObject target in _targetsInRange)
            return target;

        return null;
    }
    
    // Sensor can now prioritize a given target!
    public void PrioritizeTarget(GameObject target)
    {
        if (!target || _targetValidator != null && !IsValidTarget(target))
            return;

        _targetsInRange.Add(target);
        UpdateTargetPosition(target);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = IsTargetInRange ? Color.red : sensorColor;
        Gizmos.DrawWireSphere(transform.position, _targetValidator != null ? WorldRadius : detectionRadius);
    }
}
