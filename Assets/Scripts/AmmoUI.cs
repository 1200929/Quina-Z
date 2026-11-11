using UnityEngine;
using TMPro;

public class AmmoHUD : MonoBehaviour
{
    public Weapon weapon;
    public TextMeshProUGUI ammoText;

    private void OnEnable()
    {
        if (weapon == null) return;
        weapon.OnAmmoChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (weapon != null) weapon.OnAmmoChanged -= Refresh;
    }

    private void Refresh()
    {
        //"bullets in mag / spare bullets", for example "30 / 150"
        ammoText.text = weapon.AmmoInMagazine + " / " + weapon.ReserveAmmo;
    }
}