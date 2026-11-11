using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shared weapon logic for every gun and melee weapon. Stats come from a WeaponData asset.
/// Fully self-contained: it only depends on WeaponData and IDamageable.
///
/// Hooks for other systems (player, zombies, HUD) are exposed as public properties/events:
///   - Player movement: set weapon.IsMoving every frame, read weapon.MovementSpeedMultiplier.
///   - Targets: implement IDamageable on anything that can be shot.
///   - HUD/FX: subscribe to the On... events.
/// </summary>
public class Weapon : MonoBehaviour
{
    [Header("References")]
    public WeaponData data;
    [Tooltip("Where rays start/aim from. Assign the player camera (falls back to Camera.main).")]
    public Transform aimOrigin;
    [Tooltip("Optional. Colliders under this transform are ignored by shots (assign the player root).")]
    public Transform ignoreRoot;
    [Tooltip("Optional. Used for fire/reload sounds.")]
    public AudioSource audioSource;
    [Tooltip("Layers the weapon can hit.")]
    public LayerMask hitMask = ~0;

    // ---- Set by the player later ----
    /// <summary>Set this from your movement script. Moving uses the "moving" spread.</summary>
    public bool IsMoving { get; set; }
    /// <summary>Read this from your movement script to slow the player with heavier weapons.</summary>
    public float MovementSpeedMultiplier => data != null ? data.movementSpeedMultiplier : 1f;

    // ---- Runtime state (read these from your HUD) ----
    public int AmmoInMagazine { get; private set; }
    public int ReserveAmmo { get; private set; }
    public bool IsReloading { get; private set; }
    public float CurrentSpread { get; private set; }
    public int SpareMagazines => data == null || data.magazineSize <= 0
        ? 0 : Mathf.CeilToInt((float)ReserveAmmo / data.magazineSize);

    public event Action OnAmmoChanged;
    public event Action OnFired;
    public event Action OnReloadStarted;
    public event Action OnReloadFinished;
    /// <summary>Hit info, and whether the thing hit was an IDamageable.</summary>
    public event Action<RaycastHit, bool> OnHit;

    private float nextFireTime;
    private bool isBursting;
    private float bloom;
    private float movementBlend; // 0 = stationary accuracy, 1 = moving accuracy

    private readonly RaycastHit[] hitBuffer = new RaycastHit[32];
    private static readonly IComparer<RaycastHit> DistanceComparer =
        Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

    private void Awake()
    {
        if (aimOrigin == null && Camera.main != null) aimOrigin = Camera.main.transform;
        ResetAmmo();
    }

    private void OnEnable()
    {
        IsReloading = false;
        isBursting = false;
    }

    private void OnDisable()
    {
        // Unequipping cancels reload/burst
        StopAllCoroutines();
        IsReloading = false;
        isBursting = false;
    }

    public void ResetAmmo()
    {
        if (data == null) return;
        AmmoInMagazine = data.magazineSize;
        ReserveAmmo = data.spareMagazines * data.magazineSize;
        OnAmmoChanged?.Invoke();
    }

    /// <summary>Call from ammo pickups / wall buys. Returns true if any ammo was added.</summary>
    public bool AddAmmo(int magazines)
    {
        int max = data.maxSpareMagazines * data.magazineSize;
        if (ReserveAmmo >= max) return false;
        ReserveAmmo = Mathf.Min(max, ReserveAmmo + magazines * data.magazineSize);
        OnAmmoChanged?.Invoke();
        return true;
    }

    private void Update()
    {
        if (data == null || aimOrigin == null) return;

        UpdateSpread();

        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        if (mouse == null) return;

        if (data.weaponType == WeaponType.Ranged && keyboard != null && keyboard.rKey.wasPressedThisFrame)
            TryReload();

        if (IsReloading) return;

        bool wantsFire = data.fireMode == FireMode.FullAuto
            ? mouse.leftButton.isPressed
            : mouse.leftButton.wasPressedThisFrame;

        if (wantsFire) TryFire();
    }

    // ---------------------------------------------------------------- Accuracy
    private void UpdateSpread()
    {
        float step = Time.deltaTime / Mathf.Max(0.01f, data.accuracyTransitionTime);
        movementBlend = Mathf.MoveTowards(movementBlend, IsMoving ? 1f : 0f, step);

        bloom = Mathf.Max(0f, bloom - data.bloomRecovery * Time.deltaTime);

        CurrentSpread = Mathf.Lerp(data.spreadStationary, data.spreadMoving, movementBlend) + bloom;
    }

    // ---------------------------------------------------------------- Firing
    private void TryFire()
    {
        if (isBursting || Time.time < nextFireTime) return;

        if (data.weaponType == WeaponType.Melee)
        {
            nextFireTime = Time.time + data.SecondsBetweenShots;
            Melee();
            return;
        }

        if (AmmoInMagazine <= 0)
        {
            PlaySound(data.emptySound);
            nextFireTime = Time.time + 0.25f;
            TryReload(); // auto reload when empty
            return;
        }

        nextFireTime = Time.time + data.SecondsBetweenShots;

        if (data.fireMode == FireMode.Burst)
            StartCoroutine(BurstRoutine());
        else
            FireShot();
    }

