using UnityEngine;

/// <summary>
/// Information about one hit, sent by a Weapon to whatever it hit.
/// </summary>
public struct DamageInfo
{
    /// Base damage after range falloff. Hit-zone multipliers (headshots etc.) are up to the receiver
    public float damage;
    // World position of the impact.
    public Vector3 point;
    // Direction the shot was travelling (normalized).
    public Vector3 direction;
    // Impulse the weapon wants applied (direction * impactForce), used in ragdolls for now.
    public Vector3 force;
    // The exact collider that was hit (use it to work out head/torso/limb).
    public Collider collider;
    // The weapon that fired.
    public Weapon source;
}


// Anything that can be shot is included in this interface.
public interface IDamageable
{
    void TakeDamage(DamageInfo info);
}