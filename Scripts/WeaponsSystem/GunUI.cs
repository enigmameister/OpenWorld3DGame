using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MountedSniperStation;
using static WeaponItemData;

public class GunUI : MonoBehaviour
{
    [Header("Crosshair / Ammo")]
    private bool _lastCrosshairVisible = true;
    public GameObject crosshairUI;
    private Image[] _crosshairImages;

    public TextMeshProUGUI ammoText;


    [Header("Refs")]
    public WeaponManager weaponManager;
    public PlayerStats playerStats;

    [Header("Bullet Icons UI")]
    public GameObject bulletContainer;
    public GameObject bulletIconPrefab;

    [Header("Sniper cooldown")]
    public Image sniperCooldownBar;
    public GameObject sniperCooldownRoot;

    [Serializable]
    public class WeaponUIEntry
    {
        public WeaponSlot slotType;
        public GameObject uiSlot;
        // dla granatów wpisz nazwę prefaba / itemu (np. "Grenade", "Smoke", "Flash")
        public string itemKey;
    }

    [Header("Dynamic Weapon UI")]
    public List<WeaponUIEntry> weaponUIEntries = new();

    // ===== Cache =====
    private readonly Dictionary<WeaponSlot, WeaponUIEntry> _singleSlot = new();                     // Melees / Pistols / Riffles
    private readonly Dictionary<string, WeaponUIEntry> _nadesByKey = new(StringComparer.OrdinalIgnoreCase); // Nades
    private readonly Dictionary<GameObject, Image> _imgCache = new();
    private readonly Dictionary<GameObject, GameObject> _borderCache = new();
    private readonly Dictionary<GameObject, TextMeshProUGUI> _countCache = new();

    private readonly List<GameObject> _bulletIconPool = new();
    private int _lastBulletIconCount = -1;

    private Gun _currentGun;
    private float _sniperCooldownTimer;
    private float _sniperCooldownMax;

    // do „ticku” granatów (zapobiega spamowi assignów)
    private int _lastActiveNadeCount = int.MinValue;
    private string _lastActiveNadeKey = null;

    private int _lastAmmoCurrent = -999;
    private int _lastAmmoTotal = -999;
    private bool _lastAmmoVisible = true;
    private bool _lastBulletContainerVisible = true;

    // ===== Unity =====
    void Awake()
    {
        ResolveReferences();
        BuildMaps();
        CacheCrosshairImages();
    }

    void Update()
    {
        if (playerStats?.IsDead == true)
        {
            if (ammoText) ammoText.text = "";
            if (sniperCooldownRoot) sniperCooldownRoot.SetActive(false);
            return;
        }

        if (MountedSniperState.IsActive)
        {
            UpdateCrosshairUI();
            UpdateAmmoDisplay();
            HideAllWeaponSlots();
            UpdateSniperCooldownUI();
            return;
        }

        UpdateCurrentGun();
        UpdateCrosshairUI();
        UpdateAmmoDisplay();
        UpdateSniperCooldownUI();

        TickNadesBadge();
    }

    private void HideAllWeaponSlots()
    {
        foreach (var e in weaponUIEntries)
        {
            if (e?.uiSlot == null) continue;

            if (e.uiSlot.activeSelf)
                e.uiSlot.SetActive(false);

            SetBorder(e.uiSlot, false);
            SetCountText(e.uiSlot, "", false);
        }
    }

    private void ResolveReferences()
    {
        if (weaponManager == null)
            weaponManager = FindFirstObjectByType<WeaponManager>();

        if (playerStats == null)
            playerStats = FindFirstObjectByType<PlayerStats>();
    }

    private void SetAmmoTextVisible(bool visible)
    {
        if (ammoText == null) return;

        if (_lastAmmoVisible == visible)
            return;

        ammoText.gameObject.SetActive(visible);
        _lastAmmoVisible = visible;
    }

    private void SetBulletContainerVisible(bool visible)
    {
        if (bulletContainer == null) return;

        if (_lastBulletContainerVisible == visible)
            return;

        bulletContainer.SetActive(visible);
        _lastBulletContainerVisible = visible;
    }

    public void StopSniperCooldown()
    {
        // UI może już nie istnieć podczas stopowania gry itp.
        if (!this || !gameObject || !isActiveAndEnabled) return;

        _sniperCooldownTimer = 0f;
        _sniperCooldownMax = 0f;

        if (sniperCooldownBar) sniperCooldownBar.fillAmount = 0f;

        // użyj przypiętego root-a, a gdy go brak – rodzica obrazka
        var root = sniperCooldownRoot ? sniperCooldownRoot
                                      : sniperCooldownBar ? sniperCooldownBar.transform.parent?.gameObject
                                                          : null;
        if (root) root.SetActive(false);
    }

