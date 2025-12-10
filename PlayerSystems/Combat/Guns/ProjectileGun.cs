using UnityEngine;

public class ProjectileGun : GunBaseExtension
{
    [Header("Projectile Gun Settings")]
    // [SerializeField] private HS_ProjectileMover bulletPrefab;
    [SerializeField] private float bulletSpeed = 30f;

    [SerializeField] private Transform bulletSpawnPosition;

    protected override void PerformShoot()
    {
        // Early-exit checks
        // if (bulletPrefab == null || cam == null || bulletSpawnPosition == null)
        // {
        //     Debug.LogWarning("ProjectileGun: Missing bullet prefab, camera, or bulletSpawnPosition reference.");
        //     return;
        // }

        // 1. Raycast from the camera to see where we want the bullet to face
        Ray ray = new(cam.position, cam.forward);
        RaycastHit hit;

        // We'll default the direction to cam.forward if the ray hits nothing
        Vector3 targetDirection = cam.forward;

        if (Physics.Raycast(ray, out hit, 1000f))
        {
            // If we hit something, compute the direction from the spawn position to the hit point
            targetDirection = (hit.point - bulletSpawnPosition.position).normalized;
        }

        // 2. Determine the rotation so the bullet faces the target direction
        Quaternion spawnRot = Quaternion.LookRotation(targetDirection);

        // // 3. Instantiate the bullet at the bulletSpawnPosition with the calculated rotation
        // HS_ProjectileMover bulletInstance = Instantiate(
        //     bulletPrefab,
        //     bulletSpawnPosition.position,
        //     spawnRot
        // );

        // // 4. Give the bullet a velocity in the targetDirection
        // Rigidbody rb = bulletInstance.GetComponent<Rigidbody>();
        // if (rb != null)
        // {
        //     rb.linearVelocity = targetDirection * bulletSpeed;
        // }
        //
        // // 5. Subscribe to bullet collision event
        // bulletInstance.OnProjectileHit += OnBulletHit;
    }

    // private void OnBulletHit(Collision collision, HS_ProjectileMover projectile)
    // {
    //     // Unsubscribe to avoid memory leaks
    //     projectile.OnProjectileHit -= OnBulletHit;
    //
    //     // Check if the object we hit is IDamageable, then apply damage
    //     IDamageable damageable = collision.gameObject.GetComponent<IDamageable>();
    //     if (damageable != null)
    //     {
    //         damageable.TakeDamage(damage);
    //     }
    // }
}
