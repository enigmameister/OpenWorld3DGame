using UnityEngine;

public class ShelfCodePanelPressable : MonoBehaviour, IPressable
{
    [SerializeField] private ShelfCodePanel codePanel;
    [SerializeField] private string label = "Open Shelf";

    public string Label => label;

    private void Awake()
    {
        if (codePanel == null)
            codePanel = GetComponentInParent<ShelfCodePanel>(true);

        if (codePanel == null)
            codePanel = GetComponentInChildren<ShelfCodePanel>(true);
    }

    public void Press()
    {
        if (codePanel == null)
            return;

        if (!codePanel.gameObject.activeSelf)
            codePanel.gameObject.SetActive(true);
    }
}