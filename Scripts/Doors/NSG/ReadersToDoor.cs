using UnityEngine;

public class ReadersToDoor : MonoBehaviour
{
    [Header("Po³¹czenia")]
    public DoorInteract door;
    public ReaderZone[] readers;

    [Header("Czasy")]
    [Tooltip("Jeœli po odblokowaniu drzwi nie zostan¹ otwarte w tym czasie – ponownie siê zabezpiecz¹.")]
    public float unlockDurationIfNotOpened = 3f;

    [Header("Keypad (wspólny PIN)")]
    [Tooltip("Czy wszyscy ReaderZone podpiêci do tych drzwi maj¹ ten sam PIN?")]
    public bool useSharedKeypadCode = false;

    [Tooltip("Sekwencja wymagana na klawiaturze, np. *5321")]
    public string sharedKeypadCode = "*5321";


    // runtime
    bool accessActive;
    float accessEndTime;
    bool doorOpenedInThisWindow;
    ReaderZone lastReader;     // który czytnik odblokowa³ drzwi

    void OnEnable()
    {
        // podepnij siê pod eventy czytników
        if (readers == null) return;

        foreach (var r in readers)
        {
            if (!r) continue;
            r.onAccessGranted -= OnReaderAccessGranted;
            r.onAccessGranted += OnReaderAccessGranted;
        }
    }

    void OnDisable()
    {
        if (readers == null) return;

        foreach (var r in readers)
        {
            if (!r) continue;
            r.onAccessGranted -= OnReaderAccessGranted;
        }
    }

    void OnReaderAccessGranted(ReaderZone reader)
    {
        if (!door || !door.hasSecurity) return;

        lastReader = reader;
        accessActive = true;
        doorOpenedInThisWindow = false;
        accessEndTime = Time.time + unlockDurationIfNotOpened;

        // odblokuj drzwi – od teraz PressToOpen w DoorInteract zadzia³a
        door.UnlockDoor();

        // upewnij siê, ¿e ten czytnik œwieci na zielono
        if (lastReader != null)
            lastReader.SetAccessLight(true);
    }

    void Awake()
    {
        if (useSharedKeypadCode && readers != null)
        {
            foreach (var r in readers)
            {
                if (r != null)
                    r.SetExpectedCode(sharedKeypadCode);
            }
        }
    }

    void Update()
    {
        if (!accessActive || door == null || !door.hasSecurity)
            return;

        bool isOpen = door.IsOpen;
        bool isLocked = door.IsLocked();

        // jeœli coœ z zewn¹trz ju¿ zablokowa³o drzwi – koñczymy okno dostêpu
        if (isLocked)
        {
            EndAccessWindow();
            return;
        }

        if (isOpen)
        {
            // w tym oknie drzwi zosta³y faktycznie otwarte
            doorOpenedInThisWindow = true;
            return; // dopóki s¹ otwarte – nic nie robimy, czytnik zostaje zielony
        }

        // drzwi zamkniête
        if (doorOpenedInThisWindow)
        {
            // by³y otwarte i siê zamknê³y => natychmiast z powrotem secured
            door.LockDoor();
            EndAccessWindow();
        }
        else
        {
            // nie by³y jeszcze otwarte – pilnuj licznika czasu
            if (Time.time >= accessEndTime)
            {
                door.LockDoor();
                EndAccessWindow();
            }
        }
    }

    void EndAccessWindow()
    {
        accessActive = false;
        doorOpenedInThisWindow = false;

        if (lastReader != null)
        {
            lastReader.ResetToIdle(); // czerwone œwiat³o
            lastReader = null;
        }
    }
}
