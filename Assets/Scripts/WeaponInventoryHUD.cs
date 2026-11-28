using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows the icon of every weapon the local player carries, one per slot (usually along the right edge of the screen).
/// The weapon in hand is highlighted, and weapons that can't be equipped right now (e.g. grenades at 0) are dimmed.
///
/// Setup: create a vertical group on the right of the canvas, one child per weapon slot, each with an Image
/// for the icon (and optionally an Image for a background frame). Assign them below, in slot order (slot 1 first).
/// Put this script on any object on the canvas. The icon of each weapon comes from WeaponData.icon.
/// </summary>
public class WeaponInventoryHUD : MonoBehaviour
{
    [Header("Slots (same order as the 1/2/3 keys)")]
    public Image[] slotIcons = new Image[3];
    [Tooltip("Optional frame behind each icon, tinted to show which slot is selected.")]
    public Image[] slotBackgrounds = new Image[3];

    [Header("Look")]
    public Color activeIconColor = Color.white;
    public Color inactiveIconColor = new Color(1f, 1f, 1f, 0.55f);
    [Tooltip("Icon colour when the weapon can't be equipped right now (empty grenades / spears).")]
    public Color unavailableIconColor = new Color(1f, 1f, 1f, 0.2f);
    public Color activeBackgroundColor = new Color(1f, 1f, 1f, 0.35f);
    public Color inactiveBackgroundColor = new Color(0f, 0f, 0f, 0.35f);
    [Tooltip("Size multiplier of the selected slot's icon.")]
    public float activeScale = 1.15f;

    [Header("Labels (created automatically on each icon if left empty)")]
    [Tooltip("Slot number, shown at the middle right of the icon.")]
    public TextMeshProUGUI[] slotNumbers = new TextMeshProUGUI[3];
    [Tooltip("Ammo count, shown at the bottom left of the icon.")]
    public TextMeshProUGUI[] slotAmmoTexts = new TextMeshProUGUI[3];
    public float numberFontSize = 18f;
    public float ammoFontSize = 16f;
    public Color labelColor = Color.white;
    [Tooltip("Distance of the labels from the icon's edges, in pixels.")]
    public Vector2 labelPadding = new Vector2(4f, 4f);

    private WeaponManager manager;

    private void Awake()
    {
        EnsureArraySize(ref slotNumbers);
        EnsureArraySize(ref slotAmmoTexts);

        for (int i = 0; i < slotIcons.Length; i++)
        {
            if (slotIcons[i] == null) continue;

            if (slotNumbers[i] == null)
                slotNumbers[i] = CreateLabel(slotIcons[i].transform, "SlotNumber",
                    new Vector2(1f, 0.5f), TextAlignmentOptions.MidlineRight, numberFontSize,
                    new Vector2(-labelPadding.x, 0f));

            if (slotAmmoTexts[i] == null)
                slotAmmoTexts[i] = CreateLabel(slotIcons[i].transform, "SlotAmmo",
                    new Vector2(0f, 0f), TextAlignmentOptions.BottomLeft, ammoFontSize,
                    new Vector2(labelPadding.x, labelPadding.y));

            slotNumbers[i].text = (i + 1).ToString();
        }
    }

    private void EnsureArraySize(ref TextMeshProUGUI[] array)
    {
        if (array != null && array.Length >= slotIcons.Length) return;
        var resized = new TextMeshProUGUI[slotIcons.Length];
        if (array != null) System.Array.Copy(array, resized, array.Length);
        array = resized;
    }

    // A text pinned to one point of the icon (anchor = pivot), so it sits exactly at that corner / edge
    private TextMeshProUGUI CreateLabel(Transform parent, string name, Vector2 anchor,
                                        TextAlignmentOptions alignment, float fontSize, Vector2 offset)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = offset;
        rect.sizeDelta = new Vector2(100f, fontSize * 1.4f);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.alignment = alignment;
        text.fontSize = fontSize;
        text.color = labelColor;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private void Update()
    {
        // (Re)find the local player's WeaponManager, also after a restart creates a new player object
        if (manager == null)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) { HideAll(); return; }

            var playerObj = nm.LocalClient?.PlayerObject;
            if (playerObj == null) { HideAll(); return; }

            manager = playerObj.GetComponent<WeaponManager>();
            if (manager == null) { HideAll(); return; }
        }

        for (int i = 0; i < slotIcons.Length; i++)
            RefreshSlot(i);
    }

    private void RefreshSlot(int slot)
    {
        Image icon = slotIcons[slot];
        if (icon == null) return;

        Image background = slot < slotBackgrounds.Length ? slotBackgrounds[slot] : null;

        Weapon weapon = manager.GetWeapon(slot);
        Sprite sprite = weapon != null && weapon.BaseData != null ? weapon.BaseData.icon : null;

        // Empty slot (or a weapon without an icon): hide it
        bool visible = weapon != null && sprite != null;
        icon.gameObject.SetActive(visible);
        if (background != null) background.gameObject.SetActive(weapon != null);
        if (!visible) return;

        bool active = slot == manager.ActiveSlot;

        icon.sprite = sprite;
        icon.preserveAspect = true;
        icon.color = !weapon.CanEquip ? unavailableIconColor
                   : active ? activeIconColor
                   : inactiveIconColor;
        icon.rectTransform.localScale = Vector3.one * (active ? activeScale : 1f);

        if (background != null)
            background.color = active ? activeBackgroundColor : inactiveBackgroundColor;

        // Ammo, e.g. "30 / 150" (empty for melee, so nothing is shown)
        if (slotAmmoTexts[slot] != null)
            slotAmmoTexts[slot].text = weapon.GetAmmoText();
    }

    private void HideAll()
    {
        for (int i = 0; i < slotIcons.Length; i++)
        {
            if (slotIcons[i] != null) slotIcons[i].gameObject.SetActive(false);
            if (i < slotBackgrounds.Length && slotBackgrounds[i] != null) slotBackgrounds[i].gameObject.SetActive(false);
        }
    }
}