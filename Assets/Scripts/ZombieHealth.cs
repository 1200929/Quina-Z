using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Simple zombie health. Implements IDamageable so Weapon can shoot it.
/// Put this on the zombie ROOT (same object as the NavMeshAgent).
///
/// On death the zombie's model is detached from the root and left as a ragdoll corpse,
/// and the root is destroyed immediately, so anything that counts zombies by
/// "does the object still exist" (like the wave spawner) sees it as dead right away.
///
/// Requires a ragdoll: select the zombie model and use GameObject > 3D Object > Ragdoll...
/// Don't put a Rigidbody on the root object.
///
/// Layers (create them in Project Settings > Tags and Layers):
///   ZombieHitbox : the bone colliders while alive (shots hit them, the player walks through them)
///   Corpse       : the ragdoll after death
///   Player       : put the player (and its children) on this layer
/// The script makes ZombieHitbox and Corpse ignore Player by itself.
/// </summary>
public class ZombieHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    public float maxHealth = 100f;

    [Header("Hit zones (matched against the name of the collider that was hit)")]
    public string headName = "spine.005";
    public float headMultiplier = 2.5f;

    [Header("Layers (leave a name empty to skip that step)")]
    [Tooltip("Layer for the bone colliders while the zombie is alive.")]
    public string hitboxLayerName = "ZombieHitbox";
    [Tooltip("Layer for the ragdoll after death.")]
    public string corpseLayerName = "Corpse";
    [Tooltip("The layer your player is on. Hitbox and Corpse layers will not collide with it.")]
    public string playerLayerName = "Player";
    [Tooltip("The root capsule still blocks the player physically, but shots pass through it " +
             "so they always reach the bone hitboxes (headshots).")]
    public bool rootIgnoresShots = true;

    [Header("Death")]
    [Tooltip("Seconds the ragdoll corpse stays in the world before it's destroyed.")]
    public float corpseLifetime = 10f;
    [Tooltip("Scales the push from the bullet on the ragdoll.")]
    public float deathForceMultiplier = 1f;
    [Tooltip("Hard limit (m/s) for the push on the body, so shots never launch it.")]
    public float maxPushSpeed = 4f;
    [Tooltip("Limits how much of the push can go upwards (0 = none, 1 = unlimited).")]
    [Range(0f, 1f)] public float maxUpwardPush = 0.2f;

    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    private Rigidbody[] ragdollBodies;

    private void Awake()
    {
        CurrentHealth = maxHealth;

        // Ragdoll bones = Rigidbodies on children. Keep them off while the zombie is alive.
        var found = new System.Collections.Generic.List<Rigidbody>();
        foreach (var rb in GetComponentsInChildren<Rigidbody>())
            if (rb.gameObject != gameObject) found.Add(rb);
        ragdollBodies = found.ToArray();

        foreach (var rb in ragdollBodies)
            rb.isKinematic = true;

        if (ragdollBodies.Length == 0)
            Debug.LogWarning($"{name}: no ragdoll Rigidbodies found on children. " +
                             "Create the ragdoll on the zombie model (GameObject > 3D Object > Ragdoll...).", this);

        SetupLayers();
    }

    private void SetupLayers()
    {
        int hitboxLayer = GetLayer(hitboxLayerName);
        int corpseLayer = GetLayer(corpseLayerName);
        int playerLayer = GetLayer(playerLayerName);

        // Player walks through living bone colliders and through ragdolls
        if (playerLayer >= 0)
        {
            if (hitboxLayer >= 0) Physics.IgnoreLayerCollision(hitboxLayer, playerLayer, true);
            if (corpseLayer >= 0) Physics.IgnoreLayerCollision(corpseLayer, playerLayer, true);
        }

        // Living bone colliders go on the hitbox layer (shots still hit them)
        if (hitboxLayer >= 0)
        {
            foreach (var col in GetComponentsInChildren<Collider>())
                if (col.gameObject != gameObject)
                    col.gameObject.layer = hitboxLayer;
        }

        // Root capsule: blocks the player, but raycasts pass through it
        if (rootIgnoresShots)
            gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
    }

    private int GetLayer(string layerName)
    {
        if (string.IsNullOrEmpty(layerName)) return -1;
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0)
            Debug.LogWarning($"Layer '{layerName}' doesn't exist. Create it in Project Settings > Tags and Layers.", this);
        return layer;
    }

    // Called by Weapon when a shot (or melee hit) lands on this zombie.
    public void TakeDamage(DamageInfo info)
    {
        if (IsDead) return;

        float multiplier = IsHead(info.collider) ? headMultiplier : 1f;
        CurrentHealth -= info.damage * multiplier;

        if (CurrentHealth <= 0f) Die(info);
    }

    private static void SetLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform child in t)
            SetLayerRecursive(child, layer);
    }

    private bool IsHead(Collider col)
    {
        return col != null && col.name.ToLowerInvariant().Contains(headName.ToLowerInvariant());
    }

    private void Die(DamageInfo info)
    {
        IsDead = true;

        // Stop the zombie acting alive
        foreach (var script in GetComponents<MonoBehaviour>())
            if (script != this) script.enabled = false;

        var agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        // Stop every animation so the physics fully controls the bones
        foreach (var animator in GetComponentsInChildren<Animator>())
            animator.enabled = false;

        // The root capsule would overlap the bones when they switch to physics
        foreach (var col in GetComponents<Collider>())
            col.enabled = false;

        // No ragdoll set up: nothing to leave behind, just remove the zombie
        if (ragdollBodies.Length == 0)
        {
            Destroy(gameObject);
            return;
        }

        // Detach the model(s) from the root so they survive when the root is destroyed
        var corpseParts = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in transform)
            corpseParts.Add(child);

        int corpseLayer = GetLayer(corpseLayerName);
        foreach (var part in corpseParts)
        {
            part.SetParent(null, true);
            if (corpseLayer >= 0) SetLayerRecursive(part, corpseLayer);
            Destroy(part.gameObject, corpseLifetime);
        }

        // Hand the bones to physics. Low depenetration speed stops overlapping colliders
        // from violently pushing the body away when physics turns on.
        foreach (var rb in ragdollBodies)
        {
            rb.isKinematic = false;
            rb.maxDepenetrationVelocity = 1.5f;
        }

        PushBody(info);

        // Root is gone immediately -> the wave spawner counts this zombie as dead
        Destroy(gameObject);
    }

    private void PushBody(DamageInfo info)
    {
        if (info.collider == null) return;

        Rigidbody hitBody = info.collider.attachedRigidbody;
        if (hitBody == null || hitBody.isKinematic) return;

        Vector3 push = info.force * deathForceMultiplier;
        if (push.sqrMagnitude < 0.0001f) return;

        // Speed from the impulse, but capped so light bones (the head) never get launched
        float speed = Mathf.Min(push.magnitude / hitBody.mass, maxPushSpeed);

        Vector3 dir = push.normalized;
        dir.y = Mathf.Clamp(dir.y, -maxUpwardPush, maxUpwardPush);
        dir.Normalize();

        hitBody.AddForce(dir * speed, ForceMode.VelocityChange);
    }
}