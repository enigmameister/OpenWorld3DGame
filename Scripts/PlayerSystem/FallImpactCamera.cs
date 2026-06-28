using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class FallImpactCamera : MonoBehaviour
{
    [Tooltip("Transform, który ma być pochylany (u Ciebie: MainCamera). Ustaw ręcznie w Inspectorze.")]
    public Transform cameraTarget;

    [Header("Tilt")]
    public float rollAngle = 12f;     // maks. kąt przechyłu (stopnie)
    public float tiltDuration = 0.15f; // czas „uderzenia”
    public float returnSpeed = 5f;     // szybkość powrotu

    // stan wewnętrzny (addytywnie do bieżącej rotacji)
    private Quaternion baseLocalRot;           // baza (to, co ustawiły inne systemy)
    private Quaternion tiltOffset = Quaternion.identity; // sam roll, który dokładamy

    void Start()
    {

        if (cameraTarget == null && Camera.main != null)
            cameraTarget = Camera.main.transform; // tylko gdy brak w Inspectorze

        if (!cameraTarget)
        {
            Debug.LogWarning("FallImpactCamera: cameraTarget == null. Przypisz w Inspectorze (np. MainCamera).");
            enabled = false;
            return;
        }

        baseLocalRot = cameraTarget.localRotation;
        tiltOffset = Quaternion.identity;
    }

    /// <summary>Jeśli zmieniasz docelowy transform w locie (np. przełączenie FPS/TPP).</summary>
    public void SetTarget(Transform t)
    {
        cameraTarget = t;
        if (!cameraTarget) { enabled = false; return; }

        baseLocalRot = cameraTarget.localRotation;
        tiltOffset = Quaternion.identity;
        enabled = true;
    }

    /// <summary>Przechył losowo w lewo/prawo (np. po upadku).</summary>
    public void DoTilt()
    {
        if (!cameraTarget) return;
        float sign = Random.value < 0.5f ? -1f : 1f;
        StartSignedTilt(sign * rollAngle);
    }

    /// <summary>Przechył o zadany znak (użyte przy uderzeniach kierunkowych).</summary>
    public void DoTiltSigned(float signedAngle)
    {
        if (!cameraTarget) return;
        StartSignedTilt(Mathf.Clamp(signedAngle, -Mathf.Abs(rollAngle), Mathf.Abs(rollAngle)));
    }

    private void StartSignedTilt(float signedAngle)
    {
        StopAllCoroutines();
        StartCoroutine(TiltSignedRoutine(signedAngle));
    }

    private IEnumerator TiltSignedRoutine(float signedAngle)
    {
        // Baza = aktualna rotacja targetu (sumuje się z oddechem, recoilem itd.)
        baseLocalRot = cameraTarget.localRotation;
        tiltOffset = Quaternion.identity;

        float inDur = Mathf.Clamp(tiltDuration * 0.6f, 0.06f, tiltDuration);
        float t = 0f;

        // Wejście
        while (t < inDur)
        {
            t += Time.deltaTime;
            float k = t / inDur;
            tiltOffset = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, signedAngle, k));
            yield return null;
        }

        // Powrót – wygaszamy sam offset
        while (Mathf.Abs(GetRoll(tiltOffset)) > 0.01f)
        {
            float roll = Mathf.Lerp(GetRoll(tiltOffset), 0f, Time.deltaTime * returnSpeed);
            tiltOffset = Quaternion.Euler(0f, 0f, roll);
            yield return null;
        }

        tiltOffset = Quaternion.identity;
    }

    void LateUpdate()
    {
        if (!cameraTarget) return;

        // Gdy nie mamy aktywnego tiltu, baza podąża za bieżącą rotacją targetu.
        if (tiltOffset == Quaternion.identity)
            baseLocalRot = cameraTarget.localRotation;

        // Final = baza * nasz offset (addytywnie, tylko roll)
        Quaternion targetRot = baseLocalRot * tiltOffset;
        cameraTarget.localRotation = Quaternion.Slerp(
            cameraTarget.localRotation, targetRot, Time.deltaTime * returnSpeed);
    }

    private static float GetRoll(Quaternion q)
    {
        float z = q.eulerAngles.z;
        if (z > 180f) z -= 360f;
        return z;
    }
}
