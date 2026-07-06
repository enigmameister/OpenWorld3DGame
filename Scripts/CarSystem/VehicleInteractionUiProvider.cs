using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class VehicleInteractionUiProvider : MonoBehaviour
{
    public static VehicleInteractionUiProvider Instance { get; private set; }

    [Header("Vehicle Interaction UI")]
    [SerializeField] private GameObject loadingBarRoot;
    [SerializeField] private Image loadingBarFill;

    public GameObject LoadingBarRoot => loadingBarRoot;
    public Image LoadingBarFill => loadingBarFill;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[VehicleInteractionUiProvider] Duplicate provider found on {name}. Using first instance: {Instance.name}");
            return;
        }

        Instance = this;

        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Hide()
    {
        if (loadingBarRoot != null)
            loadingBarRoot.SetActive(false);

        if (loadingBarFill != null)
            loadingBarFill.fillAmount = 0f;
    }

    public void Show()
    {
        if (loadingBarRoot != null)
            loadingBarRoot.SetActive(true);

        if (loadingBarFill != null)
            loadingBarFill.fillAmount = 0f;
    }
}