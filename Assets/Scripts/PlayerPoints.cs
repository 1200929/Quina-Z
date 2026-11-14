using Unity.Netcode;
using UnityEngine;
using TMPro;

public class PlayerPoints : NetworkBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI pointsText;

    // Só o servidor escreve; o dono do jogador lê o resultado
    private NetworkVariable<int> cachedPoints = new NetworkVariable<int>(0); // pontos desta ronda
    private NetworkVariable<int> totalPoints = new NetworkVariable<int>(0);  // só sobem no fim da ronda

    public int CachedPoints => cachedPoints.Value;
    public int TotalPoints => totalPoints.Value;

    public override void OnNetworkSpawn()
    {
        totalPoints.OnValueChanged += OnTotalChanged;
        UpdateUI();
    }

    public override void OnNetworkDespawn()
    {
        totalPoints.OnValueChanged -= OnTotalChanged;
    }

    private void OnTotalChanged(int oldValue, int newValue)
    {
        UpdateUI();
    }

    public void AddPoints(int amount)
    {
        if (!IsServer) return;
        cachedPoints.Value += amount;
    }

    public void CommitPoints()
    {
        if (!IsServer) return;

        if (cachedPoints.Value > 0)
        {
            totalPoints.Value += cachedPoints.Value;
            cachedPoints.Value = 0;
        }
    }

    // Chamado pelo LocalPlayerHUD para o jogador local
    public void SetText(TextMeshProUGUI text)
    {
        pointsText = text;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (pointsText != null)
            pointsText.text = $"Points: {totalPoints.Value}";
    }
}