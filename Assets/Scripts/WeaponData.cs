using UnityEngine;

/// <summary>
/// Stats shared by every weapon. Don't create this directly, create one of the types:
///   Create > Weapons > Firearm Data / Melee Data / Projectile Data
/// </summary>
public abstract class WeaponData : ScriptableObject
{
    [Header("HUD")]
    [Tooltip("Name shown on the HUD.")]
    public string weaponName = "Weapon";
    public Sprite icon;

    [Header("Damage")]
    [Tooltip("Damage per bullet/pellet (firearm), per hit (melee) or on direct hit (projectile).")]
    public float damage = 25f;
    [Tooltip("Impulse applied to the ragdoll bone when this weapon kills something.")]
    public float impactForce = 12f;

    [Header("Handling")]
    [Tooltip("Player speed multiplier while this weapon is equipped (1 = normal, 0.9 = heavy). Read via Weapon.MovementSpeedMultiplier.")]
    public float movementSpeedMultiplier = 1f;

    [Header("Audio (optional)")]
    [Tooltip("Gunshot / swing / throw sound.")]
    public AudioClip attackSound;
    [Tooltip("Played when the weapon is equipped / drawn.")]
    public AudioClip drawSound;
}