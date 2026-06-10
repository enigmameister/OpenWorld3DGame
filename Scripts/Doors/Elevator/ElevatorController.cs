using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ElevatorController : MonoBehaviour
{
    [Header("Konstrukcja")]
    public Transform car;                 // kabina
    public Transform[] floors;            // pozycje pięter (parter -> 1 -> 2 -> ...)

    [Header("Drzwi")]
    public ElevatorDoor carDoor;
    public ElevatorDoor[] floorDoors;     // drzwi zewnętrzne

    [Header("Ruch")]
    public float moveSpeed = 2.5f;        // prędkość w m/s
    public float stopEps = 0.01f;         // tolerancja zatrzymania

    [Header("Kalibracja zatrzymania")]
    public Transform carFloorAnchor;   // kotwica na podłodze kabiny
    public float stopOffsetY = 0f;     // stała korekta, jeśli nie używasz kotwicy

    [Header("Czasy")]
    public float dwellTime = 2f;          // (jeśli używasz)
    public float arriveDelay = 0.75f;     // ⬅️ pauza po zatrzymaniu zanim otworzą się drzwi

    [Header("UI")]
    public TMP_Text currentLevelText;     // bieżące piętro
    public TMP_Text targetLevelText;      // docelowe piętro
    public TMP_Text[] displayAboveDoor;   // wyświetlacze nad drzwiami

    // --- animacja licznika (opcjonalnie) ---
    [Header("UI – animacja licznika")]
    public float floorTickPunch = 1.08f;   // krótkie „podbicie” skali
    public float floorTickTime = 0.10f;   // czas animacji jednego skoku
    public bool showArrow = true;         // dodaj „▲/▼” przy zmianie

    // przykładowo w ElevatorController:
    [SerializeField] private FloorIndicatorUI floorUI;

    [Header("Numeracja pięter (opcjonalnie)")]
    public int[] floorNumbers;    // np. [-1, 0, 1]

    // ===== runtime =====
    readonly Queue<int> _queue = new Queue<int>();
    int _currentFloor = 0;
    int _targetFloor = -1;
    bool _busy;

    bool _waitingForCarRequest = false;   // czekam na wybór w kabinie
    bool _isMoving = false;               // czy winda aktualnie jedzie

    // --- pasażerowie (anti-jitter) ---
    HashSet<CharacterController> _passengers = new HashSet<CharacterController>();
    Dictionary<CharacterController, float> _skipUntil = new Dictionary<CharacterController, float>();
    float _pendingDeltaY;  // suma delt w tej klatce – aplikujemy w LateUpdate

    void Awake()
    {
        if (!car) { Debug.LogError($"[{name}] Brak 'car'!"); enabled = false; return; }
        if (floors == null || floors.Length == 0) { Debug.LogError($"[{name}] Brak 'floors'!"); enabled = false; return; }

        // nadaj rigidbody...
        var rb = car.GetComponent<Rigidbody>();
        if (!rb) rb = car.gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // najpierw ustal faktyczne piętro startowe
        _currentFloor = FindClosestFloorIndex();
        SnapCarToFloor(_currentFloor);

        // dopiero teraz ustaw UI
        if (floorUI) floorUI.Snap(GetFloorNumber(_currentFloor));

        UpdateDisplays();
    }

    void LateUpdate()
    {
        if (Mathf.Abs(_pendingDeltaY) > 1e-6f)
        {
            
            ApplyPendingDelta();
            _pendingDeltaY = 0f;
        }
    }

    void ApplyPendingDelta()
    {
        if (_passengers.Count == 0) return;

        Vector3 delta = new Vector3(0f, _pendingDeltaY, 0f);
        foreach (var cc in _passengers)
        {
            if (!cc || !cc.enabled || !cc.gameObject.activeInHierarchy) continue;
            if (_skipUntil.TryGetValue(cc, out float until) && Time.time < until)
                continue;
            cc.transform.position += delta;

        }
    }
    // ========================== PUBLIC API ===========================
    public void CallFromOutside(int floor)
    {
        if (!IsFloorValid(floor)) return;

        // jeśli przycisk na tym samym piętrze → nic nie kolejkuj, tylko upewnij się że drzwi są otwarte
        if (!_busy && floor == _currentFloor)
        {
            _waitingForCarRequest = true;
            if (carDoor) carDoor.Open(false);
            if (FloorDoorNow) FloorDoorNow.Open(false);
            UpdateDisplays();
            return;
        }

        // inne piętro → dodaj do kolejki
        EnqueueOnce(floor);

        // 🔧 KLUCZ: jeśli stoimy „w oczekiwaniu” z otwartymi drzwiami, zezwól na wyjazd do wezwania
        if (_waitingForCarRequest && !_busy)
            _waitingForCarRequest = false;

        TryNext();
    }

    /// <summary> Wybór piętra Z WEWNĄTRZ kabiny </summary>
    public void RequestFloor(int floor)
    {
        if (!IsFloorValid(floor) || floor == _currentFloor) return;

        EnqueueOnce(floor);

        // jeśli czekaliśmy na wybór w kabinie – kończ czekanie i rusz
        if (_waitingForCarRequest) _waitingForCarRequest = false;

        // pokaż strzałkę kierunku natychmiast po wyborze
        if (floorUI) floorUI.SetDirection(floor > _currentFloor ? +1 : -1);

        TryNext();
    }

    // pomocniczo:
    int GetFloorNumber(int index)
    {
        if (floorNumbers != null && index >= 0 && index < floorNumbers.Length)
            return floorNumbers[index];
        return index;
    }

    // ========================== LOGIKA JAZDY ===========================

    void TryNext()
    {
        if (_busy || _waitingForCarRequest || _queue.Count == 0) return;
        _targetFloor = _queue.Dequeue();

        if (floorUI)
        {
            float curY = floors[_currentFloor].position.y;
            float tgtY = floors[_targetFloor].position.y;
            int dir = tgtY > curY ? +1 : -1;
            floorUI.SetDirection(dir);
        }

        StartCoroutine(RunTo(_targetFloor));
    }

    IEnumerator RunTo(int targetFloor)
    {
        _busy = true;
        _targetFloor = targetFloor;      // dla targetLevelText
        UpdateDisplays();

        _isMoving = true;
        SetPassengersElevatorLock(true);

        // 1) Zamknij bezpiecznie obie pary drzwi
        yield return CloseBothDoorsSafely();
        if (carDoor) carDoor.SetLocked(true);
        if (FloorDoorNow) FloorDoorNow.SetLocked(true);

        // 2) Kalkulacja celu po Y z kalibracją podłogi
        Vector3 start = car.position;
        float startY = start.y;

        // docelowa wysokość piętra (StopPoint)
        float destY = floors[targetFloor].position.y;

        // jeśli masz kotwicę na podłodze kabiny – wyrównaj tak, by kotwica trafiła na destY
        if (carFloorAnchor)
        {
            float anchorDelta = carFloorAnchor.position.y - car.position.y; // odległość anchoru od pivotu kabiny
            destY -= anchorDelta;
        }
        else
        {
            // bez anchoru – stała korekta
            destY += stopOffsetY;
        }

        int dir = (destY - startY) >= 0f ? +1 : -1;

        // pokaż STARTOWE piętro + kierunek (żeby nie mignęła kreska "-")
        int shownFloor = ClosestFloorIndexByY(car.position.y);
        SetCurrentLevelUI(shownFloor, dir);
        SetDisplayAboveDoor(shownFloor);

        // 3) Jazda tylko po Y
        float prevY = car.position.y;
        while (Mathf.Abs(car.position.y - destY) > stopEps)
        {
            float newY = Mathf.MoveTowards(car.position.y, destY, moveSpeed * Time.deltaTime);
            car.position = new Vector3(start.x, newY, start.z);

            // — mijanie pięter (gdy zmieni się najbliższy indeks)
            int nearest = ClosestFloorIndexByY(newY);
            if (nearest != shownFloor)
            {
                shownFloor = nearest;
                SetCurrentLevelUI(shownFloor, dir);
                SetDisplayAboveDoor(shownFloor);
            }

            // — zbuforuj deltę dla pasażerów
            float dy = newY - prevY;
            if (Mathf.Abs(dy) > 1e-6f) _pendingDeltaY += dy;
            prevY = newY;

            yield return null;
        }

        // 4) Zatrzymanie na docelowym
        _currentFloor = targetFloor;
        _targetFloor = -1;
        UpdateDisplays();
        SetDisplayAboveDoor(_currentFloor);

        // ⬅️ pauza po dojechaniu
        if (arriveDelay > 0f)
            yield return new WaitForSeconds(arriveDelay);

        // 5) Otwórz i przejdź w tryb „czekam na wybór w kabinie”
        SetCurrentLevelUI(_currentFloor, 0);
        yield return OpenBothDoors();
        if (carDoor) carDoor.SetLocked(false);
        if (FloorDoorNow) FloorDoorNow.SetLocked(false);
        _waitingForCarRequest = true;

        _busy = false;
        _isMoving = false;
        SetPassengersElevatorLock(false);
        _pendingDeltaY = 0f;

    }

    // ========================== DRZWI ===========================

    IEnumerator OpenBothDoors()
    {
        Coroutine a = null, b = null;

        if (carDoor) a = StartCoroutine(carDoor.OpenRoutine());
        if (FloorDoorNow) b = StartCoroutine(FloorDoorNow.OpenRoutine());

        // ważne: startujemy oba, a dopiero potem czekamy
        if (a != null) yield return a;
        if (b != null) yield return b;
    }
    IEnumerator CloseBothDoorsSafely()
    {
        // dopóki ktoś stoi w świetle drzwi — utrzymuj otwarte
        while (IsSomethingInAnyZone())
        {
            Coroutine oa = null, ob = null;
            if (carDoor) oa = StartCoroutine(carDoor.OpenRoutine());
            if (FloorDoorNow) ob = StartCoroutine(FloorDoorNow.OpenRoutine());

            if (oa != null) yield return oa;
            if (ob != null) yield return ob;

            yield return null;
        }

        // zamykanie równolegle
        Coroutine ca = null, cb = null;

        if (carDoor) ca = StartCoroutine(carDoor.CloseRoutine(() => carDoor.Obstructed));
        if (FloorDoorNow) cb = StartCoroutine(FloorDoorNow.CloseRoutine(() => FloorDoorNow.Obstructed));

        if (ca != null) yield return ca;
        if (cb != null) yield return cb;
    }

    bool IsSomethingInAnyZone()
    {
        bool a = carDoor && carDoor.Obstructed;
        bool b = FloorDoorNow && FloorDoorNow.Obstructed;
        return a || b;
    }

    ElevatorDoor FloorDoorNow =>
        (floorDoors != null && _currentFloor >= 0 && _currentFloor < floorDoors.Length)
            ? floorDoors[_currentFloor] : null;

    // ========================== UI ===========================

    void UpdateDisplays()
    {
        string cur = GetFloorNumber(_currentFloor).ToString();
        string tgt = _targetFloor >= 0 ? GetFloorNumber(_targetFloor).ToString() : "-";

        if (targetLevelText) targetLevelText.text = tgt;

        // tablice nad drzwiami – pokazują faktyczny poziom po zatrzymaniu
        if (displayAboveDoor != null)
            for (int i = 0; i < displayAboveDoor.Length; i++)
                if (displayAboveDoor[i]) displayAboveDoor[i].text = cur;
    }

    void SetDisplayAboveDoor(int floor)
    {
        if (displayAboveDoor == null) return;
        string cur = GetFloorNumber(floor).ToString();
        for (int i = 0; i < displayAboveDoor.Length; i++)
            if (displayAboveDoor[i]) displayAboveDoor[i].text = cur;
    }


    // ========================== POMOCNICZE ===========================

    void EnqueueOnce(int floor)
    {
        if (!_queue.Contains(floor)) _queue.Enqueue(floor);
    }

    bool IsFloorValid(int i) => floors != null && i >= 0 && i < floors.Length;

    int FindClosestFloorIndex()
    {
        int best = 0;
        float bestD = float.MaxValue;
        for (int i = 0; i < floors.Length; i++)
        {
            float d = Mathf.Abs(car.position.y - floors[i].position.y);
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    void SnapCarToFloor(int i)
    {
        if (IsFloorValid(i))
        {
            Vector3 p = floors[i].position;
            car.position = new Vector3(car.position.x, p.y, car.position.z);
        }
    }

    int ClosestFloorIndexByY(float y)
    {
        int best = 0; float bestD = float.MaxValue;
        for (int i = 0; i < floors.Length; i++)
        {
            float d = Mathf.Abs(y - floors[i].position.y);
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    Coroutine _floorPunchCo;
    void SetCurrentLevelUI(int floor, int dir /* -1 dół, +1 góra, 0 brak */)
    {
        // najpierw przemapuj indeks -> numer piętra
        int num = GetFloorNumber(floor);

        // 1) Nowy licznik przewijany (SlideAB)
        if (floorUI)
        {
            if (dir == 0) floorUI.Snap(num);        // winda stoi
            else floorUI.TickTo(num, dir > 0);      // mijanie pięter
            return;
        }

        // 2) Fallback: stary TMP z „punch” (gdy floorUI NIE jest przypięty)
        if (currentLevelText)
        {
            string arrow = showArrow ? (dir > 0 ? " ▲" : dir < 0 ? " ▼" : "") : "";
            currentLevelText.text = num.ToString() + arrow;

            if (_floorPunchCo != null) StopCoroutine(_floorPunchCo);
            _floorPunchCo = StartCoroutine(FloorPunchAnim());
        }
    }
    IEnumerator FloorPunchAnim()
    {
        if (!currentLevelText) yield break;

        var t = currentLevelText.rectTransform;
        Vector3 baseScale = t.localScale;   // ⬅️ zapamiętujemy aktualny scale TMP!
        float time = 0f;

        while (time < floorTickTime)
        {
            time += Time.deltaTime;
            float k = time / floorTickTime;
            float s = Mathf.Lerp(floorTickPunch, 1f, k);
            t.localScale = baseScale * s;
            yield return null;
        }

        t.localScale = baseScale;
    }

    public void RegisterPassenger(CharacterController cc)
    {
        if (!cc) return;

        _passengers.Add(cc);

        // jeśli winda już jest w ruchu – natychmiast blokujemy pion fizyki gracza
        if (_isMoving)
        {
            var pm = cc.GetComponent<PlayerMovement>();
            if (pm != null) pm.elevatorLockVertical = true;
        }
    }
    public void UnregisterPassenger(CharacterController cc)
    {
        if (!cc) return;

        _passengers.Remove(cc);

        var pm = cc.GetComponent<PlayerMovement>();
        if (pm != null) pm.elevatorLockVertical = false;

        _skipUntil.Remove(cc);
    }


    // Zgłoś, że dany pasażer wcisnął skok – nie „podwozimy” go przez chwilę
    public void MarkPassengerJump(CharacterController cc, float duration = 0.25f)
    {
        if (!cc) return;
        _skipUntil[cc] = Time.time + duration;
    }

    // Zwraca true, jeśli dno kapsuły jest blisko podłogi kabiny (±eps)
    public bool IsContactingFloor(CharacterController cc, float eps = 0.06f)
    {
        if (!cc) return false;

        // wysokość podłogi kabiny w świecie:
        float carFloorY = carFloorAnchor ? carFloorAnchor.position.y
                                         : car.position.y; // jeśli nie używasz anchoru

        // dno kapsuły CC
        float ccBottomY = cc.bounds.min.y;

        return Mathf.Abs(ccBottomY - carFloorY) <= eps;
    }

    // ElevatorController.cs
    private void SetPassengersElevatorLock(bool value)
    {
        foreach (var cc in _passengers)
        {
            if (!cc) continue;
            var pm = cc.GetComponent<PlayerMovement>();
            if (pm != null)
                pm.elevatorLockVertical = value;
        }
    }

}
