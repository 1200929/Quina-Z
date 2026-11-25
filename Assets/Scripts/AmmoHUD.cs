using UnityEngine;
using TMPro;

public class AmmoHUD : MonoBehaviour
{
    public Weapon weapon;
    public TextMeshProUGUI ammoText;
    public TextMeshProUGUI weaponNameText;

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
        if (weapon == null || weapon.BaseData == null) return;

        // Each weapon type decides what to show: "30 / 150" (gun), item count (grenade/spear), empty (melee)
        if (ammoText != null)
            ammoText.text = weapon.GetAmmoText();

        if (weaponNameText != null)
            weaponNameText.text = weapon.BaseData.weaponName;
    }
}