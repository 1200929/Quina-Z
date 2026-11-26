using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;


// 2. Changed from MonoBehaviour to NetworkBehaviour
public class WeaponManager : NetworkBehaviour
{
    [Header("Setup")]
    public Transform weaponHolder;
    public Weapon[] startingWeapons;

    [Header("Player References")]
    [SerializeField] private AudioSource playerAudioSource;
    [SerializeField] private Camera playerCamera;

    [Header("Viewmodel (optional)")]
    [Tooltip("Layer name for YOUR OWN weapon/arms (e.g. 'Viewmodel'), drawn by a separate viewmodel camera so they don't clip into walls. Leave empty to skip.")]
    public string viewmodelLayerName = "";

    private AmmoHUD localAmmoHUD;
    // Using NetworkVariable so late-joiners instantly see the correct weapon
    public NetworkVariable<int> activeSlotIndex = new NetworkVariable<int>(0);
    private Weapon[] inventory = new Weapon[3];

    /// <summary>The weapon currently in hand (null if the slot is empty).</summary>
    public Weapon ActiveWeapon
    {
        get
        {
            int i = activeSlotIndex.Value;
            return i >= 0 && i < inventory.Length ? inventory[i] : null;
        }
    }

    private void Start()
    {
        activeSlotIndex.OnValueChanged += (oldValue, newValue) =>
        {
            UpdateVisuals(newValue);
        };
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            for (int i = 0; i < startingWeapons.Length && i < 3; i++)
            {
                if (startingWeapons[i] != null)
                {
                    GiveWeapon(startingWeapons[i], i);
                }
            }
        }

        if (IsOwner)
        {
            localAmmoHUD = FindAnyObjectByType<AmmoHUD>();
        }

        UpdateVisuals(activeSlotIndex.Value);
    }

    private void Update()
    {
        if (!IsOwner) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        // Ask the server to swap weapons (weapons that can't be equipped right now, e.g. empty grenades, are skipped)
        if (kb.digit1Key.wasPressedThisFrame) TrySwap(0);
        if (kb.digit2Key.wasPressedThisFrame) TrySwap(1);
        if (kb.digit3Key.wasPressedThisFrame) TrySwap(2);

        // A used-up grenade / spear puts itself away and you go back to your previous weapon
        Weapon active = ActiveWeapon;
        if (active != null && active.WantsToUnequip && Time.time >= nextAutoSwapTime)
        {
            nextAutoSwapTime = Time.time + 0.5f; // don't spam requests while the swap is travelling to the server
            SwapToFallback();
        }
    }

    private int previousSlot = -1;
    private float nextAutoSwapTime;

    private void TrySwap(int slot)
    {
        if (slot < 0 || slot >= inventory.Length || slot == activeSlotIndex.Value) return;

        Weapon target = inventory[slot];
        // Ammo lives on the owner's copy of the weapon, so this check has to happen here, not on the server
        if (target == null || !target.CanEquip) return;

        previousSlot = activeSlotIndex.Value;
        RequestSwapServerRpc(slot);
    }

    private void SwapToFallback()
    {
        int current = activeSlotIndex.Value;

        // Previous weapon first, then any other weapon that can be equipped
        if (previousSlot >= 0 && previousSlot != current
            && inventory[previousSlot] != null && inventory[previousSlot].CanEquip)
        {
            TrySwap(previousSlot);
            return;
        }

        for (int i = 0; i < inventory.Length; i++)
        {
            if (i != current && inventory[i] != null && inventory[i].CanEquip)
            {
                TrySwap(i);
                return;
            }
        }
    }

    [Rpc(SendTo.Server)]
    private void RequestSwapServerRpc(int slotIndex)
    {
        if (inventory[slotIndex] != null)
        {
            // Changing this variable automatically updates everyone's screens
            activeSlotIndex.Value = slotIndex;
        }
    }

    private void UpdateVisuals(int index)
    {
        // Turn off all weapons first
        for (int i = 0; i < inventory.Length; i++)
        {
            if (inventory[i] != null)
            {
                inventory[i].gameObject.SetActive(false);
            }
        }

        // Turn on the requested weapon
        if (inventory[index] != null)
        {
            inventory[index].gameObject.SetActive(true);

            if (IsOwner && localAmmoHUD != null)
            {
                localAmmoHUD.SetWeapon(inventory[index]);
            }
        }
    }

    public void GiveWeapon(Weapon weaponPrefab, int slotIndex)
    {
        if (!IsServer) return;

        if (inventory[slotIndex] != null)
        {
            inventory[slotIndex].GetComponent<NetworkObject>().Despawn();
        }

        Weapon newWeapon = Instantiate(weaponPrefab, weaponHolder.position, weaponHolder.rotation);
        NetworkObject netObj = newWeapon.GetComponent<NetworkObject>();

        // Spawn it on the network
        netObj.SpawnWithOwnership(OwnerClientId);

        netObj.TrySetParent(gameObject, false);

        ReceiveWeaponRpc(netObj.NetworkObjectId, slotIndex);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ReceiveWeaponRpc(ulong weaponNetworkId, int slotIndex)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
            weaponNetworkId, out NetworkObject netObj))
        {
            Weapon newWeapon = netObj.GetComponent<Weapon>();

            // Pass references from THIS player's prefab to the weapon
            newWeapon.aimOrigin = playerCamera != null
                ? playerCamera.transform
                : GetComponentInChildren<Camera>().transform;

            newWeapon.ignoreRoot = transform.root;
            newWeapon.audioSource = playerAudioSource;

            // Own weapon goes on the viewmodel layer
            if (IsOwner && !string.IsNullOrEmpty(viewmodelLayerName))
            {
                int layer = LayerMask.NameToLayer(viewmodelLayerName);

                if (layer >= 0)
                    SetLayerRecursive(newWeapon.transform, layer);
                else
                    Debug.LogWarning(
                        $"Layer '{viewmodelLayerName}' doesn't exist. Create it in Project Settings > Tags and Layers.",
                        this);
            }

            inventory[slotIndex] = newWeapon;
            UpdateVisuals(activeSlotIndex.Value);
        }
    }

    private static void SetLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform child in t)
            SetLayerRecursive(child, layer);
    }

    private void LateUpdate()
    {
        if (!IsOwner) return;
        if (inventory.Length > activeSlotIndex.Value && inventory[activeSlotIndex.Value] != null)
        {
            Weapon activeWeapon = inventory[activeSlotIndex.Value];
            if (activeWeapon.gameObject.activeSelf)
            {
                activeWeapon.transform.position = weaponHolder.position;
                activeWeapon.transform.rotation = weaponHolder.rotation;
            }
        }
    }
}