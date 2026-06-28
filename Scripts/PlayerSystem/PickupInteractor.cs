using TMPro;
using Unity.Splines.Examples;
using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class PickupInteractor : MonoBehaviour
{
    [Header("Zasięg podnoszenia (dynamiczny)")]
    public float pickupRange = 2.5f;

    [Header("Raycast przeszkód")]
    public LayerMask obstacleLayer = 0;

    [Header("UI i warstwy")]
    public TextMeshProUGUI interactText;
    public LayerMask pickupLayer = ~0;

    [Header("Debug")]
    public Transform raycastOriginOverride;

    [Header("Kamera (opcjonalny override)")]
    public Camera camOverride;

    // wewnętrzne
    private WeaponManager wm;
    private bool IsTPP => CameraSwitcher.Instance != null && CameraSwitcher.Instance.IsTPPActive;

    void Start()
    {
        wm = GetComponentInChildren<WeaponManager>();
    }

    void LateUpdate()
    {
        var cam = camOverride ? camOverride : Camera.main;
        if (!cam) return;

        if (InventoryUI.IsInventoryOpen) return;

        if (!cam || PlayerInputHandler.Instance == null || wm == null)
            return;

        float currentPickupRange = IsTPP ? 5f : 2.5f;
        pickupRange = currentPickupRange;

        Ray ray = IsTPP && raycastOriginOverride != null
            ? new Ray(raycastOriginOverride.position, cam.transform.forward)
            : cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, currentPickupRange, pickupLayer))
        {
            // blokada jeśli za przeszkodą (wspólna dla wszystkiego)
            Vector3 origin = ray.origin;
            Vector3 targetPos = hit.point;
            float distance = Vector3.Distance(origin, targetPos);

            if (Physics.Raycast(origin, (targetPos - origin).normalized, distance, obstacleLayer, QueryTriggerInteraction.Ignore))
            {
                HideInteractText();
                return;
            }

            // ===== 1) OBSŁUGA IPressable (np. CashPickup) =====
            var pressable = hit.collider.GetComponentInParent<IPressable>();
            if (pressable != null)
            {
                ShowInteractText(pressable.Label);

                if (PlayerInputHandler.Instance.InteractPressedThisFrame)
                    pressable.Press();

                return;
            }

            // ===== 2) OBSŁUGA WeaponPickup (jak było) =====
            WeaponPickup pickup = hit.collider.GetComponentInParent<WeaponPickup>();
            if (pickup == null)
            {
                HideInteractText();
                return;
            }

            pickup.AssignWeaponManager(wm);
            ShowInteractText(pickup.itemData.itemName, pickup);

            if (PlayerInputHandler.Instance.InteractPressedThisFrame)
            {
                pickup.TryPickUpFromExternalRay();
            }
            return;
        }
        HideInteractText();
    }

    void ShowInteractText(string itemName, WeaponPickup pickup = null)
    {
        if (interactText == null) return;

        if (pickup != null && pickup.nonInteractable)
        {
            HideInteractText();
            return;
        }

        var action = PlayerInputHandler.Instance.playerMap.FindAction("Interact");
        string bindingDisplay = action?.GetBindingDisplayString() ?? "E";

        string baseLine = $"[{bindingDisplay}] Podnieś {itemName}";
        string extra = "";

        if (pickup != null && pickup.itemData is WeaponItemData wd && pickup.itemData.prefab.name != "Grenade")
        {
            if (pickup.ammoOnly)
            {
                int add = pickup.GetDisplayAmount();
                if (add > 0)
                    extra = $"ammo [+{add}]";   // tylko jeśli > 0
            }

            else
            {
                int showAmmo = pickup.currentAmmo >= 0 ? pickup.currentAmmo : wd.magazineSize;
                int showTotal = pickup.totalAmmo >= 0 ? pickup.totalAmmo : wd.magazineSize * 3;
                extra = $"{showAmmo} / {showTotal}";
            }
        }

        interactText.gameObject.SetActive(true);
        interactText.text = string.IsNullOrEmpty(extra) ? baseLine : $"{baseLine}  {extra}";
    }


    void HideInteractText()
    {
        if (interactText == null) return;
        interactText.text = "";
        interactText.gameObject.SetActive(false);
    }
}
