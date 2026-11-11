using UnityEngine;

public enum WeaponType { Ranged, Melee }
public enum FireMode { SemiAuto, FullAuto, Burst }

/// <summary>
/// All the stats for one weapon. Create one asset per weapon:
/// Right click in Project > Create > Weapons > Weapon Data
/// The same Weapon script reads these, so every gun/knife shares the same code.
/// </summary>
[CreateAssetMenu(fileName = "NewWeapon", menuName = "Weapons/WeaponData")]
public class WeaponData : ScriptableObject
{
    [Header("General")]
    public string weaponName = "Weapon";
    public WeaponType weaponType = WeaponType.Ranged;

    [Header("Damage")]
    public float damage = 25f;
    [Tooltip("Pellets fired per shot (1 for rifles/pistols, 8+ for shotguns). Each pellet does 'damage'.")]
    public int pellets = 1;
    [Tooltip("Max distance (ranged) or reach (melee) in meters.")]
    public float range = 100f;
    [Tooltip("Damage multiplier at max range (1 = no falloff).")]
    [Range(0f, 1f)] public float damageAtMaxRange = 0.6f;
    [Tooltip("Distance where damage starts to fall off.")]
    public float falloffStartDistance = 30f;
    [Tooltip("Impulse applied to the ragdoll bone when the shot kills a zombie.")]
    public float impactForce = 12f;

    [Header("Fire Mode & Rate")]
    public FireMode fireMode = FireMode.FullAuto;
    [Tooltip("Rounds per minute (for burst: the speed of shots inside a burst).")]
    public float fireRate = 600f;
    public int burstCount = 3;
    [Tooltip("Pause after a burst before you can fire again.")]
    public float burstCooldown = 0.25f;

    [Header("Ammo")]
    public int magazineSize = 30;
    [Tooltip("Number of spare magazines at start (total spare bullets = this x magazineSize).")]
    public int spareMagazines = 5;
    public int maxSpareMagazines = 8;
    public float reloadTime = 2.2f;

    [Header("Accuracy (spread in degrees, half-angle of the cone)")]
    public float spreadStationary = 0.5f;
    public float spreadMoving = 3.5f;
    [Tooltip("Seconds to blend between stationary and moving accuracy.")]
    public float accuracyTransitionTime = 0.15f;
    [Tooltip("Extra spread added per shot (bloom), decays over time.")]
    public float bloomPerShot = 0.3f;
    public float maxBloom = 3f;
    [Tooltip("Bloom lost per second.")]
    public float bloomRecovery = 6f;

    [Header("Handling")]
    [Tooltip("Player speed multiplier while this weapon is equipped (1 = normal, 0.9 = heavy).")]
    public float movementSpeedMultiplier = 1f;

    [Header("Melee only")]
    [Tooltip("Radius of the swing sphere, makes melee more forgiving.")]
    public float meleeRadius = 0.4f;

    [Header("Audio (optional)")]
    public AudioClip fireSound;
    public AudioClip reloadSound;
    public AudioClip emptySound;

    // Helpers
    public float SecondsBetweenShots => 60f / Mathf.Max(1f, fireRate);
}