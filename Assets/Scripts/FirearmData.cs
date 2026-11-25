using UnityEngine;

public enum FireMode { SemiAuto, FullAuto, Burst }
public enum ReloadMode { Magazine, SingleShell }

/// <summary>Create via: Create > Weapons > Firearm Data</summary>
[CreateAssetMenu(fileName = "NewFirearm", menuName = "Weapons/Firearm Data")]
public class FirearmData : WeaponData
{
    [Header("Range & Falloff")]
    [Tooltip("Pellets fired per shot (1 for rifles/pistols, 8+ for shotguns). Each pellet does 'damage'.")]
    public int pellets = 1;
    public float range = 100f;
    [Tooltip("Damage multiplier at max range (1 = no falloff).")]
    [Range(0f, 1f)] public float damageAtMaxRange = 0.6f;
    [Tooltip("Distance where damage starts to fall off.")]
    public float falloffStartDistance = 30f;

    [Header("Penetration")]
    [Tooltip("Layers the bullet may pass through (walls, props). Layers that aren't ticked always stop it.")]
    public LayerMask penetrableLayers = ~0;
    [Tooltip("Thickest wall in meters the bullet can pass through. 0 = no wall penetration.")]
    public float maxWallThickness = 0.3f;
    [Tooltip("Damage kept after passing through a wall (0.5 = half).")]
    [Range(0f, 1f)] public float wallDamageRetained = 0.5f;
    [Tooltip("Extra targets (zombies) the bullet can pass through after the first. 0 = stops at the first one.")]
    public int maxTargetPenetrations = 1;
    [Tooltip("Damage kept after passing through a target.")]
    [Range(0f, 1f)] public float targetDamageRetained = 0.7f;

    [Header("Fire Mode & Rate")]
    public FireMode fireMode = FireMode.FullAuto;
    [Tooltip("Rounds per minute (for burst: the speed of shots inside a burst).")]
    public float fireRate = 600f;
    public int burstCount = 3;
    [Tooltip("Pause after a burst before you can fire again.")]
    public float burstCooldown = 0.25f;
    [Tooltip("Start reloading automatically when the magazine runs empty.")]
    public bool autoReload = true;

    [Header("Ammo")]
    public int magazineSize = 30;
    [Tooltip("Number of spare magazines at start (total spare bullets = this x magazineSize).")]
    public int spareMagazines = 5;
    public int maxSpareMagazines = 8;

    [Header("Reloading")]
    [Tooltip("Magazine: whole magazine at once. SingleShell: one round at a time (shotguns).")]
    public ReloadMode reloadMode = ReloadMode.Magazine;
    [Tooltip("Reload duration in seconds. Set to -1 to use the reload animation length.")]
    public float reloadTime = -1f;
    [Tooltip("Single shell mode: time before the first shell goes in.")]
    public float shellReloadStartTime = 0.4f;
    [Tooltip("Single shell mode: time to insert each shell.")]
    public float shellInsertTime = 0.5f;
    [Tooltip("Single shell mode: time to finish after the last shell.")]
    public float shellReloadEndTime = 0.4f;
    [Tooltip("Single shell mode: pressing fire cancels the reload (needs at least one round loaded).")]
    public bool canFireInterruptReload = true;

    [Header("Accuracy (spread in degrees, half-angle of the cone)")]
    public float spreadStationary = 0.5f;
    public float spreadMoving = 3.5f;
    [Tooltip("Seconds for accuracy to go from stationary to moving once you start moving.")]
    public float spreadWorsenTime = 0.15f;
    [Tooltip("Seconds for accuracy to settle back to stationary once you stop moving.")]
    public float spreadRecoverTime = 0.35f;
    [Tooltip("Extra spread added per shot (bloom), decays over time.")]
    public float bloomPerShot = 0.3f;
    public float maxBloom = 3f;
    [Tooltip("Bloom lost per second.")]
    public float bloomRecovery = 6f;

    [Header("Recoil (camera kick in degrees, applied by the player's look script)")]
    [Tooltip("Degrees the view kicks UP per shot (random between min and max).")]
    public float recoilPitchMin = 0.4f;
    public float recoilPitchMax = 0.8f;
    [Tooltip("Degrees the view kicks sideways per shot. Negative = left, positive = right.")]
    public float recoilYawMin = -0.3f;
    public float recoilYawMax = 0.3f;
    [Tooltip("Degrees per second the view returns towards where it started once you stop firing.")]
    public float recoilRecoverySpeed = 6f;
    [Tooltip("Seconds after the last shot before recoil starts recovering.")]
    public float recoilRecoveryDelay = 0.1f;

    [Header("Firing Animation")]
    [Tooltip("If enabled, the bullet is fired after a delay.")]
    public bool useFireDelay = false;

    [Min(0f)]
    [Tooltip("Time between starting the Fire animation and firing the bullet.")]
    public float fireDelay = 0.1f;

    [Header("Audio (optional)")]
    public AudioClip reloadSound;
    public AudioClip emptySound;

    // Helpers
    public float SecondsBetweenShots => 60f / Mathf.Max(1f, fireRate);
}