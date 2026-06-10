using UnityEngine;

public class RecoilController : MonoBehaviour
{
    public Transform recoilTarget;

    [Header("Recoil Feel")]
    public float recoilSmooth = 14f;
    public float returnSpeed = 7f;
    public float recoilPowerMultiplier = 1f;

    [Header("Clamp")]
    public float maxVerticalRecoil = 6f;
    public float maxHorizontalRecoil = 3f;

    [Header("Mnożniki trybów kamery")]
    public float recoilMulFPS = 0.45f;
    public float recoilMulTPP = 0.25f;

    private float currentMul = 1f;

    private Vector2 currentRecoil;
    private Vector2 targetRecoil;

    void LateUpdate()
    {
        targetRecoil = Vector2.Lerp(
            targetRecoil,
            Vector2.zero,
            Time.deltaTime * returnSpeed
        );

        currentRecoil = Vector2.Lerp(
            currentRecoil,
            targetRecoil,
            Time.deltaTime * recoilSmooth
        );

        if (recoilTarget != null)
        {
            recoilTarget.localRotation = Quaternion.Euler(
                -currentRecoil.x,
                currentRecoil.y,
                0f
            );
        }
    }

    public void SetMode(bool tpp)
    {
        currentMul = tpp ? recoilMulTPP : recoilMulFPS;
    }

    public void ApplyRecoil(Vector2 amount)
    {
        Vector2 recoil = amount * recoilPowerMultiplier * currentMul;

        targetRecoil += recoil;

        targetRecoil.x = Mathf.Clamp(targetRecoil.x, -maxVerticalRecoil, maxVerticalRecoil);
        targetRecoil.y = Mathf.Clamp(targetRecoil.y, -maxHorizontalRecoil, maxHorizontalRecoil);
    }

    public void ResetRecoil()
    {
        currentRecoil = Vector2.zero;
        targetRecoil = Vector2.zero;

        if (recoilTarget != null)
            recoilTarget.localRotation = Quaternion.identity;
    }
}