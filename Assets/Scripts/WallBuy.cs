using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class WallBuy : NetworkBehaviour
{
    [Header("Weapon Settings")]
    public Weapon weaponPrefab;
    public string weaponName = "G3a3";
    public int cost = 1300;

    private bool isLocalPlayerNearby = false;

    private LocalPlayerHUD localHUD; 

    private void OnTriggerEnter(Collider other)
    {
        var weaponManager = other.GetComponentInParent<WeaponManager>();
        
        if (weaponManager != null && weaponManager.IsOwner)
        {
            isLocalPlayerNearby = true;
            
            // Mandar info para o HUD do jogador local (se nao aparece para todos os jogadores o texto para comprar a arma)
            localHUD = FindAnyObjectByType<LocalPlayerHUD>();
            
            if (localHUD != null)
            {
                localHUD.ShowPrompt($"Press [F] to buy {weaponName} [{cost}]");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var weaponManager = other.GetComponentInParent<WeaponManager>();
        
        if (weaponManager != null && weaponManager.IsOwner)
        {
            isLocalPlayerNearby = false;
            
            if (localHUD != null)
            {
                localHUD.HidePrompt();
                localHUD = null;
            }
        }
    }

    private void Update()
    {
        if (!isLocalPlayerNearby) return;

        var kb = Keyboard.current;
        if (kb != null && kb.fKey.wasPressedThisFrame)
        {
            // Manda um pedido ao servidor para comprar a arma.
            RequestBuyServerRpc(NetworkManager.Singleton.LocalClientId);
        }
    }

    [Rpc(SendTo.Server)]
    private void RequestBuyServerRpc(ulong clientId)
    {
        // Encontrar o jogador que fez a compra e verificar se ele tem pontos suficientes para comprar a arma.
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
        {
            var player = client.PlayerObject;
            var points = player.GetComponent<PlayerPoints>();
            var weaponManager = player.GetComponent<WeaponManager>();

            if (points != null && weaponManager != null)
            {
                if (points.TrySpendPoints(cost))
                {
                    // POR ENQUANTO, substituir a arma atual do jogador pela nova arma comprada.
                    int currentSlot = weaponManager.activeSlotIndex.Value;
                    weaponManager.GiveWeapon(weaponPrefab, currentSlot);
                }
            }
        }
    }
}