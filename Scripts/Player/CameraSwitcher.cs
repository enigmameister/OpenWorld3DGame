using System.Collections.Generic;
using UnityEngine;

public class CameraSwitcher : MonoBehaviour
{
    public enum CamMode { FPS, TPP }

    [Header("Weapon Camera Overlay")]
    public Camera weaponCameraFPS;
    public Camera weaponCameraTPP;

    [Header("Main Camera")]
    public Camera mainCamera;
    public Transform mainCamTransform;

    [Header("Transformy pozycji widoku")]
    public Transform fpsCamTransform;
    public Transform tppCamTransform;

    [Header("Płynne przejście")]
    public float switchSmooth = 10f;

    [Header("Komponenty zależne od kamery")]
    public RecoilController recoilController;
    public FallImpactCamera fallImpactCamera;
    public PickupInteractor pickupInteractor;

    public Transform recoilTargetFPS;
    public Transform recoilTargetTPP;

    public Transform fallCameraTargetFPS;
    public Transform fallCameraTargetTPP;

    public Camera pickupInteractorCameraFPS;
    public Camera pickupInteractorCameraTPP;

    [Header("Bronie gracza")]
    public List<Gun> allGuns;
    public List<Grenade> allGrenades;
    public List<Melee> allMelees;

    [Header("Warstwy kamery gameplay")]
    [SerializeField] private LayerMask gameplayCameraMask;



    [SerializeField] private CamMode currentMode = CamMode.FPS;

    private bool isTPP = false;
    public bool IsTPPActive => isTPP;
    public static CameraSwitcher Instance { get; private set; }

    private int fullMask;
    private int fpsMask;
    private int tppMask;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        fullMask = gameplayCameraMask.value;

        fpsMask = fullMask & ~LayerMask.GetMask(
            "WeaponRenderFPS",
            "WeaponRenderTPP",
            "Arms",
            "Player",
            "Hat"
        );

        tppMask = fullMask & ~LayerMask.GetMask(
            "WeaponRenderFPS",
            "Arms"
        );

        isTPP = currentMode == CamMode.TPP;

        Transform target = isTPP ? tppCamTransform : fpsCamTransform;
        if (target && mainCamTransform)
            mainCamTransform.SetPositionAndRotation(target.position, target.rotation);

        if (pickupInteractor)
            pickupInteractor.camOverride = mainCamera;

        if (isTPP) SwitchToTPP();
        else SwitchToFPS();
    }

    void Update()
    {
        if (!DevConsole.IsOpen &&
            PlayerInputHandler.Instance != null &&
            PlayerInputHandler.Instance.SwitchCameraPressedThisFrame)
        {
            isTPP = !isTPP;

            if (isTPP) SwitchToTPP();
            else SwitchToFPS();
        }
    }

    void LateUpdate()
    {
        Transform target = isTPP ? tppCamTransform : fpsCamTransform;
        if (!target || !mainCamTransform) return;

        mainCamTransform.SetPositionAndRotation(target.position, target.rotation);
    }

    void SwitchToFPS()
    {
        currentMode = CamMode.FPS;

        if (recoilController)
        {
            recoilController.recoilTarget = recoilTargetFPS;
            recoilController.SetMode(false);
        }

        if (fallImpactCamera)
            fallImpactCamera.cameraTarget = fallCameraTargetFPS;

        if (weaponCameraFPS) weaponCameraFPS.enabled = true;
        if (weaponCameraTPP) weaponCameraTPP.enabled = false;

        if (mainCamera)
            mainCamera.cullingMask = fpsMask;

        if (pickupInteractor)
            pickupInteractor.camOverride = mainCamera;

        UpdateWeaponCameras(mainCamera);
        UpdateWeaponViewModels(false);
    }

    void SwitchToTPP()
    {
        currentMode = CamMode.TPP;

        if (recoilController)
        {
            recoilController.recoilTarget = recoilTargetTPP;
            recoilController.SetMode(true);
        }

        if (fallImpactCamera)
            fallImpactCamera.cameraTarget = fallCameraTargetTPP;

        if (weaponCameraFPS) weaponCameraFPS.enabled = false;

        // Na razie TPP model widzi MainCamera, więc overlay TPP nie jest potrzebny.
        if (weaponCameraTPP) weaponCameraTPP.enabled = false;

        if (mainCamera)
            mainCamera.cullingMask = tppMask;

        if (pickupInteractor)
            pickupInteractor.camOverride = mainCamera;

        UpdateWeaponCameras(mainCamera);
        UpdateWeaponViewModels(true);
    }

    void UpdateWeaponCameras(Camera cam)
    {
        if (!cam) return;

        foreach (var gun in allGuns)
            if (gun != null)
                gun.BindCamera(cam, snapshotDefaultFOV: true);

        foreach (var grenade in allGrenades)
            if (grenade != null)
                grenade.playerCamera = cam;
    }

    void UpdateWeaponViewModels(bool tpp)
    {
        foreach (var gun in allGuns)
        {
            if (gun == null) continue;

            Transform fps = gun.transform.Find("ViewModelRoot/Model_FPS");
            Transform tppModel = gun.transform.Find("ViewModelRoot/Model_TPP");

            if (!fps)
                fps = gun.transform.Find("WeaponLogic/ViewModelRoot/Model_FPS");

            if (!tppModel)
                tppModel = gun.transform.Find("WeaponLogic/ViewModelRoot/Model_TPP");

            if (fps) fps.gameObject.SetActive(!tpp);
            if (tppModel) tppModel.gameObject.SetActive(tpp);
        }
    }

    public void ForceUpdateCameraNow()
    {
        Transform target = isTPP ? tppCamTransform : fpsCamTransform;
        if (!target || !mainCamTransform) return;

        mainCamTransform.SetPositionAndRotation(target.position, target.rotation);
    }
}