using UnityEngine;

/// <summary>
/// Information about one hit, sent by a Weapon to whatever it hit.
/// </summary>
public struct DamageInfo
{
    /// <summary>Base damage after range falloff. Hit-zone multipliers (headshots etc.) are up to the receiver.</summary>
    public float damage;
    /// <summary>World position of the impact.</summary>
    public Vector3 point;
    /// <summary>Direction the shot was travelling (normalized).</summary>
    public Vector3 direction;
    /// <summary>Impulse the weapon wants applied (direction * impactForce), e.g. for ragdolls.</summary>
    public Vector3 force;
    /// <summary>The exact collider that was hit (use it to work out head/torso/limb).</summary>
    public Collider collider;
    /// <summary>The weapon that fired.</summary>
    public Weapon source;
}

/// <summary>
/// Implement this on anything that can be shot (zombies, props, barricades...).
/// The Weapon looks for it on the hit collider or its parents.
/// </summary>
public interface IDamageable
{
    void TakeDamage(DamageInfo info);
}