using UnityEngine;

public class BankCardPickupMeta : MonoBehaviour, IItemPickupMeta
{
    [Header("Meta (dla kart na scenie)")]
    [SerializeField] private bool generateMetaOnAwake = true;
    [SerializeField] private BankCardMeta meta;

    [Header("Visual")]
    [SerializeField] private BankCardVisual visual;

    void Reset()
    {
        if (!visual) visual = GetComponent<BankCardVisual>();
    }

    void Awake()
    {
        if (!visual) visual = GetComponent<BankCardVisual>();

        if (generateMetaOnAwake && string.IsNullOrWhiteSpace(meta.cardId))
            meta = BankCardMetaUtil.Generate(meta.colorVariant);

        visual?.Apply(meta);
    }

    public void WriteToInstance(InventoryItemInstance inst)
    {
        if (inst == null) return;

        // jeœli ju¿ ma meta i ma cardId -> nie nadpisuj
        if (inst.hasBankCardMeta && !string.IsNullOrWhiteSpace(inst.bankCard.cardId))
            return;

        inst.hasBankCardMeta = true;
        inst.bankCard = Copy(meta);
    }

    public void ReadFromInstance(InventoryItemInstance inst)
    {
        if (inst == null) return;
        if (!inst.hasBankCardMeta) return;
        if (string.IsNullOrWhiteSpace(inst.bankCard.cardId)) return;

        meta = Copy(inst.bankCard);
        visual?.Apply(meta);
    }

    private static BankCardMeta Copy(BankCardMeta src)
    {
        return new BankCardMeta
        {
            cardId = src.cardId,
            accountId = src.accountId,
            pin = src.pin,
            status = src.status,
            colorVariant = src.colorVariant,
            activateAt = src.activateAt
        };
    }

    /// <summary>
    /// Wywo³uj TYLKO gdy karta jest "u¿ywana" (ATM / NPC).
    /// Zaci¹ga status z banku i aktualizuje meta+visual.
    /// </summary>
    public bool TrySyncStatusFromBank()
    {
        if (string.IsNullOrWhiteSpace(meta.cardId))
            return false;

        var bank = BankSystem.Instance;
        if (bank == null) return false;

        if (!bank.TryGetCardRecordEffective(meta.cardId, out var rec))
            return false;

        bool changed = false;

        if (meta.status != rec.status) { meta.status = rec.status; changed = true; }
        if (meta.activateAt != rec.activateAt) { meta.activateAt = rec.activateAt; changed = true; }

        if (changed && visual != null)
        {
            // albo Apply(meta) jeœli chcesz full refresh:
            visual.Apply(meta);
            // lub minimalnie:
            // visual.SetStatus(meta.status);
        }

        return changed;
    }
}
