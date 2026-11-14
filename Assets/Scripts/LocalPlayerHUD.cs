using Unity.Netcode;
using UnityEngine;
using TMPro;

public class LocalPlayerHUD : MonoBehaviour
{
    public AmmoHUD ammoHUD;
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

        if (weapon != null) ammoHUD.SetWeapon(weapon);
        if (points != null) points.SetText(pointsText);

        bound = true;
    }
}