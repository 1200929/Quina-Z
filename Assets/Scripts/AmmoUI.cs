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

    // Chamado quando o jogador local é criado
    public void SetWeapon(Weapon newWeapon)
    {
        if (weapon != null) weapon.OnAmmoChanged -= Refresh;
        weapon = newWeapon;
        if (weapon != null)
        {
            weapon.OnAmmoChanged += Refresh;
            Refresh();
        }
    }

    private void Refresh()
    {
        ammoText.text = weapon.AmmoInMagazine + " / " + weapon.ReserveAmmo;
    }
}