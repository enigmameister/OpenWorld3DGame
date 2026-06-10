using System;
using System.Linq;
using UnityEngine;
public class DebugGameHUD : MonoBehaviour
{
    [Header("Attach (auto)")]
    public Transform player;
    public Camera playerCam;
    private WeaponManager _wm;
    private Grenade _grenade; // jeśli używasz klasy Grenade z inventoryInstance

    // --- DEBUG RAY ---
    [Header("Debug: Ray z kamery")]
    [SerializeField] private bool debugRay = true;
    [SerializeField] private Color rayColor = new Color(0f, 1f, 1f, 0.9f);   // cyjan
    [SerializeField] private Color hitColor = new Color(1f, 0.6f, 0f, 0.95f); // pomarańcz
    [SerializeField] private Color blockColor = new Color(1f, 0f, 0f, 0.95f); // czerwony, gdy coś blokuje
    [SerializeField, Range(0.01f, 1f)] private float gizmoSphere = 0.04f;
    [SerializeField] private bool debugLabelInGameView = true;

    Camera _cam;
    Ray _dbgRay;
    bool _dbgHasHit;
    Vector3 _dbgHitPoint;
    Vector3 _dbgHitNormal;
    Collider _dbgHitCol;
    float _dbgHitDist;


    [Header("UI")]
    public KeyCode toggleKey = KeyCode.F8;
    public Vector2 pivot = new Vector2(12, 12);
    public int fontSize = 14;
    public bool visible = true;

    [Header("Performance")]
    [Tooltip("Co ile sekund odświeżać dane debugowe (niższe = dokładniej, większe = mniej lagów)")]
    public float refreshInterval = 0.25f;

    private float _nextUpdate;
    private GUIStyle _style;
    private Vector3 _lastPos;
    private float _speed, _speedSmoothed;

    // cache komponentów
    private PlayerStats _stats;
    private Gun _gun;
    private Melee _melee;
    private Component _pickupInteractor;
    private SniperRifleBehaviour _sniper;

    // cache danych do wyświetlenia
    private string _text = "";
    private float _fpsUpdate;
    private float _fps;

    private PickupInteractor _pi;    // <— strong-typed cache


    void Start()
    {
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            player = p ? p.transform : null;
        }
        if (!playerCam) playerCam = Camera.main;
        if (player) _lastPos = player.position;

