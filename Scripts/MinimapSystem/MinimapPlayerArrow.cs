using UnityEngine;

public class MinimapPlayerArrow : MonoBehaviour
{
    [Header("Rotation")]
    public bool mapRotatesWithPlayer = true;
    public float rotateSpeed = 15f;

    void LateUpdate()
    {
        Transform target = MinimapTargetProvider.Instance != null
            ? MinimapTargetProvider.Instance.CurrentTarget
            : null;

        if (target == null)
            return;

        Quaternion targetRotation;

        if (mapRotatesWithPlayer)
        {
            // GTA / NFS style: mapa obraca siê z graczem, wiêc strza³ka patrzy stale w górê
            targetRotation = Quaternion.identity;
        }
        else
        {
            // mapa nieruchoma, strza³ka pokazuje kierunek gracza
            targetRotation = Quaternion.Euler(0f, 0f, -target.eulerAngles.y);
        }

        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            targetRotation,
            Time.deltaTime * rotateSpeed
        );
    }
}