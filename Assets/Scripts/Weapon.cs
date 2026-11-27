using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Base class for every weapon. Don't add this directly, add one of:
///   Firearm, MeleeWeapon, ProjectileWeapon
///
/// Common stuff lives here: references, draw animation, busy state, damage helper, events.
/// Hits are applied on the server (host); clients send their aim to the server with an RPC.
///
/// Hooks for other systems:
///   - Player movement: set weapon.IsMoving every frame, read weapon.MovementSpeedMultiplier.
///   - Targets: implement IDamageable on anything that can be damaged.
///   - HUD/FX: subscribe to the On... events (hit events only fire on the server/host).
/// </summary>
public abstract class Weapon : NetworkBehaviour
{
    [Header("References")]
    [Tooltip("Where rays start/aim from. Assign the player camera (falls back to Camera.main).")]
    public Transform aimOrigin;
    [Tooltip("Optional. Colliders under this transform are ignored by shots (assign the player root).")]
    public Transform ignoreRoot;
    [Tooltip("Optional. Used for weapon sounds.")]
    public AudioSource audioSource;
    [Tooltip("Layers the weapon can hit. The 'Ignore Raycast' layer is always excluded.")]
    public LayerMask hitMask = Physics.DefaultRaycastLayers;
    [Tooltip("Layers (by name) that shots always pass through, e.g. ragdoll corpses and players. " +
             "Names of layers that don't exist are skipped.")]
    public string[] alwaysIgnoreLayers = { "Corpse", "Player" };

    [Header("Animation (optional - leave the Animator empty to run without animations)")]
    public Animator animator;

    [Tooltip("Animator trigger parameters. Names must match your Animator Controller.")]
    public string drawTrigger = "Draw";
    [Tooltip("Name of the Draw state in the Animator. The draw is played by jumping straight to this state, " +
             "so it restarts every time (even if the Animator has no Any State > Draw transition). " +
             "Leave empty to use the trigger only.")]
    public string drawStateName = "Draw";
    public string reloadTrigger = "Reload";
    [Tooltip("Played on every shot / swing / throw.")]
    public string fireTrigger = "Fire";
    [Tooltip("State TAGS set on the Draw and Reload states in the Animator. " +
             "The weapon is busy (can't fire) while the animator is in a state with these tags.")]
    public string drawTag = "Draw";
    public string reloadTag = "Reload";

    /// <summary>The stats asset of this weapon (each subclass has its own typed 'data' field).</summary>
    public abstract WeaponData BaseData { get; }

    // ---- Set by the player later ----
    /// <summary>Set this from your movement script. Moving uses the "moving" spread.</summary>
    public bool IsMoving { get; set; }
    /// <summary>Read this from your movement script to slow the player with heavier weapons.</summary>
    public float MovementSpeedMultiplier => BaseData != null ? BaseData.movementSpeedMultiplier : 1f;

    // ---- Runtime state (read these from your HUD) ----
    /// <summary>Rounds in the magazine (firearms only).</summary>
    public int AmmoInMagazine { get; protected set; }
    /// <summary>Spare rounds (firearms) or items left (projectile weapons).</summary>
    public int ReserveAmmo { get; protected set; }
    public bool IsReloading { get; protected set; }
    public bool IsDrawing { get; private set; }
    /// <summary>True while a draw or reload is in progress. You can't attack then.</summary>
    public virtual bool IsBusy => IsDrawing || IsReloading;

    public event Action OnAmmoChanged;
    public event Action OnFired;
    public event Action OnReloadStarted;
    public event Action OnReloadFinished;
    /// <summary>Hit info, and whether the thing hit was an IDamageable. Only fires on the server/host.</summary>
    public event Action<RaycastHit, bool> OnHit;

    protected void RaiseAmmoChanged() => OnAmmoChanged?.Invoke();
    protected void RaiseFired() => OnFired?.Invoke();
    protected void RaiseReloadStarted() => OnReloadStarted?.Invoke();
    protected void RaiseReloadFinished() => OnReloadFinished?.Invoke();
    protected void RaiseHit(RaycastHit hit, bool wasDamageable) => OnHit?.Invoke(hit, wasDamageable);

    protected readonly RaycastHit[] hitBuffer = new RaycastHit[32];
    protected static readonly IComparer<RaycastHit> DistanceComparer =
        Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

    /// <summary>True on the host/server, and also when running without networking (e.g. testing a lone scene).</summary>
    protected bool RunsOnServer => !IsSpawned || IsServer;

