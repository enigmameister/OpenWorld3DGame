using System.Collections.Generic;
using UnityEngine;

public class MetalDetectorGate : MonoBehaviour
{
    [Header("Zasilanie bramki")]
    [Tooltip("Czy bramka działa? Gdy false, światła wyłączone + brak detekcji.")]
    public bool hasPower = true;

    [Header("Detekcja")]
    [Tooltip("Warstwy traktowane jako \"nielegalne\" w świecie (dropy itd.)")]
    public LayerMask forbiddenLayers;          // np. Weapon
    [Tooltip("Tag gracza")]
    public string playerTag = "Player";

    [Header("Światła")]
    public Light[] indicatorLights;
    public Color idleColor = Color.black;
    public Color safeColor = Color.green;
    public Color alarmColor = Color.red;

    [Header("Czas podtrzymania koloru")]
    public float holdTime = 15f;

    [Header("Miganie alarmu")]
    [Tooltip("Co ile sekund ma migać czerwone światło.")]
    public float blinkInterval = 0.3f;

    // runtime
    private readonly HashSet<GameObject> worldForbiddenInside = new();
    private readonly HashSet<GameObject> playersInside = new();

    private float holdTimer = 0f;
    private Color currentColor;

    // blinking
    private float blinkTimer = 0f;
    private bool blinkState = true;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;

        forbiddenLayers = LayerMask.GetMask("Weapon");
        indicatorLights = GetComponentsInChildren<Light>(true);
    }

    void Awake()
    {
        if (indicatorLights == null || indicatorLights.Length == 0)
            indicatorLights = GetComponentsInChildren<Light>(true);

        SetLights(idleColor);
        blinkTimer = blinkInterval;
    }

    void Update()
    {
        if (!hasPower)
        {
            ForceLightsOff();
            return;
        }

        // wygaszanie po czasie
        if (holdTimer > 0f)
        {
            holdTimer -= Time.deltaTime;

            if (holdTimer <= 0f &&
                worldForbiddenInside.Count == 0 &&
                playersInside.Count == 0)
            {
                SetLights(idleColor);
            }
        }

        HandleBlinking();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!hasPower) return;

        var root = other.attachedRigidbody
            ? other.attachedRigidbody.gameObject
            : other.gameObject;

        // dropy
        if (IsForbiddenLayer(root.layer))
            worldForbiddenInside.Add(root);

        // gracz
        if (root.CompareTag(playerTag))
            playersInside.Add(root);

        RefreshState();
    }

    void OnTriggerExit(Collider other)
    {
        if (!hasPower) return;

        var root = other.attachedRigidbody
            ? other.attachedRigidbody.gameObject
            : other.gameObject;

        if (IsForbiddenLayer(root.layer))
            worldForbiddenInside.Remove(root);

        if (root.CompareTag(playerTag))
            playersInside.Remove(root);

        RefreshState();
    }

    bool IsForbiddenLayer(int layer)
    {
        return (forbiddenLayers.value & (1 << layer)) != 0;
    }

    bool AnyPlayerHasForbiddenInventory()
    {
        foreach (var player in playersInside)
        {
            if (!player) continue;

            var wm = player.GetComponentInChildren<WeaponManager>();
            InventoryUI inv = null;

            if (wm != null && wm.inventoryUI != null)
                inv = wm.inventoryUI;
            else
                inv = InventoryUI.Instance;

            if (inv != null && inv.HasAnyItemOnLayer(forbiddenLayers))
                return true;
        }

        return false;
    }

    void RefreshState()
    {
        if (!hasPower)
        {
            ForceLightsOff();
            return;
        }

        bool hasWorldForbidden = worldForbiddenInside.Count > 0;
        bool hasPlayer = playersInside.Count > 0;
        bool hasInvForbidden = AnyPlayerHasForbiddenInventory();

        if (hasWorldForbidden || hasInvForbidden)
        {
            // 🔴 alarm
            SetLights(alarmColor);
            holdTimer = holdTime;
        }
        else if (hasPlayer)
        {
            // 🟢 gracz czysty
            SetLights(safeColor);
            holdTimer = holdTime;
        }
        else
        {
            if (holdTimer <= 0f)
                SetLights(idleColor);
        }
    }

    void SetLights(Color c)
    {
        currentColor = c;
        blinkTimer = blinkInterval;
        blinkState = true;

        UpdateLightsImmediate();
    }

    void HandleBlinking()
    {
        if (!hasPower)
        {
            ForceLightsOff();
            return;
        }

        // migamy TYLKO gdy alarm (czerwony)
        if (currentColor != alarmColor)
        {
            // zielony / czarny = stały
            blinkState = true;
            UpdateLightsImmediate();
            return;
        }

        blinkTimer -= Time.deltaTime;
        if (blinkTimer <= 0f)
        {
            blinkTimer = blinkInterval;
            blinkState = !blinkState;
            UpdateLightsImmediate();
        }
    }

    void UpdateLightsImmediate()
    {
        bool shouldBeOn = blinkState && hasPower;

        foreach (var l in indicatorLights)
        {
            if (!l) continue;

            l.color = currentColor;

            // czerwony: miganie
            // zielony / czarny: zawsze ON jeśli ma power i nie idle
            if (currentColor == alarmColor)
                l.enabled = shouldBeOn;
            else
                l.enabled = (currentColor != idleColor) && hasPower;
        }
    }
    
    void ForceLightsOff()
    {
        foreach (var l in indicatorLights)
            if (l) l.enabled = false;
    }
}