    private IEnumerator BurstRoutine()
    {
        isBursting = true;
        for (int i = 0; i < data.burstCount; i++)
        {
            if (AmmoInMagazine <= 0) break;
            FireShot();
            if (i < data.burstCount - 1)
                yield return new WaitForSeconds(data.SecondsBetweenShots);
        }
        nextFireTime = Time.time + data.burstCooldown;
        isBursting = false;
    }

    private void FireShot()
    {
        AmmoInMagazine--;

        for (int i = 0; i < Mathf.Max(1, data.pellets); i++)
            FireRay(CurrentSpread);

        // Bloom: every shot makes the next one less accurate
        bloom = Mathf.Min(data.maxBloom, bloom + data.bloomPerShot);

        PlaySound(data.fireSound);
        OnFired?.Invoke();
        OnAmmoChanged?.Invoke();
    }

    private void FireRay(float spreadDegrees)
    {
        Vector3 origin = aimOrigin.position;
        Vector3 dir = ApplySpread(aimOrigin, spreadDegrees);

        if (TryGetHit(origin, dir, data.range, 0f, out RaycastHit hit))
        {
            Debug.DrawLine(origin, hit.point, Color.red, 0.5f);
            ApplyHit(hit, dir);
        }
        else
        {
            Debug.DrawRay(origin, dir * data.range, Color.yellow, 0.5f);
        }
    }

    private static Vector3 ApplySpread(Transform t, float degrees)
    {
        if (degrees <= 0f) return t.forward;
        Vector2 r = UnityEngine.Random.insideUnitCircle * Mathf.Tan(degrees * Mathf.Deg2Rad);
        return (t.forward + t.right * r.x + t.up * r.y).normalized;
    }

    private void Melee()
    {
        Vector3 origin = aimOrigin.position;
        Vector3 dir = aimOrigin.forward;

        if (TryGetHit(origin, dir, data.range, data.meleeRadius, out RaycastHit hit))
            ApplyHit(hit, dir);

        PlaySound(data.fireSound);
        OnFired?.Invoke();
    }

    /// <summary>
    /// Ray (or sphere cast for melee) that returns the closest hit that isn't ignored.
    /// </summary>
    private bool TryGetHit(Vector3 origin, Vector3 dir, float range, float radius, out RaycastHit best)
    {
        int count = radius > 0f
            ? Physics.SphereCastNonAlloc(origin, radius, dir, hitBuffer, range, hitMask, QueryTriggerInteraction.Ignore)
            : Physics.RaycastNonAlloc(origin, dir, hitBuffer, range, hitMask, QueryTriggerInteraction.Ignore);

        Array.Sort(hitBuffer, 0, count, DistanceComparer);

        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (ignoreRoot != null && h.collider.transform.IsChildOf(ignoreRoot)) continue; // never hit ourselves
            if (h.distance <= 0f && radius > 0f) continue; // sphere started inside something
            best = h;
            return true;
        }

        best = default;
        return false;
    }

    private void ApplyHit(RaycastHit hit, Vector3 dir)
    {
        //Debug.Log("Weapon raycast struck: " + hit.collider.name);

        IDamageable target = hit.collider.GetComponentInParent<IDamageable>();

        if (target != null)
        {
            float falloff = 1f;
            if (hit.distance > data.falloffStartDistance && data.range > data.falloffStartDistance)
            {
                float t = (hit.distance - data.falloffStartDistance) / (data.range - data.falloffStartDistance);
                falloff = Mathf.Lerp(1f, data.damageAtMaxRange, t);
            }

            target.TakeDamage(new DamageInfo
            {
                damage = data.damage * falloff,
                point = hit.point,
                direction = dir,
                force = dir * data.impactForce,
                collider = hit.collider,
                source = this
            });
        }

        OnHit?.Invoke(hit, target != null); // hook up hit markers / impact effects / blood here
    }

    // ---------------------------------------------------------------- Reload
    public bool TryReload()
    {
        if (data.weaponType != WeaponType.Ranged) return false;
        if (IsReloading || isBursting) return false;
        if (AmmoInMagazine >= data.magazineSize || ReserveAmmo <= 0) return false;

        StartCoroutine(ReloadRoutine());
        return true;
    }

    private IEnumerator ReloadRoutine()
    {
        IsReloading = true;
        PlaySound(data.reloadSound);
        OnReloadStarted?.Invoke();

        yield return new WaitForSeconds(data.reloadTime);

        int needed = data.magazineSize - AmmoInMagazine;
        int taken = Mathf.Min(needed, ReserveAmmo);
        AmmoInMagazine += taken;
        ReserveAmmo -= taken;

        IsReloading = false;
        OnReloadFinished?.Invoke();
        OnAmmoChanged?.Invoke();
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }
}