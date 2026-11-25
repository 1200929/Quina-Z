using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Launches a physical projectile: bow, spear, grenade... Stats come from a ProjectileData asset.
/// The projectile itself (flight, sticking, bouncing, explosion) is handled by the Projectile script
/// on the prefab assigned in the data.
///
/// ReserveAmmo is the number of items you have left (arrows, grenades, spears).
/// </summary>
public class WeaponProjectile : Weapon
{
    [Header("Projectile weapon")]
    public ProjectileData data;
    [Tooltip("Optional. Where the projectile spawns (a hand, the bow...). Falls back to the camera.")]
    public Transform launchPoint;

    public override WeaponData BaseData => data;

    private float nextFireTime;
    private bool isThrowing;

    protected override void ResetState()
    {
        isThrowing = false;
    }

    public override void ResetAmmo()
    {
        if (data == null) return;
        AmmoInMagazine = 0;
        ReserveAmmo = data.startingAmmo;
        RaiseAmmoChanged();
    }

    /// <summary>Adds items (arrows, grenades...). Returns true if any was added.</summary>
    public override bool AddAmmo(int amount)
    {
        if (ReserveAmmo >= data.maxAmmo) return false;
        ReserveAmmo = Mathf.Min(data.maxAmmo, ReserveAmmo + amount);
        RaiseAmmoChanged();
        return true;
    }

    public override string GetAmmoText() => ReserveAmmo.ToString();

    protected override void Tick()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (IsBusy || isThrowing) return;
        if (!mouse.leftButton.wasPressedThisFrame) return;
        if (Time.time < nextFireTime || ReserveAmmo <= 0) return;

        StartCoroutine(ThrowRoutine());
    }

    private IEnumerator ThrowRoutine()
    {
        isThrowing = true;
        nextFireTime = Time.time + data.cooldown;

        SetTrigger(fireTrigger);
        PlaySound(data.attackSound);

        // Wait for the release moment of the animation
        if (data.releaseDelay > 0f)
            yield return new WaitForSeconds(data.releaseDelay);

        if (ReserveAmmo > 0)
        {
            ReserveAmmo--;
            Launch();
            RaiseFired();
            RaiseAmmoChanged();
        }

        isThrowing = false;
    }

    private void Launch()
    {
        // Angle upwards a bit (grenades), then add spread
        Quaternion rotation = aimOrigin.rotation * Quaternion.Euler(-data.launchUpAngle, 0f, 0f);
        Vector3 dir = ApplySpread(rotation, data.spread);
        Vector3 origin = launchPoint != null ? launchPoint.position : aimOrigin.position;

        if (RunsOnServer)
            ServerLaunch(origin, dir);
        else
            LaunchServerRpc(origin, dir);
    }

    [Rpc(SendTo.Server)]
    private void LaunchServerRpc(Vector3 origin, Vector3 dir)
    {
        ServerLaunch(origin, dir);
    }

    private void ServerLaunch(Vector3 origin, Vector3 dir)
    {
        if (data.projectilePrefab == null)
        {
            Debug.LogWarning($"{name}: ProjectileData '{data.name}' has no projectile prefab.", this);
            return;
        }

        GameObject go = Instantiate(data.projectilePrefab, origin, Quaternion.LookRotation(dir));

        Projectile projectile = go.GetComponent<Projectile>();
        if (projectile == null)
        {
            Debug.LogError($"Projectile prefab '{data.projectilePrefab.name}' has no Projectile script.", go);
            Destroy(go);
            return;
        }

        projectile.Launch(data, dir * data.projectileSpeed, ignoreRoot, this, HitMaskValue);

        // In multiplayer the projectile is spawned over the network so everyone sees it
        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.IsListening && manager.IsServer)
        {
            if (go.TryGetComponent(out NetworkObject networkObject))
                networkObject.Spawn(true);
            else
                Debug.LogWarning($"Projectile prefab '{data.projectilePrefab.name}' has no NetworkObject, " +
                                 "so other players won't see it.", go);
        }
    }
}