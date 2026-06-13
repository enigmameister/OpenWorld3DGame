using System;
using TMPro;
using UnityEngine;

public class BoxCashController
{
    private readonly Func<WorldBoxInventory> getCurrentBox;
    private readonly Func<PlayerStats> getPlayerStats;
    private readonly Func<ItemAmountDialog> getAmountDialog;
    private readonly Action refreshCapacityTexts;

    private readonly TextMeshProUGUI boxCashText;
    private readonly TextMeshProUGUI playerCashText;

    public BoxCashController(
        Func<WorldBoxInventory> getCurrentBox,
        Func<PlayerStats> getPlayerStats,
        Func<ItemAmountDialog> getAmountDialog,
        TextMeshProUGUI boxCashText,
        TextMeshProUGUI playerCashText,
        Action refreshCapacityTexts)
    {
        this.getCurrentBox = getCurrentBox;
        this.getPlayerStats = getPlayerStats;
        this.getAmountDialog = getAmountDialog;
        this.boxCashText = boxCashText;
        this.playerCashText = playerCashText;
        this.refreshCapacityTexts = refreshCapacityTexts;
    }

    public void RefreshCashTexts()
    {
        WorldBoxInventory box = getCurrentBox?.Invoke();

        if (boxCashText != null)
        {
            int boxCash = box != null ? box.cash : 0;
            boxCashText.text = $"Cash: {boxCash:n0}$";
        }

        if (playerCashText != null)
        {
            PlayerStats stats = getPlayerStats?.Invoke();
            int playerCash = stats != null ? stats.money : 0;
            playerCashText.text = $"Cash: {playerCash:n0}$";
        }
    }

    public void TryOpenCashTransferBoxToPlayer()
    {
        WorldBoxInventory box = getCurrentBox?.Invoke();

        if (box == null)
            return;

        if (box.cash <= 0)
            return;

        PlayerStats stats = getPlayerStats?.Invoke();

        if (stats == null)
            return;

        ItemAmountDialog dialog = getAmountDialog?.Invoke();

        if (dialog == null)
            return;

        int max = box.cash;

        dialog.Open(
            "TRANSFER CASH TO PLAYER",
            1,
            max,
            max,
            amount =>
            {
                if (box == null)
                    return;

                amount = Mathf.Clamp(amount, 1, box.cash);

                box.cash -= amount;

                if (InventoryUI.Instance != null)
                    InventoryUI.Instance.ApplyMoneyChange(amount);
                else
                    stats.SetMoney(stats.money + amount);

                RefreshCashTexts();
                refreshCapacityTexts?.Invoke();
            }
        );
    }

    public void TryOpenCashTransferPlayerToBox()
    {
        WorldBoxInventory box = getCurrentBox?.Invoke();

        if (box == null)
            return;

        PlayerStats stats = getPlayerStats?.Invoke();

        if (stats == null)
            return;

        if (stats.money <= 0)
            return;

        ItemAmountDialog dialog = getAmountDialog?.Invoke();

        if (dialog == null)
            return;

        int max = stats.money;

        dialog.Open(
            "TRANSFER CASH TO BOX",
            1,
            max,
            max,
            amount =>
            {
                amount = Mathf.Clamp(amount, 1, stats.money);

                box.cash += amount;

                if (InventoryUI.Instance != null)
                    InventoryUI.Instance.ApplyMoneyChange(-amount);
                else
                    stats.SetMoney(stats.money - amount);

                RefreshCashTexts();
                refreshCapacityTexts?.Invoke();
            }
        );
    }
}