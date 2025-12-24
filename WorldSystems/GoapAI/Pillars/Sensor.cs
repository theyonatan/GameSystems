using System;
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
        _target = target;
        
        if (IsTargetInRange && (_lastKnownPosition != TargetPosition || _lastKnownPosition != Vector3.zero))
        {
            _lastKnownPosition = TargetPosition;
            OnTargetChanged.Invoke();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(detectionTag)) return;
        UpdateTargetPosition(other.gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(detectionTag)) return;
        UpdateTargetPosition();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = IsTargetInRange ? Color.red : sensorColor;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
