using System.Collections.Generic;
using UnityEngine;

public class DoubleDoorsManualController : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator animator;
    [SerializeField] private string openBoolName = "IsOpen";
    [SerializeField] private string openSideIntName = "OpenSide";

    [Header("Actors allowed to open")]
    [SerializeField] private LayerMask actorLayers = ~0;
    [SerializeField] private string[] allowedTags = { "Player", "NPC" };

    [Header("Closing")]
    [SerializeField] private float closeDelay = 0.45f;

    [Tooltip("Jeœli TRUE, drzwi nie zamkn¹ siê dopóki ktoœ jest w TriggerDetection.")]
    [SerializeField] private bool requireDetectionEmptyToClose = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private readonly HashSet<Transform> directionActors = new();
    private readonly HashSet<Transform> detectionActors = new();

    private int openBoolHash;
    private int openSideHash;

    private float closeTimer;
    private bool isOpen;
    private int lastOpenSide = 0;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        openBoolHash = Animator.StringToHash(openBoolName);
        openSideHash = Animator.StringToHash(openSideIntName);

        SetOpen(false, lastOpenSide, instant: true);
    }

    private void Update()
    {
        CleanupNullActors(directionActors);
        CleanupNullActors(detectionActors);

        // 1. Otwieranie robi TYLKO TriggerOutside / TriggerInside.
        if (directionActors.Count > 0)
        {
            closeTimer = closeDelay;

            if (!isOpen)
                SetOpen(true, lastOpenSide);

            return;
        }

        // 2. TriggerDetection NIE otwiera drzwi.
        // Jeœli drzwi s¹ ju¿ otwarte, Detection tylko trzyma je otwarte.
        if (isOpen && detectionActors.Count > 0)
        {
            closeTimer = closeDelay;
            return;
        }

        // 3. Zamykaj dopiero gdy nikt nie jest w direction ani detection.
        if (isOpen)
        {
            closeTimer -= Time.deltaTime;

            if (closeTimer <= 0f)
                SetOpen(false, lastOpenSide);
        }
    }

    // TriggerOutside / TriggerInside
    public void NotifyTriggerEnter(Collider other, int side)
    {
        Transform actor = GetActorRoot(other);
        if (actor == null)
            return;

        directionActors.Add(actor);

        // Na razie zawsze ustaw kierunek z triggera, ¿eby ³atwo sprawdziæ scenê.
        lastOpenSide = side;

        closeTimer = closeDelay;

        if (debugLogs)
            Debug.Log($"[DoubleDoors] Direction ENTER: {actor.name}, side={side}", this);

        SetOpen(true, lastOpenSide);
    }

    public void NotifyTriggerExit(Collider other)
    {
        Transform actor = GetActorRoot(other);
        if (actor == null)
            return;

        directionActors.Remove(actor);

        if (debugLogs)
            Debug.Log($"[DoubleDoors] Direction EXIT: {actor.name}", this);
    }

    // TriggerDetection
    public void NotifyDetectionEnter(Collider other)
    {
        Transform actor = GetActorRoot(other);
        if (actor == null)
            return;

        detectionActors.Add(actor);
        closeTimer = closeDelay;

        if (debugLogs)
            Debug.Log($"[DoubleDoors] Detection ENTER: {actor.name}", this);

        // NIE otwieramy tutaj drzwi.
        // Detection tylko blokuje zamkniêcie ju¿ otwartych drzwi.
    }

    public void NotifyDetectionExit(Collider other)
    {
        Transform actor = GetActorRoot(other);
        if (actor == null)
            return;

        detectionActors.Remove(actor);

        if (debugLogs)
            Debug.Log($"[DoubleDoors] Detection EXIT: {actor.name}", this);
    }

    private Transform GetActorRoot(Collider other)
    {
        if (other == null)
            return null;

        GameObject go = other.gameObject;

        bool layerAllowed = (actorLayers.value & (1 << go.layer)) != 0;
        bool tagAllowed = IsTagAllowed(go);

        if (!layerAllowed && !tagAllowed)
            return null;

        CharacterController cc = other.GetComponentInParent<CharacterController>();
        if (cc != null)
            return cc.transform;

        Rigidbody rb = other.attachedRigidbody;
        if (rb != null)
            return rb.transform;

        return other.transform.root;
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

    private void SetOpen(bool open, int side, bool instant = false)
    {
        isOpen = open;
        lastOpenSide = side;

        if (animator == null)
            return;

        animator.SetInteger(openSideHash, lastOpenSide);
        animator.SetBool(openBoolHash, open);

        if (instant)
            animator.Update(0f);
    }

    private void CleanupNullActors(HashSet<Transform> set)
    {
        if (set.Count == 0)
            return;

        set.RemoveWhere(t => t == null || !t.gameObject.activeInHierarchy);
    }

    private void OnDisable()
    {
        directionActors.Clear();
        detectionActors.Clear();
        closeTimer = 0f;
    }
}