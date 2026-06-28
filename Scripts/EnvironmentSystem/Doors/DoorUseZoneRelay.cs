using UnityEngine;

public class DoorUseZoneRelay : MonoBehaviour
{
    public bool PlayerInside { get; private set; }
    [SerializeField] string requiredTag = "Player";
    [SerializeField] bool verboseLogging = true;

    DoorInteract _door; bool _isFront;

    public void Bind(DoorInteract door, bool isFront, string reqTag)
    {
        _door = door; _isFront = isFront;
        requiredTag = string.IsNullOrEmpty(reqTag) ? "Player" : reqTag;
        if (verboseLogging) Debug.Log($"[Relay:{name}] Bind -> isFront={_isFront}, tag={requiredTag}", this);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(requiredTag)) return;
        PlayerInside = true;
        if (verboseLogging) Debug.Log($"[Relay:{name}] ENTER by {other.name} (PlayerInside=true)", this);
        _door?.ZoneEnter(_isFront);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(requiredTag)) return;
        PlayerInside = false;
        if (verboseLogging) Debug.Log($"[Relay:{name}] EXIT by {other.name} (PlayerInside=false)", this);
        _door?.ZoneExit(_isFront);
    }
}
