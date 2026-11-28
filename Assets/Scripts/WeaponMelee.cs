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
    private bool serverWallSoundPlayed;
    private bool serverSwingStopped;
    private int stoppedSwingId = -1; // on the attacking player: the swing that was cut short
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
            if (stoppedSwingId == swingId) break; // something was hit, stop sending rays
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
            serverWallSoundPlayed = false;
            serverSwingStopped = false;
        }

        if (serverSwingStopped) return; // an earlier ray already ended this swing

        if (data.maxTargetsPerSwing > 0 && serverHitThisSwing.Count >= data.maxTargetsPerSwing) return;

        // The thick ray skips anything it starts inside of (e.g. standing right against a wall),
        // which let swings reach zombies behind it. A thin ray finds the wall reliably.
        bool thinFound = TryGetHit(origin, dir, data.range, 0f, out RaycastHit thin);
        bool wallInTheWay = thinFound && FindDamageable(thin.collider) == null;

        bool found = TryGetHit(origin, dir, data.range, data.rayRadius, out RaycastHit hit);

        if (wallInTheWay && (!found || (FindDamageable(hit.collider) != null && hit.distance > thin.distance)))
        {
            hit = thin;   // the wall is closer than the target, so the target is behind it
            found = true;
        }

        if (!found) return;

        IDamageable target = FindDamageable(hit.collider);
        if (target == null)
        {
            if (!serverWallSoundPlayed)
            {
                serverWallSoundPlayed = true;
                PlayHitSound(false, hit.point);
            }
            RaiseHit(hit, false);
            if (data.stopSwingOnHit) StopSwing(swingId);
            return;
        }

        if (!serverHitThisSwing.Add(target)) return; // already damaged by an earlier ray of this swing

        DealDamage(target, hit, dir, data.damage, DamageType.Melee);
        PlayHitSound(true, hit.point);
        RaiseHit(hit, true);
        if (data.stopSwingOnHit) StopSwing(swingId);
    }

    // ---------------------------------------------------------------- Stop swing on hit
    private void StopSwing(int swingId)
    {
        serverSwingStopped = true;
        if (IsSpawned) StopSwingRpc(swingId);
        else OnSwingStopped(swingId);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void StopSwingRpc(int swingId)
    {
        OnSwingStopped(swingId);
    }

    private void OnSwingStopped(int swingId)
    {
        stoppedSwingId = swingId;                       // the attacking player's swing loop sees this and ends
        if (audioSource != null) audioSource.Stop();    // cuts the swing sound; hit sounds use their own source
    }

    // ---------------------------------------------------------------- Hit sounds
    private void PlayHitSound(bool enemy, Vector3 point)
    {
        if (IsSpawned) HitSoundRpc(enemy, point);
        else PlayHitSoundLocal(enemy, point);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void HitSoundRpc(bool enemy, Vector3 point)
    {
        PlayHitSoundLocal(enemy, point);
    }

    private void PlayHitSoundLocal(bool enemy, Vector3 point)
    {
        AudioClip clip = enemy ? data.hitEnemySound : data.hitWallSound;
        if (clip != null) AudioSource.PlayClipAtPoint(clip, point);
    }
}