    private void CacheCrosshairImages()
    {
        if (crosshairUI == null)
        {
            _crosshairImages = null;
            return;
        }

        _crosshairImages = crosshairUI.GetComponentsInChildren<Image>(true);
    }

    private void BuildMaps()
    {
        _singleSlot.Clear();
        _nadesByKey.Clear();

        foreach (var e in weaponUIEntries)
        {
            if (e == null || e.uiSlot == null) continue;

            CacheImage(e.uiSlot);
            CacheBorder(e.uiSlot);
            CacheCount(e.uiSlot);

            if (e.slotType == WeaponSlot.Nades)
            {
                var key = !string.IsNullOrWhiteSpace(e.itemKey) ? e.itemKey.Trim() : e.uiSlot.name;
                _nadesByKey[key] = e;
            }
            else
            {
                _singleSlot[e.slotType] = e;
            }

            // domyślnie wygaszamy
            e.uiSlot.SetActive(false);
            SetBorder(e.uiSlot, false);
            SetCountText(e.uiSlot, "", false);
        }
    }

    private Image CacheImage(GameObject slot)
    {
        if (slot == null) return null;
        if (_imgCache.TryGetValue(slot, out var img)) return img;
        img = slot.GetComponent<Image>();
        _imgCache[slot] = img;
        return img;
    }
    private GameObject CacheBorder(GameObject slot)
    {
        if (slot == null) return null;
        if (_borderCache.TryGetValue(slot, out var b)) return b;
        var t = slot.transform.Find("Border");
        b = t ? t.gameObject : null;
        _borderCache[slot] = b;
        return b;
    }
    private TextMeshProUGUI CacheCount(GameObject slot)
    {
        if (slot == null) return null;
        if (_countCache.TryGetValue(slot, out var c)) return c;
        c = slot.GetComponentsInChildren<TextMeshProUGUI>(true)
                .FirstOrDefault(t => t.name.Equals("Count", StringComparison.OrdinalIgnoreCase));
        _countCache[slot] = c;
        return c;
    }

    // ===== Per-frame UI =====
    private static string CountSuffix(int count) => count >= 2 ? $"x{count}" : string.Empty;

    private void UpdateCurrentGun()
    {
        _currentGun = null;
        if (weaponManager == null) return;

        int idx = weaponManager.GetCurrentWeaponIndex();
        if (idx < 0) return;

        var slots = weaponManager.GetWeaponSlots();
        if (slots != null && idx < slots.Length && slots[idx] != null)
            _currentGun = slots[idx].GetComponentInChildren<Gun>(true);
    }

    private void UpdateCrosshairUI()
    {
        if (crosshairUI == null) return;

        if (MountedSniperState.IsActive)
        {
            if (crosshairUI.activeSelf)
                crosshairUI.SetActive(false);

            _lastCrosshairVisible = false;
            return;
        }

        if (!crosshairUI.activeSelf)
            crosshairUI.SetActive(true);

        bool show =
        !MountedSniperState.IsActive &&
        weaponManager != null &&
        !weaponManager.IsUsingHandsOnly() &&
        _currentGun != null &&
        !_currentGun.IsSniperWeapon();

        if (_crosshairImages == null || _crosshairImages.Length == 0)
            CacheCrosshairImages();

        if (_lastCrosshairVisible == show)
            return;

        for (int i = 0; i < _crosshairImages.Length; i++)
        {
            if (_crosshairImages[i] != null)
                _crosshairImages[i].enabled = show;
        }

        _lastCrosshairVisible = show;
    }

    private void UpdateAmmoDisplay()
    {
        if (MountedSniperState.IsActive)
        {
            SetAmmoTextVisible(false);
            SetBulletContainerVisible(false);
            return;
        }

        if (ammoText == null) return;

        // ⛔ ręce → chowamy ammo i ikonki kul
        if (weaponManager != null && weaponManager.IsUsingHandsOnly())
        {
            SetAmmoTextVisible(false);
            SetBulletContainerVisible(false);
            return;
        }

        if (_currentGun != null)
        {
            SetAmmoTextVisible(true);
            SetBulletContainerVisible(true);

            int cur = _currentGun.GetCurrentAmmo();
            int tot = _currentGun.GetTotalAmmo();

            if (cur != _lastAmmoCurrent || tot != _lastAmmoTotal)
            {
                ammoText.text = $"{cur} | {tot}";
                UpdateBulletIcons(cur);

                _lastAmmoCurrent = cur;
                _lastAmmoTotal = tot;
            }

            return;
        }

        // aktywny slot granatu
        if (weaponManager != null && weaponManager.GetCurrentWeaponIndex() == 3)
        {
            SetAmmoTextVisible(true);
            SetBulletContainerVisible(false);

            var grenade = weaponManager.GetCurrentWeaponSlotObject()?.GetComponentInChildren<Grenade>(true);
            var inst = grenade?.GetInstance();

            int count = 0;

            if (inst != null && inst.data != null && InventoryUI.Instance != null)
            {
                count = InventoryUI.Instance.GetTotalCountForData(inst.data);
            }
            else if (inst != null)
            {
                count = inst.count;
            }

            if (count != _lastAmmoCurrent || _lastAmmoTotal != -1)
            {
                ammoText.text = count.ToString();

                _lastAmmoCurrent = count;
                _lastAmmoTotal = -1;
            }

            return;
        }

        SetAmmoTextVisible(false);
        SetBulletContainerVisible(false);
    }

