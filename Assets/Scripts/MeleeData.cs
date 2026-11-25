using UnityEngine;

/// <summary>
/// Create via: Create > Weapons > Melee Data
///
/// A swing sweeps an arc: one ray is fired every 'rayInterval' seconds, moving from the start angle
/// to the end angle over 'swingDuration'. Yaw: negative = left, positive = right.
/// Pitch: positive = down. A target hit by several rays in the same swing is only damaged once.
/// </summary>
[CreateAssetMenu(fileName = "NewMelee", menuName = "Weapons/Melee Data")]
public class MeleeData : WeaponData
{
    [Header("Reach")]
    [Tooltip("How far each ray reaches, in meters.")]
    public float range = 2.5f;
    [Tooltip("Radius of each ray (0 = thin ray). A small radius makes hits more forgiving.")]
    public float rayRadius = 0.1f;

    [Header("Swing arc (degrees, relative to the crosshair)")]
    public float arcStartYaw = -30f;
    public float arcEndYaw = 30f;
    public float arcStartPitch = 0f;
    [Tooltip("Positive = down.")]
    public float arcEndPitch = 5f;

    [Header("Swing timing (seconds)")]
    [Tooltip("Delay between pressing attack and the first ray (match the wind-up of the animation).")]
    public float hitDelay = 0.15f;
    [Tooltip("Time it takes the rays to sweep from the start to the end of the arc.")]
    public float swingDuration = 0.3f;
    [Tooltip("Time between rays during the swing.")]
    public float rayInterval = 0.1f;
    [Tooltip("Time after the swing ends before you can attack again.")]
    public float cooldown = 0.4f;

    [Header("Hits")]
    [Tooltip("Max different targets one swing can damage. 0 = unlimited.")]
    public int maxTargetsPerSwing = 0;
    [Tooltip("Keep attacking while the button is held.")]
    public bool holdToAttack = false;
}