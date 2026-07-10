using UnityEngine;

public class TestHouseDoorAccessPressable : MonoBehaviour, IPressable
{
    [SerializeField] private TestHouseDoorAccessController door;
    [SerializeField] private string label = "U¿yj panelu drzwi";

    public string Label => label;

    private void Awake()
    {
        if (door == null)
            door = GetComponentInParent<TestHouseDoorAccessController>();
    }

    public void Press()
    {
        if (door == null)
        {
            Debug.LogWarning($"[TestHouseDoorAccessPressable] {name}: Door reference missing.", this);
            return;
        }

        door.TryUsePanel();
    }
}