    private void UpdateSniperCooldownUI()
    {
        if (sniperCooldownBar == null) return;
        var root = sniperCooldownRoot ? sniperCooldownRoot : sniperCooldownBar.transform.parent?.gameObject;
        if (root == null) return;

        if (_sniperCooldownTimer > 0f)
        {
            _sniperCooldownTimer -= Time.deltaTime;
            sniperCooldownBar.fillAmount = Mathf.Clamp01(1f - (_sniperCooldownTimer / _sniperCooldownMax));
            root.SetActive(true);
        }
        else
        {
            sniperCooldownBar.fillAmount = 0f;
            root.SetActive(false);
        }
    }

    // ===== Public API (z WM/Inventory) =====
    public void StartSniperCooldown(float duration)
    {
        _sniperCooldownMax = duration;
        _sniperCooldownTimer = duration;
    }

    /// Wywołuj po pick‑upie, switchu, dropie, zmianie stacku itp.
    public void UpdateWeaponHUD(List<InventoryItemInstance> all, InventoryItemInstance current)
    {
        if (_singleSlot.Count == 0 && _nadesByKey.Count == 0) BuildMaps();

        // 0) zgaś wszystko
        foreach (var e in weaponUIEntries)
        {
            if (e?.uiSlot == null) continue;
            e.uiSlot.SetActive(false);
            SetBorder(e.uiSlot, false);
            SetCountText(e.uiSlot, "", false);
        }

        all ??= new List<InventoryItemInstance>();

        // 1) aktywny slot (źródło prawdy = WeaponManager)
        WeaponSlot activeSlot = (WeaponSlot)(-1);
        if (weaponManager != null)
        {
            int idx = weaponManager.GetCurrentWeaponIndex();
            bool hands = weaponManager.IsUsingHandsOnly();
            if (!hands && idx >= 0)
            {
                activeSlot = idx switch
                {
                    0 => WeaponSlot.Melees,
                    1 => WeaponSlot.Pistols,
                    2 => WeaponSlot.Riffles,
                    3 => WeaponSlot.Nades,
                    _ => (WeaponSlot)(-1)
                };
            }
        }

        _lastActiveNadeKey = null; // zaktualizujemy w pętli

        // 2) Najpierw zsumuj granaty po typie.
        // Dzięki temu np. Grenade x2 + Grenade x2 pokaże jako x4.
        Dictionary<string, NadeAggregate> nadeTotals =
            new Dictionary<string, NadeAggregate>(StringComparer.OrdinalIgnoreCase);

        // 3) włącz posiadane bronie nie-stackowane, a granaty zapisz do agregacji
        foreach (var inst in all)
        {
            if (inst?.data == null) continue;

            if (inst.data is not WeaponItemData &&
                inst.data is not MeleeItemData &&
                inst.data is not GrenadeItemData)
                continue;

            var slot = inst.data.GetWeaponSlot();

            if (slot != WeaponSlot.Nades)
            {
                if (_singleSlot.TryGetValue(slot, out var entry) && entry.uiSlot != null)
                {
                    entry.uiSlot.SetActive(true);
                    SetIcon(entry.uiSlot, inst.data.icon);
                    SetBorder(entry.uiSlot, slot == activeSlot);
                }

                continue;
            }

            string key = GetItemKey(inst.data);

            if (!nadeTotals.TryGetValue(key, out var aggregate))
            {
                aggregate = new NadeAggregate
                {
                    representative = inst,
                    count = 0
                };

                nadeTotals[key] = aggregate;
            }

            aggregate.count += Mathf.Max(1, inst.count);
        }

        // 4) Teraz pokaż granaty zsumowane
        foreach (var pair in nadeTotals)
        {
            string key = pair.Key;
            NadeAggregate aggregate = pair.Value;

            if (aggregate == null || aggregate.representative == null)
                continue;

            if (!_nadesByKey.TryGetValue(key, out var nadesEntry))
            {
                // fallback: znajdź pierwszy wpis Nades bez itemKey albo z pustym itemKey
                nadesEntry = weaponUIEntries.FirstOrDefault(e =>
                    e != null &&
                    e.slotType == WeaponSlot.Nades &&
                    e.uiSlot != null &&
                    string.IsNullOrWhiteSpace(e.itemKey)
                );
            }

            if (nadesEntry == null || nadesEntry.uiSlot == null)
                continue;

            nadesEntry.uiSlot.SetActive(true);
            SetIcon(nadesEntry.uiSlot, aggregate.representative.data.icon);

            string text = CountSuffix(aggregate.count);
            SetCountText(nadesEntry.uiSlot, text, !string.IsNullOrEmpty(text));

            bool isActive = (activeSlot == WeaponSlot.Nades) && IsActiveGrenadeType(key);
            SetBorder(nadesEntry.uiSlot, isActive);

            if (isActive)
            {
                _lastActiveNadeKey = key;
                _lastActiveNadeCount = aggregate.count;
            }
        }

        // po tej metodzie TickNadesBadge() dociągnie „prawdziwy” count aktywnego granatu,
        // gdy Inventory UI podbije stack chwilę po PickUpWeapon().
    }