    // ---------------------------------------------------------------- Lifecycle
    private int effectiveHitMask;
    /// <summary>hitMask without 'Ignore Raycast' and without the layers in alwaysIgnoreLayers. Use this for every cast.</summary>
    protected int HitMaskValue => effectiveHitMask;

    private int ComputeHitMask()
    {
        int mask = hitMask.value & Physics.DefaultRaycastLayers; // removes the Ignore Raycast layer
        if (alwaysIgnoreLayers != null)
        {
            foreach (string layerName in alwaysIgnoreLayers)
            {
                if (string.IsNullOrEmpty(layerName)) continue;
                int layer = LayerMask.NameToLayer(layerName);
                if (layer >= 0) mask &= ~(1 << layer);
            }
        }
        return mask;
    }

    protected virtual void Awake()
    {
        effectiveHitMask = ComputeHitMask();

        if (aimOrigin == null && Camera.main != null)
            aimOrigin = Camera.main.transform;

        SetupAnimator();

        ResetAmmo();
    }

    protected virtual void OnEnable()
    {
        ResetState();
        StartCoroutine(DrawRoutine()); // equipping plays the draw animation
    }

    protected virtual void OnDisable()
    {
        // Unequipping cancels draw / reload / burst / swing
        StopAllCoroutines();
        IsDrawing = false;
        IsReloading = false;
        ResetState();
        ResetAnimatorTriggers();

        // Cut any sound still playing (gunshot tail, reload, draw...) when switching away
        if (audioSource != null) audioSource.Stop();
    }

    private void Update()
    {
        if (BaseData == null || aimOrigin == null) return;
        if (IsSpawned && !IsOwner) return; // only the owning player controls this weapon
        Tick();
    }

    /// <summary>Per-frame logic (input, spread, recoil...) implemented by each weapon type.</summary>
    protected abstract void Tick();

    /// <summary>Called when equipped and unequipped, so subclasses can clear their own flags.</summary>
    protected virtual void ResetState() { }

