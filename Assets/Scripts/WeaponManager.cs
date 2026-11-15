using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode; // 1. Added Netcode library


// 2. Changed from MonoBehaviour to NetworkBehaviour
public class WeaponManager : NetworkBehaviour 
{
    [Header("Setup")]
    public Transform weaponHolder; 
    public Weapon[] startingWeapons; 

    private AmmoHUD localAmmoHUD;
    // Using NetworkVariable so late-joiners instantly see the correct weapon
    public NetworkVariable<int> activeSlotIndex = new NetworkVariable<int>(0);
    private Weapon[] inventory = new Weapon[2];

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
            for (int i = 0; i < startingWeapons.Length && i < 2; i++)
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

        // Ask the server to swap weapons
        if (kb.digit1Key.wasPressedThisFrame) RequestSwapServerRpc(0);
        if (kb.digit2Key.wasPressedThisFrame) RequestSwapServerRpc(1);
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
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(weaponNetworkId, out NetworkObject netObj))
        {
            Weapon newWeapon = netObj.GetComponent<Weapon>();
            newWeapon.aimOrigin = GetComponentInChildren<Camera>().transform;
            newWeapon.ignoreRoot = transform.root; 
            inventory[slotIndex] = newWeapon;
            UpdateVisuals(activeSlotIndex.Value);
        }
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