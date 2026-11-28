using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Put this on the projectile prefab (spear, arrow, grenade model). Motion is simulated on the server only:
/// it sweeps a small sphere along the flight path so fast projectiles never skip through walls.
///
/// Multiplayer prefab setup: add a NetworkObject and a NetworkTransform (server authoritative),
/// and register the prefab in the NetworkManager's prefab list.
/// Without networking it just works as a normal object.
/// </summary>
public class Projectile : NetworkBehaviour
{
    [Header("Effects (optional)")]
    [Tooltip("Spawned on every client when the projectile explodes (particles + sound).")]
    public GameObject explosionEffect;
    public float explosionEffectLifetime = 5f;

    [Header("Debug")]
    [Tooltip("Draw the flight path (cyan), impact points (yellow) and the explosion (red sphere, green lines to targets hit, " +
             "gray lines to targets shielded by a wall). Drawn on the host. Visible in the Scene view, " +
             "and in the Game view with Gizmos on.")]
    public bool drawDebug = true;
    [Tooltip("Seconds the debug lines stay on screen.")]
    public float debugDuration = 5f;

    private ProjectileData data;
    private Vector3 velocity;
    private Transform ignoreRoot;
    private Weapon source;
    private LayerMask mask;

    private bool launched, stuck, resting, finished;
    private float age, dieAt, fuseLeft;
    private int pierced;
    private readonly HashSet<IDamageable> damaged = new HashSet<IDamageable>();

    private static readonly RaycastHit[] sweepBuffer = new RaycastHit[16];
    private static readonly Collider[] overlapBuffer = new Collider[64];

    /// <summary>Called by the weapon, on the server, right after spawning.</summary>
    public void Launch(ProjectileData projectileData, Vector3 startVelocity, Transform ignore, Weapon fromWeapon, LayerMask hitMask)
    {
        data = projectileData;
        velocity = startVelocity;
        ignoreRoot = ignore;
        source = fromWeapon;
        mask = hitMask;

        fuseLeft = data.fuseTime;
        dieAt = data.lifetime;
        if (velocity.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(velocity);

        launched = true;
    }

    private void Update()
    {
        // Clients never call Launch(), so only the server moves the projectile (NetworkTransform syncs it)
        if (!launched || finished) return;

        float dt = Time.deltaTime;
        age += dt;

        if (data.explosionTrigger == ExplosionTrigger.Fuse)
        {
            fuseLeft -= dt;
            if (fuseLeft <= 0f)
            {
                Explode(transform.position);
                return;
            }
        }

        if (age >= dieAt)
        {
            Finish();
            return;
        }

        if (stuck || resting) return;
        Move(dt);
    }

    // ---------------------------------------------------------------- Movement
    private void Move(float dt)
    {
        velocity += Physics.gravity * data.gravityScale * dt;

        Vector3 step = velocity * dt;
        float length = step.magnitude;
        if (length < 0.00001f) return;
        Vector3 dir = step / length;

        Vector3 start = transform.position;

        if (Sweep(dir, length, out RaycastHit hit))
        {
            transform.position += dir * hit.distance; // move to the point of contact
            HandleHit(hit, dir);
        }
        else
        {
            transform.position += step;
        }

        DebugLine(start, transform.position, Color.cyan); // the path it takes

        if (!stuck && !resting && !finished && data.alignToVelocity && velocity.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(velocity);
    }

    private bool Sweep(Vector3 dir, float length, out RaycastHit best)
    {
        int count = Physics.SphereCastNonAlloc(transform.position, Mathf.Max(0.01f, data.projectileRadius), dir,
            sweepBuffer, length, mask, QueryTriggerInteraction.Ignore);

        best = default;
        float bestDistance = float.MaxValue;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit h = sweepBuffer[i];
            if (h.collider.transform.IsChildOf(transform)) continue;                       // ourselves
            if (ignoreRoot != null && h.collider.transform.IsChildOf(ignoreRoot)) continue; // the thrower

            IDamageable target = Weapon.FindDamageable(h.collider);
            if (target != null && damaged.Contains(target)) continue;                      // already hit this one

            if (h.distance < bestDistance)
            {
                bestDistance = h.distance;
                best = h;
                found = true;
            }
        }
        return found;
    }

    // ---------------------------------------------------------------- Hits
    private void HandleHit(RaycastHit hit, Vector3 dir)
    {
        Vector3 normal = hit.normal == Vector3.zero ? -dir : hit.normal;
        DebugPoint(hit.point, Color.yellow); // every impact
        IDamageable target = Weapon.FindDamageable(hit.collider);

        if (target != null)
        {
            damaged.Add(target);

            if (data.damage > 0f)
            {
                target.TakeDamage(new DamageInfo
                {
                    damage = data.damage,
                    point = hit.point,
                    direction = dir,
                    force = dir * data.impactForce,
                    collider = hit.collider,
                    source = source,
                    type = DamageType.Projectile
                });
            }

            if (data.explosionTrigger == ExplosionTrigger.OnImpact)
            {
                Explode(hit.point + normal * 0.1f);
                return;
            }

            if (pierced < data.pierceTargets)
            {
                pierced++; // keeps flying
                return;
            }

            // A bouncing grenade just bounces off the zombie, anything else is used up
            if (data.bounciness > 0f) Bounce(normal);
            else Finish();
            return;
        }

        // Wall / floor / prop
        if (data.explosionTrigger == ExplosionTrigger.OnImpact)
        {
            Explode(hit.point + normal * 0.1f);
        }
        else if (data.bounciness > 0f)
        {
            Bounce(normal);
        }
        else if (data.stickInSurfaces)
        {
            Stick();
        }
        else
        {
            Finish();
        }
    }

    private void Bounce(Vector3 normal)
    {
        Vector3 along = Vector3.Project(velocity, normal);   // part going into the surface
        Vector3 sideways = velocity - along;                  // part sliding along it
        velocity = -along * data.bounciness + sideways * (1f - data.surfaceFriction);

        transform.position += normal * 0.002f; // keep off the surface

        // Come to rest on floors once slow enough
        if (velocity.magnitude < data.restSpeed && normal.y > 0.5f)
        {
            velocity = Vector3.zero;
            resting = true;
        }
    }

    private void Stick()
    {
        stuck = true;
        velocity = Vector3.zero;
        dieAt = age + data.stuckLifetime;
    }

    // ---------------------------------------------------------------- Explosion
    private void Explode(Vector3 center)
    {
        if (finished) return;

        DebugSphere(center, data.explosionRadius, Color.red);
        DebugPoint(center, Color.red);
        DoExplosionDamage(center);

        if (IsSpawned) ExplosionEffectRpc(center);
        else PlayExplosionEffect(center);

        Finish();
    }

    private void DoExplosionDamage(Vector3 center)
    {
        if (data.explosionRadius <= 0f || data.explosionDamage <= 0f) return;

        int count = Physics.OverlapSphereNonAlloc(center, data.explosionRadius, overlapBuffer, mask, QueryTriggerInteraction.Ignore);

        // Closest collider of each target (a zombie has many bone colliders, but should only be hurt once)
        var found = new Dictionary<IDamageable, (Collider collider, Vector3 point, float distance)>();
        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            if (col.transform.IsChildOf(transform)) continue;

            IDamageable target = Weapon.FindDamageable(col);
            if (target == null) continue;

            Vector3 point = col.ClosestPoint(center);
            float distance = Vector3.Distance(center, point);

            if (!found.TryGetValue(target, out var existing) || distance < existing.distance)
                found[target] = (col, point, distance);
        }

        foreach (var entry in found)
        {
            IDamageable target = entry.Key;
            Collider col = entry.Value.collider;
            Vector3 point = entry.Value.point;
            float distance = entry.Value.distance;

            if (data.explosionNeedsLineOfSight && IsBlocked(center, point))
            {
                DebugLine(center, point, Color.gray); // shielded by a wall
                continue;
            }
            DebugLine(center, point, Color.green);    // takes damage

            float t = Mathf.Clamp01(distance / data.explosionRadius);
            float damage = data.explosionDamage * Mathf.Lerp(1f, data.explosionDamageAtEdge, t);

            Vector3 dir = point - center;
            dir = dir.sqrMagnitude > 0.000001f ? dir.normalized : Vector3.up;

            target.TakeDamage(new DamageInfo
            {
                damage = damage,
                point = point,
                direction = dir,
                force = dir * data.explosionForce * (1f - t),
                collider = col,
                source = source,
                type = DamageType.Explosion
            });
        }
    }