    protected virtual void SetupAnimator()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogError($"{name}: Could not find the model Animator!", this);
            return;
        }
    }

    public virtual void ResetAmmo() { }

    /// <summary>Call from ammo pickups / wall buys. Returns true if any ammo was added.</summary>
    public virtual bool AddAmmo(int amount) { return false; }

    public virtual bool CanEquip => true;
    public virtual bool WantsToUnequip => false;

    /// <summary>Text for the ammo HUD: "30 / 150" for guns, the item count for throwables, empty for melee.</summary>
    public virtual string GetAmmoText() => AmmoInMagazine + " / " + ReserveAmmo;

    /// <summary>
    /// Camera kick (degrees) to apply this frame, then cleared. x = pitch UP, y = yaw to the RIGHT.
    /// Only firearms have recoil; the player's look script should call this every frame.
    /// </summary>
    public virtual Vector2 ConsumeRecoil() => Vector2.zero;

    // ---------------------------------------------------------------- Animation helpers
    protected void SetTrigger(string trigger)
    {
        if (animator != null && animator.isActiveAndEnabled && !string.IsNullOrEmpty(trigger))
            animator.SetTrigger(trigger);
    }

    protected virtual void ResetAnimatorTriggers()
    {
        if (animator == null) return;
        if (!string.IsNullOrEmpty(drawTrigger)) animator.ResetTrigger(drawTrigger);
        if (!string.IsNullOrEmpty(reloadTrigger)) animator.ResetTrigger(reloadTrigger);
        if (!string.IsNullOrEmpty(fireTrigger)) animator.ResetTrigger(fireTrigger);
    }

    /// <summary>True if the animator is in (or transitioning into) a state with this tag.</summary>
    private bool AnimatorInTag(string tag)
    {
        if (animator == null || string.IsNullOrEmpty(tag)) return false;
        if (animator.GetCurrentAnimatorStateInfo(0).IsTag(tag)) return true;
        return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsTag(tag);
    }

    /// <summary>
    /// Waits until the animation tagged 'tag' has started and then finished.
    /// If the animator never enters it (trigger/tag not set up), logs a warning and waits 'fallbackSeconds' instead.
    /// </summary>
    protected IEnumerator WaitForAnimation(string tag, float fallbackSeconds, Action onStarted = null)
    {
        if (animator == null)
        {
            onStarted?.Invoke();
            if (fallbackSeconds > 0f) yield return new WaitForSeconds(fallbackSeconds);
            yield break;
        }

        float waited = 0f;
        while (!AnimatorInTag(tag))
        {
            waited += Time.deltaTime;
            if (waited > 1.5f) // generous: the previous animation (e.g. a long fire anim) may still be finishing
            {
                Debug.LogWarning($"{name}: animator never entered a state tagged '{tag}'. " +
                                 "Check the trigger name, the transition and the state's Tag.", this);
                onStarted?.Invoke();
                if (fallbackSeconds > 0f) yield return new WaitForSeconds(fallbackSeconds);
                yield break;
            }
            yield return null;
        }

        onStarted?.Invoke(); // the animation has really started now
        while (AnimatorInTag(tag))
            yield return null;
    }

    /// <summary>Plays the draw animation and sound again, e.g. after throwing a grenade to grab the next one.</summary>
    protected void PlayDraw()
    {
        if (!isActiveAndEnabled || IsDrawing) return;
        StartCoroutine(DrawRoutine());
    }

    private void PlayDrawAnimation()
    {
        if (animator == null) return;

        // Jumping straight to the Draw state always restarts it, whatever state the animator is in
        if (!string.IsNullOrEmpty(drawStateName))
        {
            int stateHash = Animator.StringToHash(drawStateName);
            if (animator.HasState(0, stateHash))
            {
                animator.Play(stateHash, 0, 0f);
                return;
            }
            Debug.LogWarning($"{name}: the Animator has no state called '{drawStateName}'. Using the '{drawTrigger}' trigger instead.", this);
        }

        SetTrigger(drawTrigger);
    }

    private IEnumerator DrawRoutine()
    {
        IsDrawing = true;

        // Wait a frame: weapons that are switched off straight after spawning never make a sound
        yield return null;
        PlaySound(BaseData != null ? BaseData.drawSound : null);

        if (animator != null)
        {
            ResetAnimatorTriggers();
            PlayDrawAnimation();
            yield return WaitForAnimation(drawTag, 0f);
        }
        IsDrawing = false;
    }

    // ---------------------------------------------------------------- Hit helpers
    public static IDamageable FindDamageable(Collider col)
    {
        return col != null ? col.GetComponentInParent<IDamageable>() : null;
    }

    protected bool IsIgnored(Collider col)
    {
        return ignoreRoot != null && col.transform.IsChildOf(ignoreRoot);
    }

    /// <summary>Closest hit of a ray (or sphere cast if radius > 0) that isn't the player.</summary>
    protected bool TryGetHit(Vector3 origin, Vector3 dir, float range, float radius, out RaycastHit best)
    {
        int count = radius > 0f
            ? Physics.SphereCastNonAlloc(origin, radius, dir, hitBuffer, range, HitMaskValue, QueryTriggerInteraction.Ignore)
            : Physics.RaycastNonAlloc(origin, dir, hitBuffer, range, HitMaskValue, QueryTriggerInteraction.Ignore);

        Array.Sort(hitBuffer, 0, count, DistanceComparer);

        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (IsIgnored(h.collider)) continue;
            if (h.distance <= 0f && radius > 0f) continue; // sphere started inside something
            best = h;
            return true;
        }

        best = default;
        return false;
    }

    protected void DealDamage(IDamageable target, RaycastHit hit, Vector3 dir, float damage, DamageType type)
    {
        target.TakeDamage(new DamageInfo
        {
            damage = damage,
            point = hit.point,
            direction = dir,
            force = dir * BaseData.impactForce,
            collider = hit.collider,
            source = this,
            type = type
        });
    }

    protected static float ComputeFalloff(float distance, float range, float falloffStart, float damageAtMaxRange)
    {
        if (distance <= falloffStart || range <= falloffStart) return 1f;
        float t = Mathf.Clamp01((distance - falloffStart) / (range - falloffStart));
        return Mathf.Lerp(1f, damageAtMaxRange, t);
    }

    protected static Vector3 ApplySpread(Quaternion rotation, float degrees)
    {
        Vector3 forward = rotation * Vector3.forward;
        if (degrees <= 0f) return forward;
        Vector2 r = UnityEngine.Random.insideUnitCircle * Mathf.Tan(degrees * Mathf.Deg2Rad);
        return (forward + (rotation * Vector3.right) * r.x + (rotation * Vector3.up) * r.y).normalized;
    }

    protected void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }
}