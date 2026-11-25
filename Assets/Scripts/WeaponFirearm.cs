using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hitscan gun: pistols, rifles, shotguns... Stats come from a FirearmData asset.
///
/// Recoil: the camera kick is exposed through ConsumeRecoil(). The player's look script must call it
/// every frame and add the result to the view (x = degrees UP, y = degrees to the RIGHT).
/// </summary>
public class WeaponFirearm : Weapon
{
    [Header("Firearm")]
    public FirearmData data;
    [Tooltip("Animator triggers used by single shell reloads: one per shell inserted, and one when finished.")]
    public string reloadShellTrigger = "ReloadShell";
    public string reloadEndTrigger = "ReloadEnd";

    public override WeaponData BaseData => data;

    public float CurrentSpread { get; private set; }
    public int SpareMagazines => data == null || data.magazineSize <= 0
        ? 0 : Mathf.CeilToInt((float)ReserveAmmo / data.magazineSize);

    private const int MaxWallPasses = 8;

    private float nextFireTime;
    private bool isBursting;
    private float bloom;
    private float movementBlend; // 0 = stationary accuracy, 1 = moving accuracy
    private Coroutine reloadRoutine;

    private Vector2 pendingRecoil;      // kick not yet applied by the player's look script
    private Vector2 accumulatedRecoil;  // total kick since the last recovery, used to return the view
    private float lastShotTime;

    private bool isShotPending;
    private Coroutine delayedFireRoutine;

    private readonly HashSet<IDamageable> hitThisShot = new HashSet<IDamageable>();

    // ---------------------------------------------------------------- Lifecycle / ammo
    protected override void OnDisable()
    {
        base.OnDisable();
        CancelDelayedFire();
    }

    protected override void ResetState()
    {
        isBursting = false;
        reloadRoutine = null;
    }

    protected override void ResetAnimatorTriggers()
    {
        base.ResetAnimatorTriggers();
        if (animator == null) return;
        if (!string.IsNullOrEmpty(reloadShellTrigger)) animator.ResetTrigger(reloadShellTrigger);
        if (!string.IsNullOrEmpty(reloadEndTrigger)) animator.ResetTrigger(reloadEndTrigger);
    }

    public override void ResetAmmo()
    {
        if (data == null) return;
        AmmoInMagazine = data.magazineSize;
        ReserveAmmo = data.spareMagazines * data.magazineSize;
        RaiseAmmoChanged();
    }

    /// <summary>Adds full magazines of spare ammo. Returns true if any was added.</summary>
    public override bool AddAmmo(int magazines)
    {
        int max = data.maxSpareMagazines * data.magazineSize;
        if (ReserveAmmo >= max) return false;
        ReserveAmmo = Mathf.Min(max, ReserveAmmo + magazines * data.magazineSize);
        RaiseAmmoChanged();
        return true;
    }

    // ---------------------------------------------------------------- Per-frame
    protected override void Tick()
    {
        UpdateSpread();
        UpdateRecoil();

        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        if (mouse == null) return;

        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            TryReload();

        bool wantsFire = data.fireMode == FireMode.FullAuto
            ? mouse.leftButton.isPressed
            : mouse.leftButton.wasPressedThisFrame;

        if (IsReloading)
        {
            bool canInterrupt = data.reloadMode == ReloadMode.SingleShell
                                && data.canFireInterruptReload && AmmoInMagazine > 0;
            if (wantsFire && canInterrupt) CancelReload();
            else return;
        }

        if (IsBusy) return; // drawing

        if (wantsFire) TryFire();
    }

    // ---------------------------------------------------------------- Accuracy
    private void UpdateSpread()
    {
        // Accuracy gets worse and recovers at different speeds
        float blendTime = IsMoving ? data.spreadWorsenTime : data.spreadRecoverTime;
        float step = Time.deltaTime / Mathf.Max(0.01f, blendTime);
        movementBlend = Mathf.MoveTowards(movementBlend, IsMoving ? 1f : 0f, step);

        bloom = Mathf.Max(0f, bloom - data.bloomRecovery * Time.deltaTime);

        CurrentSpread = Mathf.Lerp(data.spreadStationary, data.spreadMoving, movementBlend) + bloom;
    }

    // ---------------------------------------------------------------- Recoil
    private void ApplyRecoil()
    {
        Vector2 kick = new Vector2(
            UnityEngine.Random.Range(data.recoilPitchMin, data.recoilPitchMax),
            UnityEngine.Random.Range(data.recoilYawMin, data.recoilYawMax));

        pendingRecoil += kick;
        accumulatedRecoil += kick;
        lastShotTime = Time.time;
    }

