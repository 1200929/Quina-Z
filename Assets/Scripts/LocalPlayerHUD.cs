using Unity.Netcode;
using UnityEngine;
using TMPro;

public class LocalPlayerHUD : MonoBehaviour
{
    public AmmoHUD ammoHUD;
    public HealthHUD healthHUD;
    public TextMeshProUGUI pointsText;

    [Header("Interaction Prompts")]
    public TextMeshProUGUI interactionPromptText;

    private bool bound;

    private void Start()
    {
        if (interactionPromptText != null) interactionPromptText.gameObject.SetActive(false);
    }
    private void Update()
    {
        if (bound) return;

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return;

        var playerObj = nm.LocalClient?.PlayerObject;
        if (playerObj == null) return;

        var weapon = playerObj.GetComponentInChildren<Weapon>();
        var points = playerObj.GetComponent<PlayerPoints>();
        var health = playerObj.GetComponent<PlayerHealth>();

        if (weapon != null && ammoHUD != null) ammoHUD.SetWeapon(weapon);
        if (points != null && pointsText != null) points.SetText(pointsText);
        if (health != null && healthHUD != null) healthHUD.SetHealth(health);
        bound = true;
    }

    public void ShowPrompt(string message)
    {
        if (interactionPromptText != null)
        {
            interactionPromptText.text = message;
            interactionPromptText.gameObject.SetActive(true);
        }
    }

    public void HidePrompt()
    {
        if (interactionPromptText != null) interactionPromptText.gameObject.SetActive(false);
    }
}