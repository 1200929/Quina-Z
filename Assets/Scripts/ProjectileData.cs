using UnityEngine;

public enum ExplosionTrigger { None, OnImpact, Fuse }

/// <summary>
/// Create via: Create > Weapons > Projectile Data
/// For things you throw or shoot as a physical object: spears, arrows, grenades...
///
/// Examples:
///   Spear   : speed 25, gravityScale 1, stickInSurfaces on, no explosion.
///   Grenade : speed 14, launchUpAngle 10, bounciness 0.4, explosionTrigger Fuse, fuseTime 3.
///   Impact grenade / rocket : explosionTrigger OnImpact.
/// </summary>
[CreateAssetMenu(fileName = "NewProjectile", menuName = "Weapons/Projectile Data")]
public class ProjectileData : WeaponData
{
    [Header("Projectile object")]
    [Tooltip("Prefab with the Projectile script (and the model). For multiplayer it also needs a NetworkObject + NetworkTransform and must be in the NetworkManager prefab list.")]
    public GameObject projectilePrefab;

    [Header("Flight")]
    public float projectileSpeed = 25f;
    [Tooltip("0 = flies straight, 1 = normal gravity.")]
    public float gravityScale = 1f;
    [Tooltip("Degrees the throw is angled upwards from the crosshair (useful for grenades).")]
    public float launchUpAngle = 0f;
    [Tooltip("Random spread in degrees.")]
    public float spread = 0f;
    [Tooltip("Size of the collision sphere that sweeps along the flight path.")]
    public float projectileRadius = 0.05f;
    [Tooltip("Seconds before the projectile removes itself.")]
    public float lifetime = 15f;
    [Tooltip("Point the model along its velocity (arrows, spears).")]
    public bool alignToVelocity = true;

    [Header("On hitting a surface")]
    [Tooltip("Stay stuck in walls/floors (spear, arrow). Ignored if bounciness is above 0.")]
    public bool stickInSurfaces = true;
    [Tooltip("Seconds a stuck projectile stays before disappearing.")]
    public float stuckLifetime = 20f;
    [Tooltip("0 = no bounce, 1 = perfect bounce (grenades ~0.4).")]
    [Range(0f, 1f)] public float bounciness = 0f;
    [Tooltip("Speed lost along the surface on each bounce.")]
    [Range(0f, 1f)] public float surfaceFriction = 0.3f;
    [Tooltip("Below this speed on a floor, the projectile stops moving.")]
    public float restSpeed = 0.6f;
    [Tooltip("Targets (zombies) the projectile can pass through after the first. 0 = stops at the first.")]
    public int pierceTargets = 0;

    [Header("Explosion")]
    [Tooltip("None: no explosion. OnImpact: explodes on first contact. Fuse: explodes after 'fuseTime' seconds.")]
    public ExplosionTrigger explosionTrigger = ExplosionTrigger.None;
    public float fuseTime = 3f;
    public float explosionRadius = 5f;
    public float explosionDamage = 150f;
    [Tooltip("Damage multiplier at the edge of the radius (0 = nothing, 1 = same as the center).")]
    [Range(0f, 1f)] public float explosionDamageAtEdge = 0.2f;
    [Tooltip("Impulse pushing ragdolls away from the center.")]
    public float explosionForce = 20f;
    [Tooltip("Walls block the explosion.")]
    public bool explosionNeedsLineOfSight = true;

    [Header("Ammo & timing")]
    public int startingAmmo = 2;
    public int maxAmmo = 4;
    [Tooltip("Delay between pressing attack and the projectile leaving (match your throw/release animation).")]
    public float releaseDelay = 0.25f;
    [Tooltip("Time after pressing attack before you can use it again (nocking an arrow, drawing a new grenade).")]
    public float cooldown = 1f;
}