    private void UpdateRecoil()
    {
        if (accumulatedRecoil == Vector2.zero) return;
        if (Time.time - lastShotTime < data.recoilRecoveryDelay) return;

        // Bring the view back towards where it was before the burst of fire
        Vector2 next = Vector2.MoveTowards(accumulatedRecoil, Vector2.zero, data.recoilRecoverySpeed * Time.deltaTime);
        pendingRecoil += next - accumulatedRecoil;
        accumulatedRecoil = next;
    }

    /// <summary>
    /// Returns the camera movement (degrees) to apply this frame and clears it.
    /// x = pitch UP (positive looks up), y = yaw to the RIGHT. Call once per frame from the look script.
    /// </summary>
    public override Vector2 ConsumeRecoil()
    {
        Vector2 r = pendingRecoil;
        pendingRecoil = Vector2.zero;
        return r;
    }

    // ---------------------------------------------------------------- Firing
    private void TryFire()
    {
        if (isBursting || isShotPending || Time.time < nextFireTime) return;

        if (AmmoInMagazine <= 0)
        {
            PlaySound(data.emptySound);
            nextFireTime = Time.time + 0.25f;
            AutoReloadIfEmpty();
            return;
        }

        // Start the fire-rate cooldown when the trigger is accepted.
        nextFireTime = Time.time + data.SecondsBetweenShots;

        if (data.fireMode == FireMode.Burst)
        {
            StartCoroutine(BurstRoutine());
        }
        else
        {
            if (data.useFireDelay && data.fireDelay > 0f)
                delayedFireRoutine = StartCoroutine(DelayedFireRoutine());
            else
                FireShot();

            // Reload only after the shot has actually fired.
            if (!data.useFireDelay || data.fireDelay <= 0f)
                AutoReloadIfEmpty();
        }
    }

    private IEnumerator DelayedFireRoutine()
    {
        isShotPending = true;

        // Play the animation immediately, but delay the actual bullet.
        SetTrigger(fireTrigger);

        yield return new WaitForSeconds(data.fireDelay);

        if (isActiveAndEnabled && !IsReloading && !IsDrawing && AmmoInMagazine > 0)
        {
            FireShot();
            AutoReloadIfEmpty();
        }

        isShotPending = false;

        isShotPending = false;
        delayedFireRoutine = null;
    }

    private void CancelDelayedFire()
    {
        if (delayedFireRoutine != null)
        {
            StopCoroutine(delayedFireRoutine);
            delayedFireRoutine = null;
        }

        isShotPending = false;
    }

    private IEnumerator BurstRoutine()
    {
        isBursting = true;
        for (int i = 0; i < data.burstCount; i++)
        {
            FireShot();

            if (AmmoInMagazine <= 0) break;

            if (i < data.burstCount - 1)
                yield return new WaitForSeconds(data.SecondsBetweenShots);
        }
        nextFireTime = Time.time + data.burstCooldown;
        isBursting = false;

        AutoReloadIfEmpty();
    }

    private void AutoReloadIfEmpty()
    {
        if (data.autoReload && AmmoInMagazine <= 0) TryReload();
    }

    private void FireShot()
    {
        AmmoInMagazine--;

        for (int i = 0; i < Mathf.Max(1, data.pellets); i++)
            FireRay(CurrentSpread);

        // Every shot makes the next one less accurate (bloom) and kicks the view (recoil)
        bloom = Mathf.Min(data.maxBloom, bloom + data.bloomPerShot);
        ApplyRecoil();

        SetTrigger(fireTrigger);
        PlaySound(data.attackSound);
        RaiseFired();
        RaiseAmmoChanged();
    }

    private void FireRay(float spreadDegrees)
    {
        Vector3 origin = aimOrigin.position;
        Vector3 dir = ApplySpread(aimOrigin.rotation, spreadDegrees);

        if (RunsOnServer)
            ServerFire(origin, dir);      // the host applies it directly
        else
            FireServerRpc(origin, dir);   // a client asks the host
    }

    [Rpc(SendTo.Server)]
    private void FireServerRpc(Vector3 origin, Vector3 dir)
    {
        ServerFire(origin, dir);
    }

