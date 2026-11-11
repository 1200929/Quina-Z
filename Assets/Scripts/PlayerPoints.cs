using UnityEngine;
using TMPro;
public class PlayerPoints : MonoBehaviour
{
    [Header("Point Tracking")]
    public int cachedPoints; // Points earned this round
    public int totalPoints;  // Points only added at the end of the round

    [Header("UI")]
    public TextMeshProUGUI pointsText;


    private void Start()
    {
        UpdateUI();
    }
    public void AddPoints(int amount)
    {
        cachedPoints += amount;
    }

    public void CommitPoints()
    {
        if (cachedPoints > 0)
        {
            totalPoints += cachedPoints;
            cachedPoints = 0;
            UpdateUI();
        }
    }
    private void UpdateUI()
    {
        if (pointsText != null) 
        {
            pointsText.text = $"Points: {totalPoints}";
        }
    }
}
