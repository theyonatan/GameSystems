using UnityEngine;

public abstract class GunBaseExtension : MonoBehaviour
{
    [Header("Gun Stats")]
    [SerializeField] protected float damage = 10f;
    [SerializeField] protected float maxCooldown = 0.5f;

    protected float cooldownTimer;
    protected Transform cam;
    protected InputDirector _director;

    protected virtual void Start()
    {
        // Find main camera by tag
        if (Camera.main != null) cam = Camera.main.transform;

        _director = GetComponent<InputDirector>();
        if (_director != null)
        {
            _director.OnFirePressed += OnFirePressed;
        }

        // Reset cooldown
        cooldownTimer = 0f;
    }

    protected virtual void Update()
    {
        // Decrement cooldown each frame
        if (cooldownTimer > 0)
        {
            cooldownTimer -= Time.deltaTime;
        }
    }

    private void OnFirePressed()
    {
        // If there's still cooldown, don't shoot
        if (cooldownTimer > 0) return;

        PerformShoot();

        // Reset cooldown
        cooldownTimer = maxCooldown;
    }

    /// <summary>
    /// Child classes must implement how to actually shoot (projectile vs. raycast).
    /// </summary>
    protected abstract void PerformShoot();
}