    /// <summary>
    /// Server side of one bullet. Walks along the ray hit by hit: damages targets (losing some damage
    /// for each one passed), and tries to go through walls using the data's penetration settings.
    /// </summary>
    private void ServerFire(Vector3 origin, Vector3 dir)
    {
        hitThisShot.Clear();
        float travelled = 0f;      // distance from the muzzle so far
        float damageScale = 1f;    // reduced by every wall / target passed
        int targetsHit = 0;

        // Each pass is one ray. After passing a wall we start a new ray from the far side of it.
        for (int pass = 0; pass < MaxWallPasses; pass++)
        {
            float remaining = data.range - travelled;
            if (remaining <= 0f || damageScale <= 0f) return;

            int count = Physics.RaycastNonAlloc(origin, dir, hitBuffer, remaining, HitMaskValue, QueryTriggerInteraction.Ignore);
            Array.Sort(hitBuffer, 0, count, DistanceComparer);

            bool passedWall = false;

            for (int i = 0; i < count && !passedWall; i++)
            {
                RaycastHit hit = hitBuffer[i];
                if (IsIgnored(hit.collider)) continue;

                float distance = travelled + hit.distance;
                IDamageable target = FindDamageable(hit.collider);

                if (target != null)
                {
                    // A target has many colliders (bones): only the first one the bullet touches counts
                    if (!hitThisShot.Add(target)) continue;

                    float falloff = ComputeFalloff(distance, data.range, data.falloffStartDistance, data.damageAtMaxRange);
                    DealDamage(target, hit, dir, data.damage * falloff * damageScale, DamageType.Bullet);
                    RaiseHit(hit, true);

                    if (targetsHit >= data.maxTargetPenetrations) return; // bullet stops here
                    targetsHit++;
                    damageScale *= data.targetDamageRetained;
                    continue;
                }

                // Solid, non-damageable surface
                RaiseHit(hit, false);

                if (!TryPassWall(hit, dir, out Vector3 exitPoint, out float thickness)) return;

                damageScale *= data.wallDamageRetained;
                travelled = distance + thickness + 0.01f;
                origin = exitPoint + dir * 0.01f;
                passedWall = true;
            }

            if (!passedWall) return; // nothing left to hit
        }
    }

    /// <summary>
    /// Checks whether the bullet can go through the wall that was hit, and where it comes out.
    /// Works by casting back from beyond the wall at the same collider to find the far surface.
    /// </summary>
    private bool TryPassWall(RaycastHit hit, Vector3 dir, out Vector3 exitPoint, out float thickness)
    {
        exitPoint = hit.point;
        thickness = 0f;

        if (data.maxWallThickness <= 0f) return false;
        if ((data.penetrableLayers.value & (1 << hit.collider.gameObject.layer)) == 0) return false;

        Vector3 far = hit.point + dir * data.maxWallThickness;
        Ray back = new Ray(far, -dir);

        // No hit = the far side is further than maxWallThickness, so the wall is too thick
        if (!hit.collider.Raycast(back, out RaycastHit exit, data.maxWallThickness)) return false;

        exitPoint = exit.point;
        thickness = Vector3.Distance(hit.point, exit.point);
        return true;
    }

    // ---------------------------------------------------------------- Reload
    public bool TryReload()
    {
        if (IsBusy || isBursting) return false;

        if (AmmoInMagazine >= data.magazineSize || ReserveAmmo <= 0) return false;

        reloadRoutine = StartCoroutine(data.reloadMode == ReloadMode.SingleShell
            ? ShellReloadRoutine()
            : MagazineReloadRoutine());
        return true;
    }

    private IEnumerator MagazineReloadRoutine()
    {
        IsReloading = true;
        PlaySound(data.reloadSound);
        RaiseReloadStarted();

        ResetAnimatorTriggers();
        SetTrigger(reloadTrigger);
        // With an Animator the animation decides the duration; otherwise reloadTime is used
        yield return WaitForAnimation(reloadTag, data.reloadTime);

        int needed = data.magazineSize - AmmoInMagazine;
        int taken = Mathf.Min(needed, ReserveAmmo);
        AmmoInMagazine += taken;
        ReserveAmmo -= taken;

        IsReloading = false;
        reloadRoutine = null;
        RaiseReloadFinished();
        RaiseAmmoChanged();
    }

    private IEnumerator ShellReloadRoutine()
    {
        IsReloading = true;
        PlaySound(data.reloadSound);
        RaiseReloadStarted();

        ResetAnimatorTriggers();
        SetTrigger(reloadTrigger);
        yield return new WaitForSeconds(data.shellReloadStartTime);

        // One shell at a time, until full or out of spare ammo (or cancelled by firing)
        while (AmmoInMagazine < data.magazineSize && ReserveAmmo > 0)
        {
            SetTrigger(reloadShellTrigger);
            yield return new WaitForSeconds(data.shellInsertTime);

            AmmoInMagazine++;
            ReserveAmmo--;
            RaiseAmmoChanged();
        }

        SetTrigger(reloadEndTrigger);
        yield return new WaitForSeconds(data.shellReloadEndTime);

        IsReloading = false;
        reloadRoutine = null;
        RaiseReloadFinished();
    }

    /// <summary>Stops a single shell reload early (the shells already inserted stay loaded).</summary>
    private void CancelReload()
    {
        if (reloadRoutine != null) StopCoroutine(reloadRoutine);
        reloadRoutine = null;
        IsReloading = false;
        SetTrigger(reloadEndTrigger);
        RaiseReloadFinished();
        RaiseAmmoChanged();
    }
}