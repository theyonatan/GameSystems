using UnityEngine;

public class ExtensionSimpleGraplingHook : MonoBehaviour
{
    private InputDirector _inputDirector;
    public Camera Cam;
    public LineRenderer RopeLine;

    [Header("Grapple")]
    public float MaxGrappleDistance = 60f;
    public Color HighlightColor = Color.yellow;
    private bool _hittingGrappleStick;
    private RaycastHit _grappleHit;

    private Rigidbody _rb;
    private ConfigurableJoint _joint;
    private GrappleTarget _currentHighlight;
    private Vector3 _grappleHitPoint;
    private float _ropeLength;
    
    private void Start()
    {
        // collect input
        _inputDirector = GetComponent<InputDirector>();
        _inputDirector.OnFireStarted += StartGrapple;
        _inputDirector.OnFireReleased += StopGrapple;
        
        // collect references
        _rb = GetComponent<Rigidbody>();
        if (!Cam) Cam = Camera.main;
        if (RopeLine)
        {
            RopeLine.positionCount = 2;
            RopeLine.enabled = false;
        }
    }

    private void Update()
    {
        // select highlight OR null if aiming on nothing
        Ray ray = new  Ray(Cam.transform.position, Cam.transform.forward);
        if (Physics.Raycast(ray, out _grappleHit, MaxGrappleDistance))
            _hittingGrappleStick = true;

        HighlightGrappleStick();

        UpdateRopeVisual();
    }

    private void FixedUpdate()
    {
        // keep the joint's connected anchor "pinned" in world space even if the stick moves slightly
        if (_joint)
            _joint.connectedAnchor = _grappleHitPoint;
    }

    private void StartGrapple()
    {
        if (_joint || !_hittingGrappleStick)
            return;
        
        if (!_grappleHit.collider.CompareTag("GrappleStick"))
            return;
        
        _grappleHitPoint = _grappleHit.point;
        
        ConfigureRope();
    }

    private void StopGrapple()
    {
        if (_joint) Destroy(_joint);
        _joint = null;
        _rb.linearDamping = 0f;
    }

    private void HighlightGrappleStick()
    {
        // new stick we just hit
        GrappleTarget newTarget = null;
        
        // did we hit a stick?
        if (_hittingGrappleStick)
            _grappleHit.collider.TryGetComponent(out newTarget);

        if (newTarget != _currentHighlight)
        {
            if (_currentHighlight) _currentHighlight.SetHighlighted();
            if (newTarget) newTarget.SetHighlighted(HighlightColor);
            _currentHighlight = newTarget;
        }
    }

    private void ConfigureRope()
    {
        // fixed rope length from current position to stick
        _ropeLength = Vector3.Distance(transform.position, _grappleHitPoint);
        
        // build a hard-length "rope" using ConfigurableJoint
        _joint = gameObject.AddComponent<ConfigurableJoint>();
        _joint.autoConfigureConnectedAnchor = false;
        _joint.connectedAnchor = _grappleHitPoint;
        
        // lock angular motion so the rope behaves like a pivot
        _joint.angularXMotion = ConfigurableJointMotion.Free;
        _joint.angularYMotion = ConfigurableJointMotion.Free;
        _joint.angularZMotion = ConfigurableJointMotion.Free;
        
        // linear limited motion (distance cannot exceed ropelength)
        _joint.xMotion = ConfigurableJointMotion.Limited;
        _joint.yMotion = ConfigurableJointMotion.Limited;
        _joint.zMotion = ConfigurableJointMotion.Limited;

        SoftJointLimit jointLimit = new SoftJointLimit()
            { limit = _ropeLength, bounciness = 0f, contactDistance = 0.01f };
        _joint.linearLimit = jointLimit;
        
        // zero spring makes it a rigid limit (no elastic pull)
        var spring = new SoftJointLimitSpring { spring = 0f, damper = 0f };
        _joint.linearLimitSpring = spring;
        
        // give a little solver stability
        _joint.enableCollision = false;
        _joint.breakForce = Mathf.Infinity;
        _joint.breakTorque = Mathf.Infinity;

        // slightly damp velocity perpendicular to rope to reduce jitter
        _rb.linearDamping = 0.05f;

        // start rope visual
        if (RopeLine) RopeLine.enabled = true;
    }

    private void UpdateRopeVisual()
    {
        if (_joint && RopeLine)
        {
            RopeLine.enabled = true;
            RopeLine.SetPosition(0, _grappleHitPoint);
            RopeLine.SetPosition(1, transform.position + Vector3.forward * 1f); // end at player center
        }
        else if (RopeLine)
        {
            RopeLine.enabled = false;
        }
    }

    private void OnDestroy()
    {
        // remove inputs
        _inputDirector.OnFireStarted -= StartGrapple;
        _inputDirector.OnFireReleased -= StopGrapple;
        
        // remove grapple leftovers
        if (_currentHighlight) _currentHighlight.SetHighlighted();
        if (_joint) Destroy(_joint);
        if (RopeLine) RopeLine.enabled = false;
    }
}