    /// <summary>True if a solid, non-damageable object sits between the explosion and the point.</summary>
    private bool IsBlocked(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float length = delta.magnitude;
        if (length < 0.01f) return false;

        int count = Physics.RaycastNonAlloc(from, delta / length, sweepBuffer, length, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider col = sweepBuffer[i].collider;
            if (col.transform.IsChildOf(transform)) continue;
            if (Weapon.FindDamageable(col) != null) continue; // other targets don't shield
            return true;
        }
        return false;
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ExplosionEffectRpc(Vector3 position)
    {
        PlayExplosionEffect(position);
    }

    private void PlayExplosionEffect(Vector3 position)
    {
        if (explosionEffect == null) return;
        GameObject fx = Instantiate(explosionEffect, position, Quaternion.identity);
        Destroy(fx, explosionEffectLifetime);
    }

    // ---------------------------------------------------------------- Debug drawing
    // Wire sphere around a live projectile that will explode (Scene view, or Game view with Gizmos on)
    private void OnDrawGizmos()
    {
        if (!drawDebug || !launched || finished || data == null) return;
        if (data.explosionTrigger == ExplosionTrigger.None || data.explosionRadius <= 0f) return;

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, data.explosionRadius);
    }

    private void DebugLine(Vector3 from, Vector3 to, Color color)
    {
        if (drawDebug) Debug.DrawLine(from, to, color, debugDuration);
    }

    private void DebugPoint(Vector3 point, Color color)
    {
        if (!drawDebug) return;
        const float size = 0.15f;
        Debug.DrawLine(point - Vector3.right * size, point + Vector3.right * size, color, debugDuration);
        Debug.DrawLine(point - Vector3.up * size, point + Vector3.up * size, color, debugDuration);
        Debug.DrawLine(point - Vector3.forward * size, point + Vector3.forward * size, color, debugDuration);
    }

    private void DebugSphere(Vector3 center, float radius, Color color)
    {
        if (!drawDebug) return;
        DebugCircle(center, Vector3.right, Vector3.up, radius, color);
        DebugCircle(center, Vector3.right, Vector3.forward, radius, color);
        DebugCircle(center, Vector3.up, Vector3.forward, radius, color);
    }

    private void DebugCircle(Vector3 center, Vector3 axisA, Vector3 axisB, float radius, Color color)
    {
        const int steps = 32;
        Vector3 previous = center + axisA * radius;
        for (int i = 1; i <= steps; i++)
        {
            float angle = i * Mathf.PI * 2f / steps;
            Vector3 next = center + (axisA * Mathf.Cos(angle) + axisB * Mathf.Sin(angle)) * radius;
            Debug.DrawLine(previous, next, color, debugDuration);
            previous = next;
        }
    }

    // ---------------------------------------------------------------- Cleanup
    private void Finish()
    {
        if (finished) return;
        finished = true;

        if (IsSpawned)
        {
            if (IsServer) NetworkObject.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}