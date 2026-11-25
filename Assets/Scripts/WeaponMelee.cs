using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Melee weapon (knife, bat, machete...). Stats come from a MeleeData asset.
///
/// A swing fires a ray every 'rayInterval' seconds, sweeping from the start angle to the end angle of the arc
/// relative to where the camera is looking. A target caught by several rays is only damaged once per swing.
/// </summary>
public class WeaponMelee : Weapon
{
    [Header("Melee")]
    public MeleeData data;

    public override WeaponData BaseData => data;

    private float nextAttackTime;
    private bool isSwinging;
    private int swingCounter;

    // Server-side bookkeeping so one swing only damages each target once
    private int serverSwingId = -1;
    private readonly HashSet<IDamageable> serverHitThisSwing = new HashSet<IDamageable>();

    protected override void ResetState()
    {
        isSwinging = false;
    }

    public override string GetAmmoText() => ""; // melee has no ammo

    protected override void Tick()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        bool wantsAttack = data.holdToAttack
            ? mouse.leftButton.isPressed
            : mouse.leftButton.wasPressedThisFrame;

        if (IsBusy || !wantsAttack) return;
        if (isSwinging || Time.time < nextAttackTime) return;

        StartCoroutine(SwingRoutine());
    }

    private IEnumerator SwingRoutine()
    {
        isSwinging = true;
        int swingId = ++swingCounter;

        SetTrigger(fireTrigger);
        PlaySound(data.attackSound);
        RaiseFired();

        if (data.hitDelay > 0f)
            yield return new WaitForSeconds(data.hitDelay);

        float interval = Mathf.Max(0.01f, data.rayInterval);
        float elapsed = 0f;

        while (true)
        {
            float t = data.swingDuration > 0f ? Mathf.Clamp01(elapsed / data.swingDuration) : 1f;
            float yaw = Mathf.Lerp(data.arcStartYaw, data.arcEndYaw, t);
            float pitch = Mathf.Lerp(data.arcStartPitch, data.arcEndPitch, t); // positive = down

            // Uses the camera's current rotation, so the arc follows where you're looking
            Vector3 dir = aimOrigin.rotation * Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
            Vector3 origin = aimOrigin.position;
            Debug.DrawRay(origin, dir * data.range, Color.cyan, 0.5f);

            if (RunsOnServer) ServerSwingRay(origin, dir, swingId);
            else SwingRayServerRpc(origin, dir, swingId);

            if (elapsed >= data.swingDuration) break;

            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        nextAttackTime = Time.time + data.cooldown;
        isSwinging = false;
    }

    [Rpc(SendTo.Server)]
    private void SwingRayServerRpc(Vector3 origin, Vector3 dir, int swingId)
    {
        ServerSwingRay(origin, dir, swingId);
    }

    private void ServerSwingRay(Vector3 origin, Vector3 dir, int swingId)
    {
        // A new swing starts a fresh list of targets already hit
        if (swingId != serverSwingId)
        {
            serverSwingId = swingId;
            serverHitThisSwing.Clear();
        }

        if (data.maxTargetsPerSwing > 0 && serverHitThisSwing.Count >= data.maxTargetsPerSwing) return;

        if (!TryGetHit(origin, dir, data.range, data.rayRadius, out RaycastHit hit)) return;

        IDamageable target = FindDamageable(hit.collider);
        if (target == null)
        {
            RaiseHit(hit, false);
            return;
        }

        if (!serverHitThisSwing.Add(target)) return; // already damaged by an earlier ray of this swing

        DealDamage(target, hit, dir, data.damage, DamageType.Melee);
        RaiseHit(hit, true);
    }
}