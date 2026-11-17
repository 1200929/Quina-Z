using UnityEngine;
using Unity.Netcode;
using System;

// Inherit from NetworkBehaviour AND your existing IDamageable interface
public class PlayerHealth : NetworkBehaviour, IDamageable 
{
    [Header("Stats")]
    public float maxHealth = 100f;

    [Header("Cheats")]
    public bool godMode = false;

    // NetworkVariable syncs
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(100f);

    // Event for the HUD to update the health bar when the health changes
    public event Action OnHealthChanged;

    public override void OnNetworkSpawn()
    {
        // Whenever the server changes the health, update the HUD on all screens
        currentHealth.OnValueChanged += (oldHealth, newHealth) => 
        {
            OnHealthChanged?.Invoke();
            
            if (newHealth <= 0 && IsOwner) 
            {
                Die();
            }
        };
    }

    public void TakeDamage(DamageInfo info)
    {
        if (!IsServer) return; 

        if (godMode) return;

        if (currentHealth.Value > 0)
        {
            currentHealth.Value -= info.damage;
        }
    }

    private void Die()
    {
        Debug.Log("PLAYER DIED! Game Over.");

        // Desativar todos os controaldores do jogador
        var movementScript = GetComponent<PlayerMovement>(); 
        if (movementScript != null) movementScript.enabled = false;

        var controller = GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        var weaponManager = GetComponent<WeaponManager>();
        if (weaponManager != null) weaponManager.enabled = false;

        Weapon[] allWeapons = GetComponentsInChildren<Weapon>(true);
        foreach (Weapon w in allWeapons)
        {
            w.enabled = false;
            w.gameObject.SetActive(false);
        }
        transform.Rotate(new Vector3(0, 0, 90));
    }
}