public enum BankCardStatus
{
    Active,
    Pending,
    Blocked,
    Revoked
}

[System.Serializable]
public struct BankCardMeta
{
    public string cardId;     // unikalne ID karty (np. GUID skrócony)
    public int accountId; // np. -1 = brak kontaz
    public int pin;           // 4 cyfry
    public BankCardStatus status;
    public int colorVariant;  // 0..N (Twoje kolory)
    public long activateAt;   // timestamp/“koniec doby” na później (Pending)
}

public class InventoryItemInstance
{
    public string id { get; private set; } = System.Guid.NewGuid().ToString();

    public InventoryItemData data;
    public int count = 1;

    public int currentAmmo;
    public int totalAmmo;

    public BankCardMeta bankCard;
    public bool hasBankCardMeta;

    public InventoryItemInstance(InventoryItemData data, int currentAmmo = -1, int totalAmmo = -1)
    {
        this.data = data;

        if (data is WeaponItemData wd)
        {
            this.currentAmmo = (currentAmmo >= 0) ? currentAmmo : wd.magazineSize;
            this.totalAmmo = (totalAmmo >= 0) ? totalAmmo : wd.magazineSize * 3;
        }
        else if (data is AmmoItemData ad)
        {
            // „ładunek” magazynka/paczki – preferuj przekazane wartości
            int payload = (totalAmmo >= 0) ? totalAmmo
                        : (currentAmmo >= 0) ? currentAmmo
                        : ad.amountPerUnit;

            if (payload < 0) payload = 0;
            this.currentAmmo = payload;
            this.totalAmmo = payload;
            if (this.count < 1) this.count = 1; // pojedynczy „mag” jako item
        }
        else
        {
            // inne typy – zachowaj to, co przyszło
            this.currentAmmo = currentAmmo;
            this.totalAmmo = totalAmmo;
        }
    }

    public InventoryItemInstance(BankCardItemData data, BankCardMeta meta)
    : this((InventoryItemData)data)
    {
        bankCard = meta;
        hasBankCardMeta = true;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param></param>
    /// <returns></returns>
    /// 

    public override bool Equals(object obj)
    {
        if (obj is not InventoryItemInstance other) return false;
        return id == other.id;
    }
    public override int GetHashCode() => id.GetHashCode();
}