        TryCacheRefs();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) visible = !visible;
        if (!visible) return;

        // FPS (co 0.5s)
        if (Time.time >= _fpsUpdate)
        {
            _fps = 1f / Time.smoothDeltaTime;
            _fpsUpdate = Time.time + 0.5f;
        }

        // prędkość (lekko, co klatkę)
        if (player)
        {
            Vector3 delta = player.position - _lastPos; delta.y = 0f;
            float dt = Mathf.Max(Time.deltaTime, 0.001f);
            _speed = delta.magnitude / dt;
            _speedSmoothed = Mathf.Lerp(_speedSmoothed, _speed, 0.15f);
            _lastPos = player.position;
        }

        if (Time.time >= _nextUpdate)
        {
            _nextUpdate = Time.time + refreshInterval;
            TryCacheRefs();
            RefreshText();
        }

        DebugRaycastAndDraw();
    }

    void Awake()
    {
        if (!_cam) _cam = Camera.main;
    }

    void TryCacheRefs()
    {
        if (!_stats && player) _stats = player.GetComponent<PlayerStats>();
        if (!_pi && player) _pi = player.GetComponentInChildren<PickupInteractor>(true);

        if (!_wm && player) _wm = player.GetComponentInChildren<WeaponManager>(true);

        // UWAGA: _gun/_melee/_grenade nie wystarczy cache’ować 1 raz.
        // Będziemy i tak brać je z aktualnego obiektu slota (patrz helper poniżej),
        // ale trzymamy tu referencję awaryjną, gdyby slot był null.
        if (!_gun && player) _gun = player.GetComponentInChildren<Gun>(true);
        if (!_melee && player) _melee = player.GetComponentInChildren<Melee>(true);
        if (!_grenade && player) _grenade = player.GetComponentInChildren<Grenade>(true);

        if (!playerCam) playerCam = Camera.main;
    }

    // Zwraca ładną nazwę aktualnie trzymanego przedmiotu z danych itemu.
    // Priorytet: Gun -> Melee -> Grenade -> "—".
    string GetEquippedItemName()
    {
        // Gun
        if (_gun)
        {
            var d = _gun.inventoryInstance?.data as InventoryItemData;
            if (d != null)
            {
                if (!string.IsNullOrEmpty(d.itemName)) return d.itemName;
                return d.name; // nazwa assetu ScriptableObject
            }
            // awaryjnie: jeśli brak instancji, spróbuj z przypiętego SO w broni
            if (_gun.weaponData != null)
            {
                if (!string.IsNullOrEmpty(_gun.weaponData.itemName)) return _gun.weaponData.itemName;
                return _gun.weaponData.name;
            }
        }

        // Melee
        if (_melee)
        {
            var d = _melee.inventoryInstance?.data as InventoryItemData;
            if (d != null)
            {
                if (!string.IsNullOrEmpty(d.itemName)) return d.itemName;
                return d.name;
            }
        }

        // Grenade (jeśli masz klasę Grenade z inventoryInstance)
        var grenade = player ? player.GetComponentInChildren<Grenade>(true) : null;
        if (grenade)
        {
            var d = grenade.inventoryInstance?.data as InventoryItemData;
            if (d != null)
            {
                if (!string.IsNullOrEmpty(d.itemName)) return d.itemName;
                return d.name;
            }
        }

        return "—";
    }

    void RefreshText()
    {
        float kmh = _speedSmoothed * 3.6f;

        var cc = player ? player.GetComponent<CharacterController>() : null;
        string ccInfo = cc ? $"CC: h={cc.height:0.00}  cY={cc.center.y:0.00}" : "CC: —";

        // aktualny item + nazwa
        if (!TryGetCurrentItem(out var itemData, out var wid, out string weaponName))
            wid = null; // Hands → brak mnożników

        // mnożniki (gdy brak danych = 1.0)
        string moveMul = "1.00", stamMul = "1.00";
        if (wid != null)
        {
            float mm = Mathf.Clamp(wid.moveSpeedMultiplier, 0.5f, 1.2f);
            float sm = Mathf.Clamp(wid.staminaDrainMultiplier, 0.5f, 2.0f);
            moveMul = mm.ToString("0.00");
            stamMul = sm.ToString("0.00");
        }

        // Interactor range  ✅ brakująca zmienna
        string interRange = _pi ? _pi.pickupRange.ToString("0.00") + " m" : "—";

        // FOV
        bool scoped = _gun && _gun.IsScoped();
        float camFOV = playerCam ? playerCam.fieldOfView : 0f;
        float targetFOV = _gun
    ? _gun.GetComputedTargetFOV()
    : camFOV;
        float baseFOV = playerCam ? playerCam.fieldOfView : 60f;
        float adsFOV = targetFOV;

        bool ads = false;
        if (_gun && !scoped)
            ads = (baseFOV - camFOV) > 5f || Mathf.Abs(camFOV - adsFOV) <= 1.25f;

        if (scoped && _sniper)
            targetFOV = Mathf.Clamp(_sniper.GetScopedTargetFOV(baseFOV), _sniper.sniperMinFOV, 120f);
        else
            targetFOV = Mathf.Clamp(ads ? adsFOV : baseFOV, 5f, 120f);

        _text =
            $"<b>FPS</b>: {_fps:0}\n" +
            $"<b>Speed</b>: {_speedSmoothed:0.00} m/s  ({kmh:0.0} km/h)\n" +
            $"<b>FOV</b>: {camFOV:0.0}°   ADS: {(ads ? "YES" : "no")}   Scope: {(scoped ? "YES" : "no")}\n" +
            $"DefaultFOV: {baseFOV:0.###}\n" +
            $"TargetFOV:  {targetFOV:0.###}\n" +
            $"RealFOV:    {camFOV:0.###}\n" +
            $"<b>Weapon</b>: {weaponName}\n" +
            $"Load: move x{moveMul}  stamina x{stamMul}\n" +
            $"...\n{ccInfo}\n..." +
            $"Interactor range: {interRange}";
    }


    // Zwraca aktualny InventoryItemData i (opcjonalnie) WeaponItemData oraz nazwę do HUD.
    // Działa dla: broń palna, melee, granat, oraz Hands (gdy nic nie trzymasz).
    bool TryGetCurrentItem(out InventoryItemData itemData, out WeaponItemData weaponData, out string displayName)
    {
        itemData = null;
        weaponData = null;
        displayName = "—";

        GameObject cur = _wm ? _wm.GetCurrentWeaponSlotObject() : null;

        if (cur)
        {
            var gun = cur.GetComponentInChildren<Gun>(true);
            var melee = cur.GetComponentInChildren<Melee>(true);
            var nade = cur.GetComponentInChildren<Grenade>(true);

            if (gun && gun.gameObject.activeInHierarchy)
            {
                itemData = (gun.inventoryInstance != null) ? gun.inventoryInstance.data as InventoryItemData
                                                             : gun.weaponData;
                weaponData = gun.weaponData;
            }
            else if (melee && melee.gameObject.activeInHierarchy)
            {
                itemData = (melee.inventoryInstance != null) ? melee.inventoryInstance.data as InventoryItemData
                                                               : null;
                weaponData = itemData as WeaponItemData;
            }
            else if (nade && nade.gameObject.activeInHierarchy)
            {
                itemData = (nade.inventoryInstance != null) ? nade.inventoryInstance.data as InventoryItemData
                                                              : null;
                weaponData = itemData as WeaponItemData;
            }
        }

        if (cur == null || (_wm && _wm.IsUsingHandsOnly()))
        {
            displayName = "Hands";
            return false;
        }

        if (itemData != null)
        {
            displayName = string.IsNullOrEmpty(itemData.itemName) ? itemData.name : itemData.itemName;
            if (string.IsNullOrEmpty(displayName) && weaponData != null)
                displayName = string.IsNullOrEmpty(weaponData.itemName) ? weaponData.name : weaponData.itemName;
            return true;
        }

        if (weaponData != null)
        {
            displayName = string.IsNullOrEmpty(weaponData.itemName) ? weaponData.name : weaponData.itemName;
            return true;
        }

        return false;
    }


    void OnGUI()
    {
        if (!visible) return;

        // ---- MAIN HUD PANEL ----
        if (_style == null)
            _style = new GUIStyle(GUI.skin.label) { fontSize = fontSize, richText = true };

        float boxW = 320f;
        float contentH = Mathf.Max(110f, _style.CalcHeight(new GUIContent(_text), boxW - 16f) + 12f);

        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.Box(new Rect(pivot.x, pivot.y, boxW, contentH), GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(new Rect(pivot.x + 8f, pivot.y + 6f, boxW - 16f, contentH - 12f), _text, _style);

        // ---- OPTIONAL: RAY LABEL NEAR HIT ----
        if (debugRay && debugLabelInGameView && _cam != null)
        {
            Vector3 sp = _cam.WorldToScreenPoint(_dbgHitPoint);
            if (sp.z > 0f)
            {
                float rx = sp.x + 12f;
                float ry = Screen.height - sp.y - 12f;

                string who = _dbgHitCol
                    ? $"{_dbgHitCol.gameObject.name}  <size=11><i>(Layer {LayerMask.LayerToName(_dbgHitCol.gameObject.layer)})</i></size>"
                    : "—";
                string rtxt = $"<b>Ray</b>: {_dbgHitDist:0.00} m\n<b>Hit</b>: {who}";

                var rstyle = new GUIStyle(GUI.skin.box) { richText = true, fontSize = 13, alignment = TextAnchor.UpperLeft };
                Vector2 rsz = rstyle.CalcSize(new GUIContent(rtxt));
                Rect rr = new Rect(rx, ry, Mathf.Max(160f, rsz.x + 10f), rsz.y + 8f);

                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.Box(rr, GUIContent.none);
                GUI.color = Color.white;
                GUI.Label(new Rect(rr.x + 6f, rr.y + 4f, rr.width - 12f, rr.height - 8f), rtxt, rstyle);
            }
        }
    }


    void DebugRaycastAndDraw()
    {
        if (!debugRay) return;
        if (!_cam) _cam = Camera.main;
        if (!_cam) return;

        // Ray z kamery
        Vector3 origin = _cam.transform.position;
        Vector3 dir = _cam.transform.forward;
        float maxDist = (_pi && _pi.pickupRange > 0f) ? _pi.pickupRange : 2.5f;

        _dbgRay = new Ray(origin, dir);

        // Szukamy pierwszego trafienia na warstwach przeszkód LUB pick-upów
        int mask = _pi ? (_pi.pickupLayer | _pi.obstacleLayer) : Physics.DefaultRaycastLayers;
        _dbgHasHit = Physics.Raycast(_dbgRay, out RaycastHit hit, maxDist, mask, QueryTriggerInteraction.Ignore);

        Vector3 end = origin + dir * maxDist;

        if (_dbgHasHit)
        {
            _dbgHitPoint = hit.point;
            _dbgHitNormal = hit.normal;
            _dbgHitCol = hit.collider;
            _dbgHitDist = hit.distance;

            // linia do trafienia
            Debug.DrawLine(origin, _dbgHitPoint, hitColor, 0f, false);
            Debug.DrawRay(_dbgHitPoint, _dbgHitNormal * 0.25f, hitColor, 0f, false);
        }
        else
        {
            _dbgHitPoint = end;
            _dbgHitNormal = Vector3.zero;
            _dbgHitCol = null;
            _dbgHitDist = maxDist;

            // linia do końca zasięgu
            Debug.DrawLine(origin, end, rayColor, 0f, false);
        }
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || !debugRay || _cam == null) return;

        Gizmos.color = _dbgHasHit ? hitColor : rayColor;
        Gizmos.DrawSphere(_dbgHitPoint, gizmoSphere);
    }


}