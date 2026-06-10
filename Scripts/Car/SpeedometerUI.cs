using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpeedometerUI : MonoBehaviour
{
    public CarControll carController;
    public RectTransform speedNeedle;
    public RectTransform rpmNeedle;

    public TextMeshProUGUI gearText;
    public TMP_Text speedText;

    public GameObject speedometerRoot;

    public float maxSpeed = 260f;
    public float maxRPM = 7000f;
    public float speedMaxRotation = -220f;
    public float speedMinRotation = 40f;
    public float rpmMaxRotation = -220f;
    public float rpmMinRotation = 40f;

    public NitroSystem nitroSystem;
    public Image nitroFillImage;

    void Update()
    {
        if (speedometerRoot != null)
            speedometerRoot.SetActive(carController != null && carController.isControlled);

        if (carController == null || !carController.isControlled)
            return;

        // prędkość
        int displaySpeed = carController.GetDisplaySpeedKPH();

        if (speedText != null)
            speedText.text = $"{displaySpeed}";

        float speedPercent = Mathf.Clamp01(displaySpeed / maxSpeed);
        float speedRotation = Mathf.Lerp(speedMinRotation, speedMaxRotation, speedPercent);
        if (speedNeedle != null)
            speedNeedle.localRotation = Quaternion.Euler(0, 0, speedRotation);

        // obroty – używamy GetDisplayRPM()
        int rpm = carController.GetDisplayRPM();
        float rpmPercent = Mathf.Clamp01(rpm / maxRPM);
        float rpmRotation = Mathf.Lerp(rpmMinRotation, rpmMaxRotation, rpmPercent);
        if (rpmNeedle != null)
            rpmNeedle.localRotation = Quaternion.Euler(0, 0, rpmRotation);

        // bieg
        if (gearText != null)
            gearText.text = carController.isReversing ? "R" : carController.currentGear.ToString();

        // nitro
        if (nitroSystem != null && nitroFillImage != null)
            nitroFillImage.fillAmount = nitroSystem.GetNitroNormalized();
    }
}
