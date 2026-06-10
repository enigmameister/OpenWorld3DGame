using UnityEngine;

public class WeaponViewModelController : MonoBehaviour
{
    [Header("Hands Model")]
    [SerializeField] private GameObject handsModel;

    [Header("Hands Transform")]
    [SerializeField]
    private Vector3 handsPosition =
        new Vector3(0f, -0.2f, 0.4f);

    [SerializeField]
    private Vector3 handsRotation =
        new Vector3(0f, 0f, 0f);

    private WeaponManager weaponManager;

    void Awake()
    {
        weaponManager = GetComponent<WeaponManager>();
    }

    public void ShowHands()
    {
        if (!handsModel) return;

        handsModel.SetActive(true);

        handsModel.transform.localPosition = handsPosition;
        handsModel.transform.localEulerAngles = handsRotation;
    }

    public void HideHands()
    {
        if (!handsModel) return;

        handsModel.SetActive(false);
    }

    public void ShowWeapon(GameObject weapon)
    {
        if (weapon != null)
            weapon.SetActive(true);
    }

    public void HideWeapon(GameObject weapon)
    {
        if (weapon != null)
            weapon.SetActive(false);
    }
}