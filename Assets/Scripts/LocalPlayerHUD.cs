using Unity.Netcode;
using UnityEngine;
using TMPro;

public class LocalPlayerHUD : MonoBehaviour
{
    public AmmoHUD ammoHUD;
    public HealthHUD healthHUD;
    public TextMeshProUGUI pointsText;

    private bool bound;

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
}