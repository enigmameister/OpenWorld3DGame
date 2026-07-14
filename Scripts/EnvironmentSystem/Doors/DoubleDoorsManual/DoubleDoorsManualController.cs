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

    [Header("Opening Hours")]
    [SerializeField] private bool useOpeningHours = false;

    [SerializeField, Range(0, 23)] private int openHour = 7;
    [SerializeField, Range(0, 23)] private int closeHour = 15;

    [Tooltip("Jeœli TRUE, drzwi pozostaj¹ otwarte niezale¿nie od godzin.")]
    [SerializeField] private bool forceAlwaysOpenAllowed = false;

    [Tooltip("Jeœli TRUE, po zamkniêciu godzin drzwi zamkn¹ siê, gdy Detection bêdzie puste.")]
    [SerializeField] private bool closeWhenOutOfHours = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private readonly Dictionary<Transform, int> directionActors = new();
    private readonly Dictionary<Transform, int> detectionActors = new();

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

        if (debugLogs)
        {
            Debug.Log(
                $"[DoubleDoors] isOpen={isOpen}, direction={directionActors.Count}, detection={detectionActors.Count}",
                this
            );
        }

        // Trigger kierunkowy otwiera drzwi.
        if (directionActors.Count > 0)
        {
            closeTimer = closeDelay;

            if (CanOpenNow())
            {
                if (!isOpen)
                    SetOpen(true, lastOpenSide);
            }

            return;
        }

        // Detection tylko trzyma ju¿ otwarte drzwi.
        if (isOpen && detectionActors.Count > 0)
        {
            closeTimer = closeDelay;
            return;
        }

        // Poza godzinami te¿ zamykaj dopiero gdy detection jest puste.
        if (isOpen && closeWhenOutOfHours && !CanOpenNow())
        {
            closeTimer -= Time.deltaTime;

            if (closeTimer <= 0f)
                SetOpen(false, lastOpenSide);

            return;
        }

        // Normalne zamykanie po odejœciu.
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

        bool wasDirectionEmpty = directionActors.Count == 0;

        AddActor(directionActors, actor);

        // Kierunek ustawiamy tylko gdy drzwi s¹ zamkniête
        // i to jest pierwsze wejœcie w trigger kierunkowy.
        // Dziêki temu Outside + Inside nie przepisuj¹ sobie strony nawzajem.
        if (!isOpen && wasDirectionEmpty)
            lastOpenSide = side;

        closeTimer = closeDelay;

        if (debugLogs)
            Debug.Log($"[DoubleDoors] Direction ENTER: {actor.name}, side={side}", this);

        if (!CanOpenNow())
        {
            if (debugLogs)
                Debug.Log($"[DoubleDoors] Blocked by opening hours. Hour={GameTime.Hour}", this);

            return;
        }

        if (!isOpen)
            SetOpen(true, lastOpenSide);
    }

    public void NotifyTriggerExit(Collider other)
    {
        Transform actor = GetActorRoot(other);
        if (actor == null)
            return;

        RemoveActor(directionActors, actor);

        if (debugLogs)
            Debug.Log($"[DoubleDoors] Direction EXIT: {actor.name}", this);
    }

    // TriggerDetection
    public void NotifyDetectionEnter(Collider other)
    {
        Transform actor = GetActorRoot(other);
        if (actor == null)
            return;

        AddActor(detectionActors, actor);
        closeTimer = closeDelay;

        if (debugLogs)
            Debug.Log($"[DoubleDoors] Detection ENTER: {actor.name}", this);

        // Detection NIE otwiera drzwi.
    }

    public void NotifyDetectionExit(Collider other)
    {
        Transform actor = GetActorRoot(other);
        if (actor == null)
            return;

        RemoveActor(detectionActors, actor);

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

    private bool CanOpenNow()
    {
        if (forceAlwaysOpenAllowed)
            return true;

        if (!useOpeningHours)
            return true;

        return GameTime.IsTimeBetweenHours(openHour, closeHour);
    }

    private void AddActor(Dictionary<Transform, int> dict, Transform actor)
    {
        if (actor == null)
            return;

        if (dict.TryGetValue(actor, out int count))
            dict[actor] = count + 1;
        else
            dict.Add(actor, 1);
    }

    private void RemoveActor(Dictionary<Transform, int> dict, Transform actor)
    {
        if (actor == null)
            return;

        if (!dict.TryGetValue(actor, out int count))
            return;

        count--;

        if (count <= 0)
            dict.Remove(actor);
        else
            dict[actor] = count;
    }

    private void CleanupNullActors(Dictionary<Transform, int> dict)
    {
        if (dict.Count == 0)
            return;

        List<Transform> toRemove = null;

        foreach (var kvp in dict)
        {
            if (kvp.Key == null || !kvp.Key.gameObject.activeInHierarchy)
            {
                toRemove ??= new List<Transform>();
                toRemove.Add(kvp.Key);
            }
        }

        if (toRemove == null)
            return;

        foreach (var t in toRemove)
            dict.Remove(t);
    }
}