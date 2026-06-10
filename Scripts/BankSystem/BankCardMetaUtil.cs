using UnityEngine;

public static class BankCardMetaUtil
{
    public static BankCardMeta Generate(int colorVariant = 0)
    {
        return new BankCardMeta
        {
            cardId = null,          // ❌ NIE generujemy ID tutaj
            accountId = -1,
            pin = 0,
            status = BankCardStatus.Pending,
            colorVariant = colorVariant,
            activateAt = 0
        };
    }
}
