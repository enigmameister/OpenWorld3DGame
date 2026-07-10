using UnityEngine;

public class DoorPressable : MonoBehaviour, IPressable
{
    [SerializeField] private DoorInteract door;
    [SerializeField] private string label = "Open Doors";

    public string Label
    {
        get
        {
            if (door != null && door.IsLocked())
                return "Doors Closed";

            return label;
        }
    }

    private void Awake()
    {
        if (door == null)
            door = GetComponentInParent<DoorInteract>();
    }

    public void Press()
    {
        if (door == null)
            return;

        door.TryUseFromPlayer();
    }
}