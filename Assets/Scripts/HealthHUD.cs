using UnityEngine;
using TMPro;

public class HealthHUD : MonoBehaviour
{
    public PlayerHealth playerHealth;
    public TextMeshProUGUI healthText;

    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.OnHealthChanged -= Refresh;
    }

    public void SetHealth(PlayerHealth newHealth)
    {
        if (playerHealth != null) playerHealth.OnHealthChanged -= Refresh;
        
        playerHealth = newHealth;
        
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += Refresh;
            Refresh();
        }
    }

    private void Refresh()
    {
        if (playerHealth == null) return;
        
        // Mathf.CeilToInt to round up health   
        healthText.text = Mathf.CeilToInt(playerHealth.currentHealth.Value).ToString();
    }
}