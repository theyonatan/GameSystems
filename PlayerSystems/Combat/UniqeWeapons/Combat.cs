using System;
using UnityEngine;
using DG.Tweening;
using UnityEngine.VFX;

public class Combat : MonoBehaviour
{
    // melee:
    // check for hit
    // hit? nice get component as SnowEnemy
    // gameObject.enemy.playerHit(hp);

    InputDirector director;
    [SerializeField] Weapon equippedWeapon;
    [SerializeField] GameObject weaponSensor;
    [SerializeField] VisualEffect flamethrowerEffect;

    [SerializeField] float weaponRange = 3f;
    [SerializeField] float weaponCooldown = 0.1f;
    [SerializeField] float flamethrowerDamage = 1f;
    private float _weaponCooldown = 0.1f;
    private bool isFlameThrowering;
    private int combatLayerMask = 0;

    [SerializeField] Transform camPosition;
    [SerializeField] Transform weaponTransform;
    [SerializeField] Transform idlePosition;
    [SerializeField] Transform focusedPosition;

    [SerializeField] float transitionDuration = 0.3f;

    private Tween currentTween;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        director = GetComponent<InputDirector>();
        director.OnCombatPressed += OnCombatMeleePressed;
        director.OnPlayerFlameThrowerStart += Director_OnPlayerFlameThrowerStart;
        director.OnPlayerFlameThrowerStop += Director_OnPlayerFlameThrowerStop;

        weaponTransform.localPosition = idlePosition.localPosition;
        weaponTransform.localRotation = idlePosition.localRotation;
        weaponTransform.gameObject.SetActive(false);

        _weaponCooldown = weaponCooldown;

        combatLayerMask = LayerMask.GetMask("Characters");
    }

    private void OnDestroy()
    {
        director.OnCombatPressed -= OnCombatMeleePressed;
        director.OnPlayerFlameThrowerStart -= Director_OnPlayerFlameThrowerStart;
        director.OnPlayerFlameThrowerStop -= Director_OnPlayerFlameThrowerStop;
    }

    private void OnCombatMeleePressed()
    {
        Debug.Log("Combatting");

        Ray ray = new(camPosition.position, camPosition.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 2f, combatLayerMask))
        {
            if (hit.collider.CompareTag("Enemy"))
            {
                if (hit.collider.TryGetComponent(out Enemy enemy))
                    enemy.FistHit(camPosition.position);
            }
        }
    }

    private void Director_OnPlayerFlameThrowerStart()
    {
        weaponTransform.gameObject.SetActive(true);
        isFlameThrowering = true;
        MoveAndRotateWeapon(focusedPosition.localPosition, focusedPosition.localRotation.eulerAngles);
    }

    private void OnPlayerFlameThrowerContinue()
    {
        Debug.Log("That's a fucking FlameThrower!!!");

        // reset cooldown
        if (_weaponCooldown <= 0f)
            _weaponCooldown = weaponCooldown;
        else
            return;

        // actual weapon hit check
        Ray ray = new(weaponSensor.transform.position, weaponSensor.transform.forward);

        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, weaponRange, combatLayerMask))
        {
            if (!hit.collider.CompareTag("Enemy"))
                return;

            Debug.Log("Hit an Enemy!");

            Enemy enemy = hit.collider.GetComponent<Enemy>();

            if (enemy != null)
                enemy.TakeDamage(flamethrowerDamage);
            else
                Debug.Log("WTF Enemy has no Enemy component!");
        }
    }

    private void Director_OnPlayerFlameThrowerStop()
    {
        isFlameThrowering = false;
        MoveAndRotateWeapon(idlePosition.localPosition, idlePosition.localRotation.eulerAngles, false);
    }
    
    public void MoveAndRotateWeapon(Vector3 targetPosition, Vector3 targetRotation, bool startTweek=true)
    {
        // kill any running tweens
        currentTween?.Kill();

        // move the weapon
        currentTween = DOTween.Sequence()
            .Append(weaponTransform.DOLocalMove(targetPosition, transitionDuration).SetEase(Ease.OutQuad))
            .Join(weaponTransform.DOLocalRotate(targetRotation, transitionDuration).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                if (startTweek)
                {
                    flamethrowerEffect.Play();
                }
                else
                {
                    weaponTransform.gameObject.SetActive(false);
                    flamethrowerEffect.Stop();
                }
            }));
    }

    public void EquipWeapon(Weapon newWeapon)
    {
        equippedWeapon = newWeapon;
    }

    private void Update()
    {
        _weaponCooldown -= Time.deltaTime;

        if (isFlameThrowering)
            OnPlayerFlameThrowerContinue();
    }
}

public class Weapon
{
    public int Damage;
}