    // ===== Helpers =====
    private static string GetItemKey(InventoryItemData data)
    {
        return data?.prefab != null ? data.prefab.name : data?.itemName ?? "";
    }

    private bool IsActiveGrenadeType(string key)
    {
        if (weaponManager == null) return false;
        if (weaponManager.IsUsingHandsOnly()) return false;
        if (weaponManager.GetCurrentWeaponIndex() != 3) return false;

        // aktywny obiekt granatu → jego klucz
        var grenade = weaponManager.GetCurrentWeaponSlotObject()?.GetComponentInChildren<Grenade>(true);
        var data = grenade?.GetInstance()?.data;
        if (data == null) return false;

        string activeKey = GetItemKey(data);
        return string.Equals(activeKey, key, StringComparison.OrdinalIgnoreCase);
    }

    private void TickNadesBadge()
    {
        // działa tylko gdy aktywny jest slot 3 (granaty)
        if (weaponManager == null) return;
        if (weaponManager.IsUsingHandsOnly()) return;
        if (weaponManager.GetCurrentWeaponIndex() != 3) return;

        var grenade = weaponManager.GetCurrentWeaponSlotObject()?.GetComponentInChildren<Grenade>(true);
        var inst = grenade?.GetInstance();
        if (inst?.data == null) return;

        string key = GetItemKey(inst.data);

        int count = inst.count;

        // WAŻNE:
        // Jeżeli stack granatów jest rozdzielony w InventoryUI,
        // GunUI ma pokazywać sumę wszystkich stacków tego samego typu.
        if (InventoryUI.Instance != null)
            count = InventoryUI.Instance.GetTotalCountForData(inst.data);

        // jeśli nic się nie zmieniło — nic nie rób
        if (count == _lastActiveNadeCount &&
            string.Equals(_lastActiveNadeKey, key, StringComparison.OrdinalIgnoreCase))
            return;

        _lastActiveNadeCount = count;
        _lastActiveNadeKey = key;

        if (_nadesByKey.TryGetValue(key, out var entry) && entry.uiSlot != null)
        {
            entry.uiSlot.SetActive(count > 0);
            SetIcon(entry.uiSlot, inst.data.icon);

            string text = CountSuffix(count);
            SetCountText(entry.uiSlot, text, !string.IsNullOrEmpty(text));

            SetBorder(entry.uiSlot, count > 0);
        }
    }

    private void SetIcon(GameObject slot, Sprite sprite)
    {
        var img = CacheImage(slot);
        if (img != null) img.sprite = sprite;
    }
    private void SetBorder(GameObject slot, bool on)
    {
        var b = CacheBorder(slot);
        if (b != null) b.SetActive(on);
    }
    private void SetCountText(GameObject slot, string text, bool visible)
    {
        var tmp = CacheCount(slot);
        if (tmp == null) return;
        tmp.text = text ?? "";
        tmp.gameObject.SetActive(visible);
    }

    public void UpdateBulletIcons(int count)
    {
        if (bulletContainer == null || bulletIconPrefab == null)
            return;

        count = Mathf.Max(0, count);

        if (_lastBulletIconCount == count)
            return;

        EnsureBulletIconPool(count);

        for (int i = 0; i < _bulletIconPool.Count; i++)
        {
            bool shouldShow = i < count;

            if (_bulletIconPool[i] != null &&
                _bulletIconPool[i].activeSelf != shouldShow)
            {
                _bulletIconPool[i].SetActive(shouldShow);   
            }
        }

        _lastBulletIconCount = count;
    }

    private void EnsureBulletIconPool(int neededCount)
    {
        while (_bulletIconPool.Count < neededCount)
        {
            GameObject icon = Instantiate(bulletIconPrefab, bulletContainer.transform);
            icon.SetActive(false);
            _bulletIconPool.Add(icon);
        }
    }
    private class NadeAggregate
    {
        public InventoryItemInstance representative;
        public int count;
    }
}
