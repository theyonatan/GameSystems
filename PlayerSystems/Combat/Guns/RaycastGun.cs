using UnityEngine;

public class RaycastGun : GunBaseExtension
{
    [Header("Raycast Gun Settings")]
    [SerializeField] private float maxRange = 100f;
    [SerializeField] private LayerMask hitLayers;
    private AnimationsManager _animationsManager;

    protected override void Start()
    {
        base.Start();
        _animationsManager = GetComponent<AnimationsManager>();
    }   

    protected override void PerformShoot()
    {
        if (cam == null)
        {
            Debug.LogWarning("RaycastGun: Missing camera reference.");
            return;
        }

        if (_animationsManager != null)
        {
            _animationsManager.Play("Shoot");
        }

        Ray ray = new(cam.position, cam.forward);
        
        if (Physics.Raycast(ray, out var hit, maxRange, hitLayers))
        {
            // We hit something: check for damageable
            IDamageable damageable = hit.collider.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage);
            }
        }
    }
}
