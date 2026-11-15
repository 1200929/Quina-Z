using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Simple zombie health. Implements IDamageable so Weapon can shoot it.
/// Put this on the zombie ROOT (same object as the NavMeshAgent).
///
/// Multiplayer: only the server applies damage and decides when the zombie dies.
/// On death the server tells every player (DieClientRpc) to turn the zombie model into a
/// ragdoll corpse, then despawns the root, so anything that counts zombies by
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
public class ZombieHealth : NetworkBehaviour, IDamageable
{
    [Header("Health")]
    public float maxHealth = 100f;

    [Header("Hit zones (matched against the name of the collider that was hit)")]
    public string headName = "spine.005";
    public float headMultiplier = 2.5f;

    [Header("Points System")]
    public int headPoints = 50;
    public int legPoints = 20;
    public int torsoPoints = 10;
    public int killBonus = 90;

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
    public float maxPushSpeed = 15f;
    [Tooltip("Limits how much of the push can go upwards (0 = none, 1 = unlimited).")]
    [Range(0f, 1f)] public float maxUpwardPush = 1f;

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

        // Player walks through living bone colliders and through ragdolls (maybe to be changed later)
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

        // Blocks the player, but raycasts pass through it
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

    // Called by Weapon when a shot lands on this zombie (info). Only the server applies damage.
    public void TakeDamage(DamageInfo info)
    {
        if (!IsServer) return;
        if (IsDead) return;

        float multiplier = IsHead(info.collider) ? headMultiplier : 1f;
        CurrentHealth -= info.damage * multiplier;

        int pointsEarned = torsoPoints; // Default for now, any part that is not the legs or head is considered the torso for testing purposes.
        if (IsHead(info.collider)) pointsEarned = headPoints;
        else if (IsLeg(info.collider)) pointsEarned = legPoints;

        if (CurrentHealth <= 0f) pointsEarned += killBonus;

        if (info.source != null)
        {
            PlayerPoints player = info.source.GetComponentInParent<PlayerPoints>();
            if (player != null) player.AddPoints(pointsEarned);
        }
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

    private bool IsLeg(Collider col)
    {
        if (col == null) return false;
        string n = col.name.ToLowerInvariant();
        return n.Contains("leg") || n.Contains("shin") || n.Contains("thigh") || n.Contains("calf");
    }

    // Runs on the server: tells everyone to show the ragdoll, then removes the zombie
    private void Die(DamageInfo info)
    {
        IsDead = true;

        string hitBone = info.collider != null ? info.collider.name : "";
        DieClientRpc(hitBone, info.force);   // ragdoll on every player (host included)

        // Root is gone -> the wave spawner counts this zombie as dead
        NetworkObject.Despawn();
    }

    // Runs on EVERY machine: turns the zombie model into a ragdoll corpse
    [Rpc(SendTo.ClientsAndHost)]
    private void DieClientRpc(string hitBoneName, Vector3 force)
    {
        IsDead = true;

        // Stop the zombie acting alive
        foreach (var script in GetComponents<MonoBehaviour>())
            if (script != this && !(script is NetworkObject))
                script.enabled = false;

        var agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        // Stop every animation so the physics fully controls the bones
        foreach (var animator in GetComponentsInChildren<Animator>())
            animator.enabled = false;

        // The root capsule would overlap the bones when they switch to physics
        foreach (var col in GetComponents<Collider>())
            col.enabled = false;

        // No ragdoll set up: nothing to leave behind (the server removes the root)
        if (ragdollBodies.Length == 0) return;

        // Detach the model(s) from the root so they survive when the root is removed
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

        PushBody(hitBoneName, force);
    }

    private void PushBody(string hitBoneName, Vector3 force)
    {
        // Colliders can't be sent over the network, so find the bone by name
        Rigidbody hitBody = null;
        foreach (var rb in ragdollBodies)
        {
            if (rb.name == hitBoneName) { hitBody = rb; break; }
        }
        if (hitBody == null || hitBody.isKinematic) return;

        Vector3 push = force * deathForceMultiplier;
        if (push.sqrMagnitude < 0.0001f) return;

        float speed = push.magnitude / hitBody.mass;
        hitBody.AddForce(push.normalized * speed, ForceMode.VelocityChange);
    }
}