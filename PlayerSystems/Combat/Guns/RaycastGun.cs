using UnityEngine;

public class RaycastGun : GunBaseExtension
{
    [Header("Raycast Gun Settings")]
    [SerializeField] private float maxRange = 100f;
    [SerializeField] private LayerMask hitLayers;
    private AnimationManager _animationManager;

    protected override void Start()
    {
        base.Start();
        _animationManager = AnimationManager.Instance;
    }   

    protected override void PerformShoot()
    {
        if (cam == null)
        {
            Debug.LogWarning("RaycastGun: Missing camera reference.");
            return;
        }

        if (_animationManager != null)
        {
            _animationManager.SetAnimatorValue("Shoot");
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
