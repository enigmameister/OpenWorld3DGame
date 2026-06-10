using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

[Serializable]
public class BankAccount
{
    public int accountId;
    public int balance;
    public bool locked;
    public List<string> issuedCardIds = new();


    public string citizenId;     // kto jest właścicielem konta (do UI)
    public long createdAtMin;    // czas gry w minutach: DayIndex*1440 + minuta dnia
}

[Serializable]
public class BankCardRecord
{
    public string cardId;
    public int accountId;
    public int pin;
    public long lastPinChangeAtMin;
    public BankCardStatus status;
    public int colorVariant;
    public long activateAt;
    public long lastVariantChangeAtMin;

    // ===== Card block/unblock metadata =====
    // Uzupełniane przez ATM / Card Operations.
    public BankCardBlockReason blockReason = BankCardBlockReason.None;

    // Kiedy karta została zablokowana (dla weryfikacji w panelu UNBLOCK)
    public int blockedDay;
    public int blockedMonth;
    public int blockedHour;
    public int blockedMinute;

    // Limit blokad wykonanych ręcznie przez gracza ("I BLOCKED")
    public int ownerBlockCount;
    public bool IsBlocked =>
    status == BankCardStatus.Blocked &&
    blockReason != BankCardBlockReason.None;
}

public enum BankCardBlockReason
{
    None = 0,
    PinFail3x = 1,
    OwnerBlocked = 2,
}

public enum BankTransactionType
{
    Deposit,
    Withdraw,
    TransferIn,
    TransferOut,
    LoanIn,
    LoanRepay
}

[Serializable]
public class BankTransactionRecord
{
    public int accountId;
    public int otherAccountId;   // 0 jeśli brak
    public string otherPartyName; // np. "BankSystem", "Shop 24/7", "Client #204"
    public int amount;
    public long utcTicks;
    public BankTransactionType type;
}

[Serializable]
public struct CloseAccountCheckResult
{
    public bool accountExists;
    public bool hasCurrentLoans;
    public bool balanceNotZero;
    public bool hasActiveCards;
    public bool allConditionsMet;

    public int balance;
    public int activeLoanCount;
    public int activeCardCount;
}

public class BankSystem : MonoBehaviour
{
    public static BankSystem Instance { get; private set; }

    [Header("Rules")]
    [SerializeField] private bool strictCardRegistry = true;
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Fees / Treasury")]
    [SerializeField, Range(0f, 0.25f)] private float transactionFeeRate = 0.01f;
    [SerializeField] private int bankTreasury = 0;
    [SerializeField] private bool enableTransactionFees = false; // <- OFF = brak prowizji
    public int BankTreasury => bankTreasury;
    public float TransactionFeeRate => transactionFeeRate;

    private readonly Dictionary<int, BankAccount> _accounts = new();
    private readonly Dictionary<string, BankCardRecord> _cards = new();

    private readonly Dictionary<int, List<BankTransactionRecord>> _transactionHistory = new();

    [Header("Card activation")]
    [Tooltip("Po ilu godzinach czasu gry karta Pending staje się Active")]
    [SerializeField, Min(0f)]
    private float cardActivationDelayHours = 1f; // domyślnie 1 godzina gry

    [Header("Card Variants (source of truth)")]
    [SerializeField] private BankCardVariantDatabase variantDb;

    [SerializeField, Min(1)] private int maxCardsPerAccount = 5;
    [SerializeField, Min(0)] private int defaultPinChangeCooldownMinutes = 60;

    [SerializeField, Min(0)] private int variantChangeCooldownMinutes = 1440;
    [SerializeField, Min(0)] private int variantChangePrice = 5;

    public int VariantChangeCooldownMinutes => variantChangeCooldownMinutes;
    public int VariantChangePrice => variantChangePrice;


    public int MaxCardsPerAccount => maxCardsPerAccount;
    public int DefaultPinChangeCooldownMinutes => defaultPinChangeCooldownMinutes;

    public int VariantCount => variantDb != null ? variantDb.Count : 0;
    public float CardActivationDelayHours => cardActivationDelayHours;

    public Color GetVariantColor(int index)
    {
        return variantDb != null ? variantDb.Get(index) : Color.white;
    }

    [Header("Save/Load")]
    [SerializeField] private bool enablePersistence = true;
    [SerializeField] private string saveFileName = "bank_save.json";
    [SerializeField] private bool prettyPrintJson = true;

    // dirty-save (żeby nie spamować dysku co klatkę)
    private bool _dirty;
    private float _dirtyTimer;
    [SerializeField] private float autosaveDelay = 1.0f;

    private string SavePath => System.IO.Path.Combine(Application.persistentDataPath, saveFileName);

    [Header("Dev")]
    [SerializeField] private bool devWipeStateOnStart = false;
    [SerializeField] private bool devDeleteSaveFileOnStart = false;

    [Header("Loan Debuging")]
    [SerializeField] private bool debugSessionOnly = false;
    [SerializeField] private bool debugDisableSaving = false;

    private const string PrefKey_Persistence = "BANK_PERSISTENCE_ENABLED";

    [Header("Inventory Integration")] // Tworzenie karty do zapisanego konta w InventoryUI gracza
    [SerializeField] private BankCardItemData bankCardItemDataForInventory;

    // --- GameTime hook (jedno źródło prawdy) ---
    private bool _timeHooked;
    private GameTimeSystem _timeSystem;
    private BankSaveData _save; // cache aktualnego save (w tym playerCards)

    // BankSystem.cs

    // [SerializeField] private bool debugAutoCreateMissingAccount = false;

    // citizenId -> accountId
    private readonly Dictionary<string, int> _citizenToAccount = new();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // 🔧 DEV MODE: start zawsze na czysto
        if (devWipeStateOnStart)
        {
            _accounts.Clear();
            _cards.Clear();
            _citizenToAccount.Clear();
            _dirty = false;

            if (devDeleteSaveFileOnStart)
            {
                try
                {
                    if (System.IO.File.Exists(SavePath))
                        System.IO.File.Delete(SavePath);
                }
                catch { }
            }
        }
        else
        {
            // 💾 NORMAL MODE: wczytaj zapis tylko jeśli persistencja włączona
            if (enablePersistence)
                LoadFromDisk();
        }

#if UNITY_EDITOR
        EnsureDevTestAccount();
#endif

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        // Spróbuj podpiąć się do czasu gry od razu (jeśli GameTimeSystem już istnieje)
        HookGameTimeIfNeeded();

