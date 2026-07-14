using System.Collections.Generic;
using UnityEngine;

public class MotionLightDetector : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Light targetLight;
    [SerializeField] private Collider triggerZone;

    [Header("Godziny dzia³ania")]
    [SerializeField] private bool useNightHours = true;

    [Tooltip("Godzina od której czujnik dzia³a. 22 = 22:00.")]
    [SerializeField, Range(0, 23)] private int activeFromHour = 22;

    [Tooltip("Godzina do której czujnik dzia³a. 6 = do 05:59.")]
    [SerializeField, Range(0, 23)] private int activeToHour = 6;

    [Header("Warstwy wykrywane")]
    [SerializeField] private LayerMask detectionLayers;

    [Header("Tagi wykrywane awaryjnie")]
    [SerializeField] private string[] allowedTags = { "Player", "NPC", "VehicleBody" };

    [Header("Zachowanie œwiat³a")]
    [SerializeField] private float stayOnTime = 6f;
    [SerializeField] private bool turnOffImmediatelyWhenDay = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private readonly HashSet<Transform> detectedActors = new();
    private float lightTimer;

    private void Reset()
    {
        targetLight = GetComponentInChildren<Light>();
        triggerZone = GetComponentInChildren<Collider>();
    }

    private void Awake()
    {
        if (targetLight == null)
            targetLight = GetComponentInChildren<Light>();

        if (triggerZone == null)
            triggerZone = GetComponentInChildren<Collider>();

        if (triggerZone != null)
            triggerZone.isTrigger = true;

        SetLight(false);
    }

    private void Update()
    {
        CleanupNullActors();

        bool activeByTime = IsActiveByTime();

        if (!activeByTime)
        {
            detectedActors.Clear();
            lightTimer = 0f;

            if (turnOffImmediatelyWhenDay)
                SetLight(false);

            return;
        }

        if (detectedActors.Count > 0)
        {
            lightTimer = stayOnTime;
            SetLight(true);
            return;
        }

        if (lightTimer > 0f)
        {
            lightTimer -= Time.deltaTime;

            if (lightTimer <= 0f)
                SetLight(false);
        }
    }

    public void NotifyTriggerEnter(Collider other)
    {
        TryRegisterActor(other);
    }

    public void NotifyTriggerStay(Collider other)
    {
        TryRegisterActor(other);
    }

    public void NotifyTriggerExit(Collider other)
    {
        Transform actor = GetActorRoot(other);

        if (actor == null)
            return;

        detectedActors.Remove(actor);

        if (debugLogs)
            Debug.Log($"[MotionLightDetector] EXIT: {actor.name}", this);
    }

    private void OnTriggerStay(Collider other)
    {
        // Daje pewnoœæ przy autach/NPC, które mog³y wejœæ zanim godzina przesz³a na noc.
        TryRegisterActor(other);
    }

    private void OnTriggerExit(Collider other)
    {
        Transform actor = GetActorRoot(other);

        if (actor == null)
            return;

        detectedActors.Remove(actor);

        if (debugLogs)
            Debug.Log($"[MotionLightDetector] EXIT: {actor.name}", this);
    }

    private void TryRegisterActor(Collider other)
    {
        if (!IsActiveByTime())
            return;

        Transform actor = GetActorRoot(other);

        if (actor == null)
            return;

        if (detectedActors.Add(actor))
        {
            if (debugLogs)
                Debug.Log($"[MotionLightDetector] ENTER: {actor.name}", this);
        }

        lightTimer = stayOnTime;
        SetLight(true);
    }

    private Transform GetActorRoot(Collider other)
    {
        if (other == null)
            return null;

        Transform root = other.transform.root;
        GameObject go = other.gameObject;

        bool layerAllowed =
            IsLayerAllowed(go.layer) ||
            IsLayerAllowed(root.gameObject.layer);

        bool tagAllowed =
            IsTagAllowed(go) ||
            IsTagAllowed(root.gameObject);

        if (!layerAllowed && !tagAllowed)
            return null;

        CharacterController cc = other.GetComponentInParent<CharacterController>();
        if (cc != null)
            return cc.transform;

        Rigidbody rb = other.attachedRigidbody;
        if (rb != null)
            return rb.transform;

        return root;
    }

    private bool IsLayerAllowed(int layer)
    {
        return (detectionLayers.value & (1 << layer)) != 0;
    }

    private bool IsTagAllowed(GameObject go)
    {
        if (allowedTags == null || allowedTags.Length == 0)
            return false;

        for (int i = 0; i < allowedTags.Length; i++)
        {
            string tag = allowedTags[i];

            if (string.IsNullOrWhiteSpace(tag))
                continue;

            if (go.CompareTag(tag))
                return true;
        }

        return false;
    }

    private bool IsActiveByTime()
    {
        if (!useNightHours)
            return true;

        // 22 -> 6 dzia³a przez pó³noc: 22:00-05:59.
        return GameTime.IsTimeBetweenHours(activeFromHour, activeToHour);
    }

    private void SetLight(bool enabled)
    {
        if (targetLight == null)
            return;

        if (targetLight.enabled != enabled)
            targetLight.enabled = enabled;
    }

    private void CleanupNullActors()
    {
        if (detectedActors.Count == 0)
            return;

        detectedActors.RemoveWhere(t => t == null || !t.gameObject.activeInHierarchy);
    }
}