using UnityEngine;

public class CodePanelPressable : MonoBehaviour, IPressable
{
    [SerializeField] private CodeInputPanel codePanel;
    [SerializeField] private string label = "Use Panel";

    public string Label => label;

    private void Awake()
    {
        if (codePanel == null)
            codePanel = GetComponentInParent<CodeInputPanel>();
    }

    public void Press()
    {
        if (codePanel == null)
            return;

        codePanel.OpenPanel();
    }
}