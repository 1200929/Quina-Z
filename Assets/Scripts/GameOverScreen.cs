using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The end game screen. Put this script on the CANVAS (which stays active) and assign the panel,
/// which is hidden at start and shown when the game is over.
/// </summary>
public class GameOverScreen : MonoBehaviour
{
    [Header("Panel (a child of the canvas, hidden until game over)")]
    public GameObject panel;

    [Header("Texts")]
    public TextMeshProUGUI killsText;
    public TextMeshProUGUI roundText;
    public TextMeshProUGUI timeText;

    [Header("Restart")]
    public Button restartButton;

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void OnEnable()
    {
        GameManager.GameOver += Show;
        if (restartButton != null) restartButton.onClick.AddListener(OnRestartClicked);
    }

    private void OnDisable()
    {
        GameManager.GameOver -= Show;
        if (restartButton != null) restartButton.onClick.RemoveListener(OnRestartClicked);
    }

    private void Show(int zombiesKilled, int round, float secondsSurvived)
    {
        if (killsText != null) killsText.text = "Zombies killed: " + zombiesKilled;
        if (roundText != null) roundText.text = "Round reached: " + round;
        if (timeText != null) timeText.text = "Time survived: " + FormatTime(secondsSurvived);

        if (panel != null) panel.SetActive(true);

        // The player script locks the cursor, so free it to be able to click the button
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnRestartClicked()
    {
        if (restartButton != null) restartButton.interactable = false; // avoid double clicks
        if (GameManager.Instance != null) GameManager.Instance.RequestRestart();
    }

    private static string FormatTime(float seconds)
    {
        TimeSpan t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
    }
}