        StartCoroutine(CoRestorePlayerCardsToInventory());
    }
    void Update()
    {
        if (!_timeHooked) HookGameTimeIfNeeded();

        if (!enablePersistence) return;
        if (!_dirty) return;

        _dirtyTimer -= Time.unscaledDeltaTime;
        if (_dirtyTimer <= 0f)
        {
            SaveToDisk();
            _dirty = false;
        }
    }

    private IEnumerator CoRestorePlayerCardsToInventory()
    {
        // 1) Poczekaj aż pojawi się PlayerStats i InventoryUI.Instance
        float timeout = 5f;
        while (timeout > 0f && (FindFirstObjectByType<PlayerStats>() == null || InventoryUI.Instance == null))
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        var ps = FindFirstObjectByType<PlayerStats>();
        var inv = InventoryUI.Instance;
        if (ps == null || inv == null) yield break;
        if (string.IsNullOrWhiteSpace(ps.citizenId)) yield break;

        // 2) Poczekaj aż InventoryUI zakończy Start() i wygeneruje sloty
        for (int i = 0; i < 30; i++)
            yield return null;

        var bank = BankSystem.Instance;
        if (bank == null) yield break;

        void RemoveAllCardsFromInventory()
        {
            var instances = inv.GetAllInstancesDistinct();
            for (int i = 0; i < instances.Count; i++)
            {
                var it = instances[i];
                if (it == null || it.data == null) continue;

                bool isCardType = it.data is BankCardItemData;
                bool isCardMeta = it.hasBankCardMeta;

                if (!isCardType && !isCardMeta) continue;

                inv.RemoveItem(it, it.count);
            }
        }

        // ✅ Brak konta -> usuń karty i wyjdź
        if (!bank.TryGetAccountForCitizen(ps.citizenId, out var account))
        {
            RemoveAllCardsFromInventory();
            yield break;
        }

        if (bankCardItemDataForInventory == null)
        {
            Debug.LogWarning("[BANK] bankCardItemDataForInventory not set. Cannot restore cards to inventory.");
            yield break;
        }

        // 3) Pobierz listę kart z konta (TO jest źródło prawdy)
        int activeAccountId = account.accountId;

        var issued = bank.GetCardsForAccount(activeAccountId, includeDeleted: false);
        var validIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < issued.Count; i++)
        {
            var rec = issued[i];
            if (rec == null) continue;

            // tylko niepuste id
            if (!string.IsNullOrWhiteSpace(rec.cardId))
                validIds.Add(rec.cardId);
        }

        // 4) (Opcjonalnie, ale polecam) posprzątaj playerCards, żeby już nie wracały stare ID
        if (_save != null && _save.playerCards != null)
        {
            PlayerCardsSave p = null;
            for (int i = 0; i < _save.playerCards.Count; i++)
            {
                var entry = _save.playerCards[i];
                if (entry != null && string.Equals(entry.citizenId, ps.citizenId, StringComparison.OrdinalIgnoreCase))
                {
                    p = entry;
                    break;
                }
            }

            if (p != null)
            {
                if (p.cardIds == null) p.cardIds = new List<string>();

                // usuń wszystko co nie jest w validIds
                p.cardIds.RemoveAll(id => string.IsNullOrWhiteSpace(id) || !validIds.Contains(id));

                // dodaj brakujące z konta (żeby był pełny sync)
                foreach (var id in validIds)
                    if (!p.cardIds.Exists(x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase)))
                        p.cardIds.Add(id);

                MarkDirty(); // żeby zapisało
            }
        }

        // 5) HARD SYNC inventory: usuń każdą kartę, która nie jest w validIds
        {
            var instances = inv.GetAllInstancesDistinct();
            for (int i = 0; i < instances.Count; i++)
            {
                var it = instances[i];
                if (it == null || it.data == null) continue;

                bool isCardType = it.data is BankCardItemData;
                bool hasMeta = it.hasBankCardMeta;
                if (!isCardType && !hasMeta) continue;

                string cardId = hasMeta ? it.bankCard.cardId : null;

                // "goła" karta albo spoza konta -> usuń
                if (string.IsNullOrWhiteSpace(cardId) || !validIds.Contains(cardId))
                    inv.RemoveItem(it, it.count);
            }
        }

        // 6) Zbuduj set kart już w inventory (po syncu)
        var existingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        {
            var instances = inv.GetAllInstancesDistinct();
            for (int i = 0; i < instances.Count; i++)
            {
                var it = instances[i];
                if (it == null) continue;

                if (it.hasBankCardMeta)
                {
                    var id = it.bankCard.cardId;
                    if (!string.IsNullOrWhiteSpace(id))
                        existingIds.Add(id);
                }
            }
        }

        // 7) Dodaj brakujące karty: UWAGA -> iterujemy po validIds, NIE po p.cardIds
        foreach (var cardId in validIds)
        {
            if (existingIds.Contains(cardId)) continue;

            if (!TryGetCardRecordEffective(cardId, out var rec) || rec == null)
                continue;

            var newInst = new InventoryItemInstance(bankCardItemDataForInventory) { count = 1 };
            newInst.hasBankCardMeta = true;
            newInst.bankCard = new BankCardMeta
            {
                cardId = rec.cardId,
                accountId = rec.accountId,
                pin = rec.pin,
                status = rec.status,
                colorVariant = rec.colorVariant,
                activateAt = rec.activateAt
            };

            if (!inv.TryAddItem(newInst))
                inv.GiveOrDrop(newInst);

            existingIds.Add(cardId);
        }
    }

    // ---------- Accounts ----------
    public BankAccount CreateAccount(int initialBalance = 0, int? forcedAccountId = null)
    {
        int id = forcedAccountId ?? GenerateUniqueAccountId();
        var acc = new BankAccount { accountId = id, balance = Mathf.Max(0, initialBalance) };
        _accounts[id] = acc;

        MarkDirty();
        return acc;
    }

    // -------- TAX -------------

    private int CalcFee(int amount)
    {
        if (amount <= 0) return 0;
        return Mathf.Max(1, Mathf.CeilToInt(amount * transactionFeeRate)); // min 1
    }

    public bool TryGetAccount(int accountId, out BankAccount account) => _accounts.TryGetValue(accountId, out account);

    public bool TryGetAccountInfo(int accountId, out BankAccount acc) => _accounts.TryGetValue(accountId, out acc);

    public bool TryGetAccountInfoForCitizen(string citizenId, out BankAccount acc)
    {
        acc = null;
        if (string.IsNullOrWhiteSpace(citizenId)) return false;
        if (!_citizenToAccount.TryGetValue(citizenId, out var id)) return false;
        return _accounts.TryGetValue(id, out acc);
    }
    public int GetBalance(int accountId) => _accounts.TryGetValue(accountId, out var a) ? a.balance : 0;

    public bool DepositFromPlayer(
        int accountId,
        int amount,
        out int feeCharged,
        out int netCredited)
    {
        feeCharged = 0;
        netCredited = 0;

        if (amount <= 0) return false;
        if (!_accounts.TryGetValue(accountId, out var acc)) return false;
        if (acc.locked) return false;

        feeCharged = enableTransactionFees ? CalcFee(amount) : 0;
        netCredited = Mathf.Max(0, amount - feeCharged);
        if (netCredited < 0) netCredited = 0;

        acc.balance += netCredited;
        bankTreasury += feeCharged;

        AddTransactionRecord(accountId, 0, "CASH", netCredited, BankTransactionType.Deposit);

        MarkDirty();
        return true;
    }

    public bool WithdrawToPlayer(
        int accountId,
        int amount,
        out int feeCharged,
        out int totalDebited)
    {
        feeCharged = 0;
        totalDebited = 0;

        if (amount <= 0) return false;
        if (!_accounts.TryGetValue(accountId, out var acc)) return false;
        if (acc.locked) return false;

        feeCharged = enableTransactionFees ? CalcFee(amount) : 0;
        totalDebited = amount + feeCharged;

        if (acc.balance < totalDebited) return false;

        acc.balance -= totalDebited;
        bankTreasury += feeCharged;

        AddTransactionRecord(accountId, 0, "CASH", amount, BankTransactionType.Withdraw);

        MarkDirty();
        return true;
    }

    // ---------- Cards ----------
    public bool TryGetCard(string cardId, out BankCardRecord record)
    {
        record = null;
        if (string.IsNullOrWhiteSpace(cardId)) return false;
        return _cards.TryGetValue(cardId, out record);
    }

    public BankCardRecord IssueCard(
        int accountId,
        int pin,
        int colorVariant,
        BankCardStatus initialStatus = BankCardStatus.Active,
        string forcedCardId = null
    )
    {
        if (!_accounts.ContainsKey(accountId))
            CreateAccount(0, accountId);

        var cards = GetCardsForAccount(accountId);
        if (cards.Count >= MaxCardsPerAccount)
            return null;

        string cid = forcedCardId ?? GenerateUniqueCardId();

        long nowMin = GetGameMinutesNow();
        long delayMin = Mathf.RoundToInt(cardActivationDelayHours * 60f);

        var rec = new BankCardRecord
        {
            cardId = cid,
            accountId = accountId,
            pin = Mathf.Clamp(pin, 0, 9999),
            status = initialStatus,
            colorVariant = Mathf.Max(0, colorVariant),
            activateAt = (initialStatus == BankCardStatus.Pending && delayMin > 0)
                ? nowMin + delayMin
                : 0
        };

        _cards[cid] = rec;

        var list = _accounts[accountId].issuedCardIds;
        if (!list.Contains(cid))
            list.Add(cid);

        MarkDirty();
        return rec;
    }

    public bool ChangePin(string cardId, int currentPin, int newPin)
    {
        if (!TryGetCard(cardId, out var c)) return false;
        if (c.status != BankCardStatus.Active) return false;
        if (c.pin != currentPin) return false;
        if (newPin == currentPin) return false;

        c.pin = Mathf.Clamp(newPin, 0, 9999);
        MarkDirty();
        return true;
    }

    public bool ValidatePin(string cardId, int typedPin)
    {
        if (!TryGetCard(cardId, out var c)) return false;
        if (c.status != BankCardStatus.Active) return false;

        // opcjonalnie: konto locked
        if (!_accounts.TryGetValue(c.accountId, out var acc)) return false;
        if (acc.locked) return false;

        return c.pin == typedPin;
    }

    public bool ChangeCardStatus(string cardId, BankCardStatus newStatus)
    {
        if (!TryGetCard(cardId, out var c)) return false;

        c.status = newStatus;

        // jeśli ktoś zmieni status na nie-Blocked, wyczyść powód blokady
        if (newStatus != BankCardStatus.Blocked)
        {
            c.blockReason = BankCardBlockReason.None;
            c.blockedDay = 0;
            c.blockedMonth = 0;
            c.blockedHour = 0;
            c.blockedMinute = 0;
        }

        MarkDirty();
        return true;
    }

    // =====================
    // CARD BLOCK (SOURCE OF TRUTH)
    // =====================

    public bool SetCardBlockReason(string cardId, BankCardBlockReason reason)
    {
        if (!TryGetCard(cardId, out var c) || c == null) return false;

        c.blockReason = reason;

        // jeśli ustawiamy powód, to upewnij się że status jest Blocked
        if (reason != BankCardBlockReason.None)
            c.status = BankCardStatus.Blocked;

        // zapis "czasu" blokady tylko gdy reason != None
        if (reason != BankCardBlockReason.None)
            StampBlockTime(c);

        MarkDirty();
        return true;
    }

    public bool BlockCardAsOwnerBlocked(string cardId)
    {
        if (!TryGetCard(cardId, out var c) || c == null) return false;

        c.status = BankCardStatus.Blocked;
        c.blockReason = BankCardBlockReason.OwnerBlocked;
        c.ownerBlockCount++;                 // licz ręczne blokady (limit w TryUnblockCard_OwnerBlocked)
        StampBlockTime(c);

        MarkDirty();
        return true;
    }

    public bool BlockCardAsPinFail3x(string cardId)
    {
        if (!TryGetCard(cardId, out var c) || c == null) return false;

        c.status = BankCardStatus.Blocked;
        c.blockReason = BankCardBlockReason.PinFail3x;
        StampBlockTime(c);

        MarkDirty();
        return true;
    }

    private void StampBlockTime(BankCardRecord c)
    {
        // Bezpiecznie: spróbuj wyciągnąć day/month/hour/min z GameTimeSystem/DayNightCycle REFLECTION,
        // żeby nie zgadywać nazw pól i nie psuć kompilacji gdy ich nie ma.
        int day = 0, month = 0, hour = 0, minute = 0;

        if (!TryGetCalendarParts(out day, out month, out hour, out minute))
        {
            // fallback: policz z TotalMinutesSinceStart (miesiąc=0, dzień = dayIndex)
            long nowMin = GetGameMinutesNow();
            day = (int)(nowMin / 1440) + 1;
            month = 0;
            int mod = (int)(nowMin % 1440);
            hour = mod / 60;
            minute = mod % 60;
        }

        c.blockedDay = day;
        c.blockedMonth = month;
        c.blockedHour = hour;
        c.blockedMinute = minute;
    }

    // REFLECTION helper: próbuje odczytać popularne nazwy właściwości/pól
    private bool TryGetCalendarParts(out int day, out int month, out int hour, out int minute)
    {
        day = month = hour = minute = 0;

        object src = null;

        if (GameTimeSystem.Instance != null) src = GameTimeSystem.Instance;
        else
        {
            var dnc = FindFirstObjectByType<DayNightCycle>();
            if (dnc != null) src = dnc;
        }

        if (src == null) return false;

        // ✅ SPECJALNIE DLA TWOJEGO GameTimeSystem
        if (src is GameTimeSystem gts)
        {
            var dt = gts.CurrentTime;
            day = dt.Day;
            month = dt.Month;
            hour = dt.Hour;
            minute = dt.Minute;
            return true;
        }

        // fallback: reflection dla innych systemów
        bool okDay = TryReadInt(src, new[] { "Day", "CurrentDay", "day", "currentDay", "DayOfMonth" }, out day);
        bool okMonth = TryReadInt(src, new[] { "Month", "CurrentMonth", "month", "currentMonth" }, out month);
        bool okHour = TryReadInt(src, new[] { "Hour", "CurrentHour", "hour", "currentHour" }, out hour);
        bool okMin = TryReadInt(src, new[] { "Minute", "CurrentMinute", "minute", "currentMinute" }, out minute);

        return okHour && okMin && okDay; // month może zostać 0, ale day/h/m muszą być
    }

    private bool TryReadInt(object obj, string[] names, out int value)
    {
        value = 0;
        var t = obj.GetType();

        for (int i = 0; i < names.Length; i++)
        {
            var n = names[i];

            // property
            var p = t.GetProperty(n);
            if (p != null && p.PropertyType == typeof(int))
            {
                value = (int)p.GetValue(obj);
                return true;
            }

            // field
            var f = t.GetField(n);
            if (f != null && f.FieldType == typeof(int))
            {
                value = (int)f.GetValue(obj);
                return true;
            }
        }

        return false;
    }

    public bool CanUseCard(string cardId, int accountIdFromCard, BankCardStatus statusFromCard)
    {
        if (strictCardRegistry)
        {
            if (!TryGetCard(cardId, out var c)) return false;
            if (c.status != BankCardStatus.Active) return false;
            if (!_accounts.TryGetValue(c.accountId, out var acc)) return false;
            if (acc.locked) return false;
            return true;
        }
        else
        {
            // loose/hack: pozwól operować samym accountId, jeśli konto istnieje
            if (!_accounts.TryGetValue(accountIdFromCard, out var acc)) return false;
            if (acc.locked) return false;
            if (statusFromCard == BankCardStatus.Blocked) return false;
            return true;
        }
    }

    public int GetPinChangeCooldownRemainingMinutes(string cardId, int cooldownMinutes)
    {
        if (!TryGetCard(cardId, out var c)) return 0;

        if (cooldownMinutes <= 0) return 0;
        if (c.lastPinChangeAtMin <= 0) return 0;

        long now = GetGameMinutesNow();
        long elapsed = now - c.lastPinChangeAtMin;
        long remain = cooldownMinutes - elapsed;

        return (int)Mathf.Max(0, remain);
    }


    public bool TryChangeCardPin(
       string cardId,
       int currentPin,
       int newPin,
       int cooldownMinutes,
       out string reason
   )
    {
        reason = "";

        if (!TryGetCard(cardId, out var c))
        {
            reason = "Card not found";
            return false;
        }

        if (c.status != BankCardStatus.Active && c.status != BankCardStatus.Pending)
        {
            reason = "Card is not active";
            return false;
        }

        if (c.pin != currentPin)
        {
            reason = "Wrong current PIN";
            return false;
        }

        if (newPin == currentPin)
        {
            reason = "New PIN same as current";
            return false;
        }

        int remain = GetPinChangeCooldownRemainingMinutes(cardId, cooldownMinutes);
        if (remain > 0)
        {
            reason = $"Cooldown {remain} min";
            return false;
        }

        c.pin = Mathf.Clamp(newPin, 0, 9999);
        c.lastPinChangeAtMin = GetGameMinutesNow();
        MarkDirty();
        return true;
    }


    // ---------- ID gen ----------
    private int GenerateUniqueAccountId()
    {
        for (int i = 0; i < 200; i++)
        {
            int id = UnityEngine.Random.Range(1000, 99999);
            if (!_accounts.ContainsKey(id)) return id;
        }
        int fallback = 1000;
        while (_accounts.ContainsKey(fallback)) fallback++;
        return fallback;
    }

    private string GenerateUniqueCardId()
    {
        // zakres 1001–9999
        for (int i = 0; i < 200; i++)
        {
            int n = UnityEngine.Random.Range(1001, 10000); // 10000 exclusive
            string id = n.ToString();

            if (!_cards.ContainsKey(id))
                return id;
        }

        // fallback – znajdź pierwsze wolne
        for (int n = 1001; n <= 9999; n++)
        {
            string id = n.ToString();
            if (!_cards.ContainsKey(id))
                return id;
        }

        throw new Exception("[BANK] Brak wolnych numerów kart (1001–9999)");
    }

    // Sprawdza czy gracz ma wyrobione ID (czyli czy w ogóle znamy jego citizenId)
    public bool HasCitizenId(string citizenId)
    {
        return !string.IsNullOrWhiteSpace(citizenId);
    }

    // Sprawdza czy pod danym ID jest konto w NASZYM banku
    public bool TryGetAccountForCitizen(string citizenId, out BankAccount account)
    {
        account = null;
        if (string.IsNullOrWhiteSpace(citizenId)) return false;

        if (_citizenToAccount.TryGetValue(citizenId, out var accId))
            return TryGetAccount(accId, out account);

        return false;
    }

    // Tworzy konto i przypina do citizenId
    public BankAccount CreateAccountForCitizen(string citizenId, int initialBalance = 0, int? forcedAccountId = null)
    {
        if (string.IsNullOrWhiteSpace(citizenId))
            throw new ArgumentException("citizenId is empty");

        if (_citizenToAccount.TryGetValue(citizenId, out var existingId) &&
            TryGetAccount(existingId, out var existingAcc))
            return existingAcc;

        var acc = CreateAccount(initialBalance, forcedAccountId);

        // NEW:
        acc.citizenId = citizenId;
        acc.createdAtMin = GetGameMinutesNow(); // masz już tę metodę w BankSystem :contentReference[oaicite:2]{index=2}

        _citizenToAccount[citizenId] = acc.accountId;

        MarkDirty(); // jeśli masz persistence w tej wersji
        return acc;
    }

    public int GenerateUniqueAccountIdInRange(int minInclusive, int maxInclusive)
    {
        minInclusive = Mathf.Clamp(minInclusive, 1, 9999999);
        maxInclusive = Mathf.Max(minInclusive, maxInclusive);

        for (int i = 0; i < 500; i++)
        {
            int id = UnityEngine.Random.Range(minInclusive, maxInclusive + 1);
            if (!_accounts.ContainsKey(id)) return id;
        }

        // fallback: liniowo
        for (int id = minInclusive; id <= maxInclusive; id++)
            if (!_accounts.ContainsKey(id)) return id;

        // jakby full, wróć do normalnego generatora
        return GenerateUniqueAccountId();
    }

    private long GetGameMinutesNow()
    {
        // Źródło prawdy: GameTimeSystem (czas gry)
        if (GameTimeSystem.Instance != null)
            return GameTimeSystem.Instance.TotalMinutesSinceStart;

        // Fallback (gdy ktoś odpali scenę bez GameTimeSystem)
        var dnc = FindFirstObjectByType<DayNightCycle>();
        if (dnc != null) return dnc.GetTotalMinutesNow();
        return (long)(Time.timeSinceLevelLoad / 60f);
    }
    public bool TryGetCardRecordEffective(string cardId, out BankCardRecord rec)
    {
        rec = null;
        if (string.IsNullOrWhiteSpace(cardId)) return false;

        if (!TryGetCard(cardId, out rec)) return false;
        if (rec == null) return false;

        if (rec.status == BankCardStatus.Pending && rec.activateAt > 0)
        {
            long nowMin = GetGameMinutesNow();
            if (nowMin >= rec.activateAt)
            {
                rec.status = BankCardStatus.Active;
                rec.activateAt = 0;
                _cards[cardId] = rec;

                MarkDirty();
            }
        }


        return true;
    }

    private void CheckPendingCards(long nowMin)
    {
        bool changed = false;

        foreach (var kv in _cards)
        {
            var card = kv.Value;

            if (card.status == BankCardStatus.Pending && card.activateAt > 0)
            {
                if (nowMin >= card.activateAt)
                {
                    card.status = BankCardStatus.Active;
                    card.activateAt = 0;
                    changed = true;

                    Debug.Log($"[BANK] Card {card.cardId} activated automatically (game minutes)");
                }
            }
        }

        if (changed) MarkDirty();
    }

    // wygodny wrapper (gdy ktoś jednak wywoła bez parametru)
    private void CheckPendingCards()
    {
        CheckPendingCards(GetGameMinutesNow());
    }


    [Serializable]
    public class BankSaveData
    {
        public int nextAccountId;
        public int nextCardId;

        public int playerCash;
        public string playerCitizenId;

        public int version = 1;
        public int bankTreasury;
        public float transactionFeeRate;

        public List<BankAccount> accounts = new();
        public List<BankCardRecord> cards = new();
        public List<CitizenAccountLink> citizenLinks = new();
        public List<PlayerCardsSave> playerCards = new();
    }

    [Serializable]
    public class PlayerCardsSave
    {
        public string citizenId;
        public List<string> cardIds = new();
    }

    [Serializable]
    public class CitizenAccountLink
    {
        public string citizenId;
        public int accountId;
    }

    private void MarkDirty()
    {
        if (!enablePersistence) return;
        _dirty = true;
        _dirtyTimer = autosaveDelay;
    }
    private int MaxAccountId()
    {
        int max = 0;
        foreach (var id in _accounts.Keys) if (id > max) max = id;
        return max;
    }

    private int MaxCardId()
    {
        int max = 0;
        foreach (var id in _cards.Keys)
            if (int.TryParse(id, out var n) && n > max) max = n;
        return max;
    }

    public void SaveToDisk()
    {
        try
        {
            var data = new BankSaveData
            {
                version = 1,
                bankTreasury = bankTreasury,
                transactionFeeRate = transactionFeeRate,
                accounts = new List<BankAccount>(_accounts.Values),
                cards = new List<BankCardRecord>(_cards.Values),
                citizenLinks = new List<CitizenAccountLink>(),
                playerCards = (_save != null && _save.playerCards != null)
                    ? new List<PlayerCardsSave>(_save.playerCards)
                    : new List<PlayerCardsSave>(),
                nextAccountId = _accounts.Count == 0 ? 1000 : Mathf.Max(1000, MaxAccountId() + 1),
                nextCardId = _cards.Count == 0 ? 1001 : Mathf.Max(1001, MaxCardId() + 1)
            };

            // 🔹 PRZENIESIONE PRZED zapisem do pliku
            var ps = FindFirstObjectByType<PlayerStats>();
            if (ps != null)
            {
                data.playerCash = ps.money;
                data.playerCitizenId = ps.citizenId;
            }

            foreach (var kv in _citizenToAccount)
            {
                data.citizenLinks.Add(new CitizenAccountLink
                {
                    citizenId = kv.Key,
                    accountId = kv.Value
                });
            }

            string json = JsonUtility.ToJson(data, prettyPrintJson);
            File.WriteAllText(SavePath, json);

            _save = data;
        }
        catch (Exception e)
        {
            Debug.LogError($"[BANK] Save failed: {e}");
        }
    }

    public void LoadFromDisk()
    {
        try
        {
            if (!System.IO.File.Exists(SavePath))
            {
                // Debug.Log("[BANK] No save file, starting fresh.");
                return;
            }

            string json = System.IO.File.ReadAllText(SavePath);
            var data = JsonUtility.FromJson<BankSaveData>(json);

            if (data == null)
            {
                Debug.LogWarning("[BANK] Save file exists but could not be parsed. Starting fresh.");
                return;
            }

            _save = data;
            if (_save.playerCards == null) _save.playerCards = new List<PlayerCardsSave>();

            // przywróć proste pola
            bankTreasury = data.bankTreasury;
            transactionFeeRate = data.transactionFeeRate;

            // odbuduj słowniki
            _accounts.Clear();
            _cards.Clear();
            _citizenToAccount.Clear();

            if (data.accounts != null)
            {
                foreach (var acc in data.accounts)
                {
                    if (acc == null) continue;
                    _accounts[acc.accountId] = acc;
                }
            }

            // ✅ DODAJ: odbuduj mapowanie z samego konta (gdy citizenLinks było puste)
            foreach (var acc in _accounts.Values)
            {
                if (acc == null) continue;
                if (string.IsNullOrWhiteSpace(acc.citizenId)) continue;

                // jeśli link nie istniał w save (albo jest pusty), odzyskaj go
                if (!_citizenToAccount.ContainsKey(acc.citizenId))
                    _citizenToAccount[acc.citizenId] = acc.accountId;
            }

            var ps = FindFirstObjectByType<PlayerStats>();
            if (ps != null)
            {
                // jeśli chcesz chronić przed “innym graczem”:
                if (string.IsNullOrEmpty(ps.citizenId) || ps.citizenId == data.playerCitizenId)
                    ps.money = data.playerCash;
            }


            if (data.cards != null)
            {
                foreach (var c in data.cards)
                {
                    if (c == null || string.IsNullOrWhiteSpace(c.cardId)) continue;
                    _cards[c.cardId] = c;

                    // opcjonalna naprawa spójności: dopnij cardId do account.issuedCardIds
                    if (_accounts.TryGetValue(c.accountId, out var acc))
                    {
                        acc.issuedCardIds ??= new List<string>();
                        if (!acc.issuedCardIds.Contains(c.cardId))
                            acc.issuedCardIds.Add(c.cardId);
                    }
                }
            }

            if (data.citizenLinks != null)
            {
                foreach (var link in data.citizenLinks)
                {
                    if (link == null || string.IsNullOrWhiteSpace(link.citizenId)) continue;
                    _citizenToAccount[link.citizenId] = link.accountId;
                }
            }

            // Debug.Log($"[BANK] Loaded from: {SavePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[BANK] Load failed: {e.Message}\n{e}");
        }
    }

    private void OnApplicationQuit()
    {
        if (debugDisableSaving)
        {
            Debug.Log("[BANK DEBUG] Saving disabled.");
            return;
        }

        if (debugSessionOnly)
        {
            Debug.Log("[BANK DEBUG] Session only mode - bank state not saved.");
            return;
        }

        if (!enablePersistence) return;
        SaveToDisk();
    }

    public string ExportToJson(bool pretty = false)
    {
        var data = new BankSaveData
        {
            version = 1,
            bankTreasury = bankTreasury,
            transactionFeeRate = transactionFeeRate,
            accounts = new List<BankAccount>(_accounts.Values),
            cards = new List<BankCardRecord>(_cards.Values),
            citizenLinks = new List<CitizenAccountLink>()
        };

        foreach (var kv in _citizenToAccount)
        {
            data.citizenLinks.Add(new CitizenAccountLink
            {
                citizenId = kv.Key,
                accountId = kv.Value
            });
        }

        return JsonUtility.ToJson(data, pretty);
    }

    public void ImportFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        var data = JsonUtility.FromJson<BankSaveData>(json);
        if (data == null) return;

        bankTreasury = data.bankTreasury;
        transactionFeeRate = data.transactionFeeRate;

        _accounts.Clear();
        _cards.Clear();
        _citizenToAccount.Clear();

        if (data.accounts != null)
            foreach (var acc in data.accounts)
                if (acc != null) _accounts[acc.accountId] = acc;

        if (data.cards != null)
        {
            foreach (var c in data.cards)
            {
                if (c == null || string.IsNullOrWhiteSpace(c.cardId)) continue;
                _cards[c.cardId] = c;

                if (_accounts.TryGetValue(c.accountId, out var acc))
                {
                    acc.issuedCardIds ??= new List<string>();
                    if (!acc.issuedCardIds.Contains(c.cardId))
                        acc.issuedCardIds.Add(c.cardId);
                }
            }
        }

        if (data.citizenLinks != null)
            foreach (var link in data.citizenLinks)
                if (link != null && !string.IsNullOrWhiteSpace(link.citizenId))
                    _citizenToAccount[link.citizenId] = link.accountId;

        // po imporcie nie zapisuj od razu
        _dirty = false;
    }

    public void SetPersistenceEnabled(bool enabled)
    {
        enablePersistence = enabled;
        PlayerPrefs.SetInt(PrefKey_Persistence, enabled ? 1 : 0);
        PlayerPrefs.Save();

        if (enablePersistence)
            LoadFromDisk();
        else
            _dirty = false;
    }

    public void WipeBankStateAndDeleteSave()
    {
        _accounts.Clear();
        _cards.Clear();
        _citizenToAccount.Clear();
        _dirty = false;

        try
        {
            if (System.IO.File.Exists(SavePath))
                System.IO.File.Delete(SavePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[BANK] Could not delete save file: {e.Message}");
        }
    }

    public bool TransferBetweenAccounts(int fromAccountId, int toAccountId, int amount)
    {
        if (amount <= 0) return false;
        if (fromAccountId == toAccountId) return false;

        if (!_accounts.TryGetValue(fromAccountId, out var from)) return false;
        if (!_accounts.TryGetValue(toAccountId, out var to)) return false;

        if (from.locked || to.locked) return false;
        if (from.balance < amount) return false;

        from.balance -= amount;
        to.balance += amount;

        AddTransactionRecord(fromAccountId, toAccountId, "", amount, BankTransactionType.TransferOut);
        AddTransactionRecord(toAccountId, fromAccountId, "", amount, BankTransactionType.TransferIn);

        MarkDirty();
        return true;
    }

    public List<BankCardRecord> GetCardsForAccount(int accountId, bool includeDeleted = false)
    {
        var result = new List<BankCardRecord>();

        if (!_accounts.TryGetValue(accountId, out var acc))
            return result;

        foreach (var cid in acc.issuedCardIds)
        {
            if (!_cards.TryGetValue(cid, out var card))
                continue;

            // "Deleted" u Ciebie = Revoked
            if (!includeDeleted && card.status == BankCardStatus.Revoked)
                continue;

            result.Add(card);
        }

        return result;
    }

    public int CountCardsForAccount(int accountId, bool includeRevoked = false)
    {
        int count = 0;

        foreach (var rec in _cards.Values)
        {
            if (rec.accountId != accountId) continue;
            if (!includeRevoked && rec.status == BankCardStatus.Revoked) continue;
            count++;
        }

        return count;
    }


    public bool AccountExists(int accountId) => _accounts.ContainsKey(accountId);

    public bool TransferNoFee(int fromAccountId, int toAccountId, int amount)
    {
        if (amount <= 0) return false;
        if (fromAccountId == toAccountId) return false;

        if (!_accounts.TryGetValue(fromAccountId, out var from)) return false;
        if (!_accounts.TryGetValue(toAccountId, out var to)) return false;
        if (from.locked || to.locked) return false;

        if (from.balance < amount) return false;

        from.balance -= amount;
        to.balance += amount;

        MarkDirty();
        return true;
    }

#if UNITY_EDITOR
    private void EnsureDevTestAccount()
    {
        const int testId = 2407;
        const int testBalance = 1993;

        if (!_accounts.TryGetValue(testId, out var acc))
        {
            acc = CreateAccount(initialBalance: testBalance, forcedAccountId: testId);
            Debug.Log($"[BANK DEV] Created test account {testId} balance={testBalance}$");
        }
        else
        {
            acc.balance = testBalance; // opcjonalnie: zawsze resetuj do testowej wartości
            Debug.Log($"[BANK DEV] Test account {testId} exists, forced balance={testBalance}$");
        }

        MarkDirty();
    }
#endif

    void OnEnable()
    {
        HookGameTimeIfNeeded();
    }

    void OnDisable()
    {
        UnhookGameTime();
    }

    private void HookGameTimeIfNeeded()
    {
        if (_timeHooked) return;

        _timeSystem = GameTimeSystem.Instance;
        if (_timeSystem == null) return;

        _timeSystem.OnMinuteChanged += OnGameMinuteChanged;
        _timeHooked = true;

        // przy podpięciu od razu wykonaj tick, żeby Pending mogły się aktywować po Load/SetTime
        OnGameMinuteChanged((int)_timeSystem.TotalMinutesSinceStart);
    }

    private void UnhookGameTime()
    {
        if (!_timeHooked) return;
        if (_timeSystem != null)
            _timeSystem.OnMinuteChanged -= OnGameMinuteChanged;

        _timeSystem = null;
        _timeHooked = false;
    }

    private void OnGameMinuteChanged(int nowMin)
    {
        // Jeden tick/minutę gry → odpal wszystkie czasowe rzeczy banku
        CheckPendingCards(nowMin);
    }

    private PlayerCardsSave GetOrCreatePlayerCards(string citizenId)
    {
        if (_save == null) return null;
        if (_save.playerCards == null) _save.playerCards = new List<PlayerCardsSave>();

        for (int i = 0; i < _save.playerCards.Count; i++)
        {
            var p = _save.playerCards[i];
            if (p != null && string.Equals(p.citizenId, citizenId, StringComparison.OrdinalIgnoreCase))
                return p;
        }

        var created = new PlayerCardsSave { citizenId = citizenId };
        _save.playerCards.Add(created);
        return created;
    }

    public void RegisterPlayerOwnedCard(string citizenId, string cardId)
    {
        if (string.IsNullOrWhiteSpace(citizenId) || string.IsNullOrWhiteSpace(cardId)) return;

        if (_save == null)
            _save = new BankSaveData();

        if (_save.playerCards == null)
            _save.playerCards = new List<PlayerCardsSave>();

        var p = _save.playerCards.FirstOrDefault(x =>
            x != null && string.Equals(x.citizenId, citizenId, StringComparison.OrdinalIgnoreCase));

        if (p == null)
        {
            p = new PlayerCardsSave { citizenId = citizenId, cardIds = new List<string>() };
            _save.playerCards.Add(p);
        }

        if (p.cardIds.Any(x => string.Equals(x, cardId, StringComparison.OrdinalIgnoreCase)))
            return;

        p.cardIds.Add(cardId);
        SaveToDisk();
    }

    // =====================
    // CARD UNBLOCK (NEW)
    // =====================

    // 7 argumentów (zgodnie z błędem): cardId + day + month + hour + minute + toleranceMinutes + out reason
    public bool TryUnblockCard_PinFail3x(
        string cardId,
        int day,
        int month,
        int hour,
        int minute,
        int toleranceMinutes,
        out string reason
    )
    {
        reason = "";

        if (!TryGetCard(cardId, out var c) || c == null)
        {
            reason = "Card not found";
            return false;
        }

        if (c.status != BankCardStatus.Blocked)
        {
            reason = "Card is not blocked";
            return false;
        }

        if (c.blockReason != BankCardBlockReason.PinFail3x)
        {
            reason = "Wrong block reason";
            return false;
        }

        // dzień/miesiąc MUSZĄ być identyczne
        if (c.blockedDay != day || c.blockedMonth != month)
        {
            reason = "Wrong date";
            return false;
        }

        // godzina/minuta mogą być w przybliżeniu
        int input = Mathf.Clamp(hour, 0, 23) * 60 + Mathf.Clamp(minute, 0, 59);
        int blocked = Mathf.Clamp(c.blockedHour, 0, 23) * 60 + Mathf.Clamp(c.blockedMinute, 0, 59);

        // różnica na kole 24h (żeby 23:59 vs 00:05 też działało)
        int diff = Mathf.Abs(input - blocked);
        diff = Mathf.Min(diff, 1440 - diff);

        if (diff > Mathf.Max(0, toleranceMinutes))
        {
            reason = "Wrong time";
            return false;
        }

        // OK -> odblokuj
        c.status = BankCardStatus.Active;
        c.blockReason = BankCardBlockReason.None;
        c.blockedDay = c.blockedMonth = c.blockedHour = c.blockedMinute = 0;

        MarkDirty();
        reason = "OK";
        return true;
    }

    public bool TryUnblockCard_OwnerBlocked(
        string cardId,
        out string reason,
        out int remaining
    )
    {
        reason = "";
        remaining = 0;

        if (!TryGetCard(cardId, out var c) || c == null)
        {
            reason = "Card not found";
            return false;
        }

        if (c.status != BankCardStatus.Blocked)
        {
            reason = "Card is not blocked";
            return false;
        }

        if (c.blockReason != BankCardBlockReason.OwnerBlocked)
        {
            reason = "Wrong block reason";
            return false;
        }

        const int LIMIT = 3;

        // jeśli limit przekroczony -> nie pozwalaj
        if (c.ownerBlockCount >= LIMIT)
        {
            remaining = 0;
            reason = "LIMIT";
            return false;
        }

        // OK -> odblokuj
        c.status = BankCardStatus.Active;
        c.blockReason = BankCardBlockReason.None;
        c.blockedDay = c.blockedMonth = c.blockedHour = c.blockedMinute = 0;

        remaining = Mathf.Max(0, LIMIT - c.ownerBlockCount);
        reason = "OK";

        MarkDirty();
        return true;
    }

    // =====================
    // CARD BLOCK (SOURCE OF TRUTH)
    // =====================

    public bool BlockCard_PinFail3x(string cardId)
    {
        return BlockCardInternal(cardId, BankCardBlockReason.PinFail3x, incrementOwnerCount: false);
    }

    public bool BlockCard_OwnerBlocked(string cardId)
    {
        return BlockCardInternal(cardId, BankCardBlockReason.OwnerBlocked, incrementOwnerCount: true);
    }

    private bool BlockCardInternal(string cardId, BankCardBlockReason reason, bool incrementOwnerCount)
    {
        if (!TryGetCard(cardId, out var c) || c == null) return false;

        c.status = BankCardStatus.Blocked;
        c.blockReason = reason;

        // zapisz czas blokady (do panelu UNBLOCK)
        GetGameDateTimeFallback(out c.blockedDay, out c.blockedMonth, out c.blockedHour, out c.blockedMinute);

        if (incrementOwnerCount)
            c.ownerBlockCount++;

        MarkDirty();
        return true;
    }

    // Bezpieczny fallback bez zależności od konkretnych pól GameTimeSystem
    private void GetGameDateTimeFallback(out int day, out int month, out int hour, out int minute)
    {
        // Fallback: licz z TotalMinutesSinceStart (dzień = dayIndex+1, miesiąc = 1)
        long totalMin = GetGameMinutesNow();

        int dayIndex = (int)(totalMin / 1440);
        int minOfDay = (int)(totalMin % 1440);

        hour = minOfDay / 60;
        minute = minOfDay % 60;

        day = dayIndex + 1;
        month = 1;
    }
    public bool TryDeleteCard(string cardId, out string reason)
    {
        return RevokeCardInternal(cardId, out _, out reason);
    }
    public bool CreditAccount(int accountId, int amount)
    {
        if (amount <= 0) return false;
        if (!_accounts.TryGetValue(accountId, out var acc)) return false;
        if (acc.locked) return false;

        acc.balance += amount;
        MarkDirty();
        return true;
    }

    public bool ApplyLoanInstallment(int accountId, int amount)
    {
        if (amount <= 0) return false;
        if (!_accounts.TryGetValue(accountId, out var acc)) return false;

        acc.balance -= amount;

        AddTransactionRecord(
            accountId,
            0,
            "BankSystem",
            amount,
            BankTransactionType.LoanRepay
        );

        MarkDirty();
        return true;
    }

    public bool TryRepayLoanFromBalance(int accountId, int amount)
    {
        if (amount <= 0) return false;
        if (!_accounts.TryGetValue(accountId, out var acc)) return false;
        if (acc.locked) return false;
        if (acc.balance < amount) return false;

        acc.balance -= amount;
        MarkDirty();
        return true;
    }

    private long GetCurrentUtcTicks()
    {
        if (GameTimeSystem.Instance != null)
            return GameTimeSystem.Instance.CurrentTime.Ticks;

        return DateTime.UtcNow.Ticks;
    }

    private void AddTransactionRecord(
        int accountId,
        int otherAccountId,
        string otherPartyName,
        int amount,
        BankTransactionType type)

    {
        if (accountId <= 0 || amount <= 0)
            return;

        if (!_transactionHistory.TryGetValue(accountId, out var list) || list == null)
        {
            list = new List<BankTransactionRecord>();
            _transactionHistory[accountId] = list;
        }

        list.Add(new BankTransactionRecord
        {
            accountId = accountId,
            otherAccountId = otherAccountId,
            otherPartyName = otherPartyName,
            amount = amount,
            utcTicks = GetCurrentUtcTicks(),
            type = type
        });
    }

    public List<BankTransactionRecord> GetTransactionHistory(int accountId)
    {
        var result = new List<BankTransactionRecord>();

        if (accountId <= 0)
            return result;

        if (_transactionHistory.TryGetValue(accountId, out var list) && list != null)
            result.AddRange(list);

        result.Sort((a, b) => b.utcTicks.CompareTo(a.utcTicks)); // najnowsze na górze
        return result;
    }

    public void AddLoanTransactionIn(int accountId, int amount)
    {
        AddTransactionRecord(accountId, 0, "BankSystem", amount, BankTransactionType.LoanIn);
    }

    public void AddLoanRepayTransaction(int accountId, int amount)
    {
        AddTransactionRecord(accountId, 0, "BankSystem", amount, BankTransactionType.LoanRepay);
    }

    public bool TryDebitAccount(int accountId, int amount, out string reason)
    {
        reason = "";

        if (amount <= 0)
        {
            reason = "INVALID AMOUNT";
            return false;
        }

        if (!_accounts.TryGetValue(accountId, out var acc) || acc == null)
        {
            reason = "ACCOUNT NOT FOUND";
            return false;
        }

        if (acc.locked)
        {
            reason = "ACCOUNT LOCKED";
            return false;
        }

        if (acc.balance < amount)
        {
            reason = "INSUFFICIENT FUNDS";
            return false;
        }

        acc.balance -= amount;
        MarkDirty();

        return true;
    }

    private bool RevokeCardInternal(string cardId, out BankCardRecord card, out string reason)
    {
        card = null;
        reason = "";

        if (string.IsNullOrWhiteSpace(cardId))
        {
            reason = "NO CARD ID";
            return false;
        }

        if (!TryGetCard(cardId, out var c) || c == null)
        {
            reason = "CARD NOT FOUND";
            return false;
        }

        c.status = BankCardStatus.Revoked;
        c.blockReason = BankCardBlockReason.None;
        c.blockedDay = c.blockedMonth = c.blockedHour = c.blockedMinute = 0;

        if (_save != null && _save.playerCards != null)
        {
            for (int i = 0; i < _save.playerCards.Count; i++)
            {
                var p = _save.playerCards[i];
                if (p?.cardIds == null) continue;
                p.cardIds.RemoveAll(id => string.Equals(id, cardId, StringComparison.OrdinalIgnoreCase));
            }
        }

        card = c;
        MarkDirty();
        reason = "OK";
        return true;
    }

    public bool TryReportCardStolen(
       string oldCardId,
       bool issueReplacement,
       int replacementVariant,
       int replacementFee,
       out BankCardRecord newCard,
       out string reason)
    {
        newCard = null;
        reason = "";

        if (string.IsNullOrWhiteSpace(oldCardId))
        {
            reason = "NO CARD ID";
            return false;
        }

        if (!TryGetCard(oldCardId, out var oldCard) || oldCard == null)
        {
            reason = "CARD NOT FOUND";
            return false;
        }

        if (oldCard.status == BankCardStatus.Revoked)
        {
            reason = "CARD ALREADY REVOKED";
            return false;
        }

        if (issueReplacement)
        {
            if (!_accounts.TryGetValue(oldCard.accountId, out var acc) || acc == null)
            {
                reason = "ACCOUNT NOT FOUND";
                return false;
            }

            int activeCards = CountCardsForAccount(oldCard.accountId, includeRevoked: false);
            if (activeCards >= MaxCardsPerAccount)
            {
                reason = "CARD LIMIT REACHED";
                return false;
            }

            if (replacementFee > 0 && !TryDebitAccount(oldCard.accountId, replacementFee, out reason))
                return false;
        }

        if (!RevokeCardInternal(oldCardId, out oldCard, out reason))
            return false;

        if (!issueReplacement)
        {
            reason = "OK";
            return true;
        }

        newCard = IssueCard(
            accountId: oldCard.accountId,
            pin: oldCard.pin,
            colorVariant: Mathf.Max(0, replacementVariant),
            initialStatus: BankCardStatus.Pending
        );

        if (newCard == null)
        {
            if (issueReplacement && replacementFee > 0)
                CreditAccount(oldCard.accountId, replacementFee);

            reason = "REPLACEMENT FAILED";
            return false;
        }

        reason = "OK";
        return true;
    }

    public BankCardItemData GetCardItemData()
    {
        return bankCardItemDataForInventory;
    }

    public int GetVariantChangeCooldownRemainingMinutes(string cardId)
    {
        if (!TryGetCard(cardId, out var c) || c == null)
            return 0;

        if (variantChangeCooldownMinutes <= 0)
            return 0;

        if (c.lastVariantChangeAtMin <= 0)
            return 0;

        long now = GetGameMinutesNow();
        long elapsed = now - c.lastVariantChangeAtMin;
        long remain = variantChangeCooldownMinutes - elapsed;

        return Mathf.Max(0, (int)remain);
    }
    public string GetVariantChangeCooldownFormatted(string cardId)
    {
        int remainMin = GetVariantChangeCooldownRemainingMinutes(cardId);
        if (remainMin <= 0)
            return "00:00:00";

        int totalSeconds = remainMin * 60;
        TimeSpan ts = TimeSpan.FromSeconds(totalSeconds);
        return $"{ts.Hours + ts.Days * 24:00}:{ts.Minutes:00}:{ts.Seconds:00}";
    }

    public bool TryChangeCardVariant(
    string cardId,
    int newVariant,
    out string reason)
    {
        reason = "";

        if (!TryGetCard(cardId, out var c) || c == null)
        {
            reason = "CARD NOT FOUND";
            return false;
        }

        if (c.status != BankCardStatus.Active)
        {
            reason = "CARD NOT ACTIVE";
            return false;
        }

        if (newVariant < 0 || newVariant >= VariantCount)
        {
            reason = "INVALID VARIANT";
            return false;
        }

        if (c.colorVariant == newVariant)
        {
            reason = "SAME VARIANT";
            return false;
        }

        int remain = GetVariantChangeCooldownRemainingMinutes(cardId);
        if (remain > 0)
        {
            reason = "COOLDOWN";
            return false;
        }

        if (!_accounts.TryGetValue(c.accountId, out var acc) || acc == null)
        {
            reason = "ACCOUNT NOT FOUND";
            return false;
        }

        if (acc.locked)
        {
            reason = "ACCOUNT LOCKED";
            return false;
        }

        if (variantChangePrice > 0 && acc.balance < variantChangePrice)
        {
            reason = "NOT ENOUGH MONEY";
            return false;
        }

        if (variantChangePrice > 0)
            acc.balance -= variantChangePrice;

        c.colorVariant = newVariant;
        c.lastVariantChangeAtMin = GetGameMinutesNow();

        MarkDirty();
        reason = "OK";
        return true;
    }

    public int CountClosableBlockingCardsForAccount(int accountId)
    {
        int count = 0;

        if (!_accounts.TryGetValue(accountId, out var acc) || acc == null || acc.issuedCardIds == null)
            return 0;

        for (int i = 0; i < acc.issuedCardIds.Count; i++)
        {
            string cardId = acc.issuedCardIds[i];
            if (string.IsNullOrWhiteSpace(cardId)) continue;

            if (!TryGetCardRecordEffective(cardId, out var rec) || rec == null)
                continue;

            if (rec.status == BankCardStatus.Active || rec.status == BankCardStatus.Pending)
                count++;
        }

        return count;
    }

    public CloseAccountCheckResult EvaluateCloseAccount(int accountId)
    {
        var result = new CloseAccountCheckResult
        {
            accountExists = false,
            hasCurrentLoans = false,
            balanceNotZero = false,
            hasActiveCards = false,
            allConditionsMet = false,
            balance = 0,
            activeLoanCount = 0,
            activeCardCount = 0
        };

        if (accountId <= 0)
            return result;

        if (!_accounts.TryGetValue(accountId, out var acc) || acc == null)
            return result;

        result.accountExists = true;
        result.balance = acc.balance;
        result.balanceNotZero = acc.balance != 0;

        var loanSystem = LoanSystem.Instance;
        if (loanSystem != null)
        {
            result.activeLoanCount = loanSystem.GetActiveLoanCountForAccount(accountId);
            result.hasCurrentLoans = result.activeLoanCount > 0;
        }

        // tu zmiana logiki:
        // activeCardCount = ile wymaganych kart ACTIVE/PENDING NIE ma w inventory gracza
        result.activeCardCount = CountMissingReturnableCardsForAccount(accountId);
        result.hasActiveCards = result.activeCardCount > 0;

        result.allConditionsMet =
            result.accountExists &&
            !result.hasCurrentLoans &&
            !result.balanceNotZero &&
            !result.hasActiveCards;

        return result;
    }

    public bool TryCloseAccount(int accountId, out string reason)
    {
        reason = "";

        if (accountId <= 0)
        {
            reason = "ACCOUNT NOT FOUND";
            return false;
        }

        if (!_accounts.TryGetValue(accountId, out var acc) || acc == null)
        {
            reason = "ACCOUNT NOT FOUND";
            return false;
        }

        // finalny check backendowy:
        // inventory NIE jest już tutaj sprawdzane, bo karty mogły zostać odebrane
        var loanSystem = LoanSystem.Instance;
        if (loanSystem != null && loanSystem.GetActiveLoanCountForAccount(accountId) > 0)
        {
            reason = "ACTIVE LOAN EXISTS";
            return false;
        }

        if (acc.balance != 0)
        {
            reason = "BALANCE NOT ZERO";
            return false;
        }

        // zapamiętaj wszystkie karty tego konta
        var cardIdsToRemove = new List<string>();
        if (acc.issuedCardIds != null)
        {
            for (int i = 0; i < acc.issuedCardIds.Count; i++)
            {
                string cardId = acc.issuedCardIds[i];
                if (!string.IsNullOrWhiteSpace(cardId))
                    cardIdsToRemove.Add(cardId);
            }
        }

        // usuń mapowanie citizen -> account
        if (!string.IsNullOrWhiteSpace(acc.citizenId))
            _citizenToAccount.Remove(acc.citizenId);

        // usuń karty z save playerCards
        if (_save != null && _save.playerCards != null)
        {
            for (int i = 0; i < _save.playerCards.Count; i++)
            {
                var p = _save.playerCards[i];
                if (p?.cardIds == null) continue;

                p.cardIds.RemoveAll(cardId =>
                    !string.IsNullOrWhiteSpace(cardId) &&
                    cardIdsToRemove.Contains(cardId));
            }
        }

        // usuń karty całkowicie z rejestru banku
        for (int i = 0; i < cardIdsToRemove.Count; i++)
            _cards.Remove(cardIdsToRemove[i]);

        // wyczyść konto
        acc.issuedCardIds?.Clear();

        // usuń konto
        _accounts.Remove(accountId);

        MarkDirty();
        reason = "OK";
        return true;
    }

    public int CountMissingReturnableCardsForAccount(int accountId)
    {
        int missing = 0;

        if (!_accounts.TryGetValue(accountId, out var acc) || acc == null || acc.issuedCardIds == null)
            return 0;

        for (int i = 0; i < acc.issuedCardIds.Count; i++)
        {
            string cardId = acc.issuedCardIds[i];
            if (string.IsNullOrWhiteSpace(cardId))
                continue;

            if (!TryGetCardRecordEffective(cardId, out var rec) || rec == null)
                continue;

            // tylko ACTIVE / PENDING trzeba fizycznie oddać do banku
            if (rec.status != BankCardStatus.Active && rec.status != BankCardStatus.Pending)
                continue;

            // jeśli gracz nie ma tej karty w inventory -> warunek niespełniony
            if (InventoryUI.Instance == null || !InventoryUI.Instance.HasBankCardId(rec.cardId))
                missing++;
        }

        return missing;
    }

    public List<BankCardRecord> GetReturnableCardsForAccount(int accountId)
    {
        var result = new List<BankCardRecord>();

        if (!_accounts.TryGetValue(accountId, out var acc) || acc == null || acc.issuedCardIds == null)
            return result;

        for (int i = 0; i < acc.issuedCardIds.Count; i++)
        {
            string cardId = acc.issuedCardIds[i];
            if (string.IsNullOrWhiteSpace(cardId))
                continue;

            if (!TryGetCardRecordEffective(cardId, out var rec) || rec == null)
                continue;

            if (rec.status == BankCardStatus.Active || rec.status == BankCardStatus.Pending)
                result.Add(rec);
        }

        return result;
    }
}