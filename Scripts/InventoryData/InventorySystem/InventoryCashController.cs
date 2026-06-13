using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class InventoryCashController
{
    private readonly MonoBehaviour owner;
    private readonly Func<PlayerStats> getPlayerStats;
    private readonly TextMeshProUGUI moneyText;
    private readonly TextMeshProUGUI cashText;

    private Coroutine moneyAnimCo;
    private int uiCashShown = -1;

    public InventoryCashController(
        MonoBehaviour owner,
        Func<PlayerStats> getPlayerStats,
        TextMeshProUGUI moneyText,
        TextMeshProUGUI cashText)
    {
        this.owner = owner;
        this.getPlayerStats = getPlayerStats;
        this.moneyText = moneyText;
        this.cashText = cashText;
    }

    public void Init()
    {
        PlayerStats stats = GetStats();

        if (stats != null && moneyText != null)
        {
            stats.moneyText = moneyText;
            stats.UpdateMoneyUI();
        }

        uiCashShown = stats != null ? stats.money : -1;

        StopAnimation();
        RefreshCashUI();
    }

    public void RefreshCashUI()
    {
        PlayerStats stats = GetStats();

        if (stats == null || cashText == null)
            return;

        int value = uiCashShown >= 0 ? uiCashShown : stats.money;
        cashText.text = $"Cash: {value:n0}$";
    }

    public void ApplyMoneyChange(int delta)
    {
        PlayerStats stats = GetStats();

        if (stats == null)
            return;

        int before = stats.money;
        int after = Mathf.Max(0, before + delta);

        stats.money = after;
        stats.UpdateMoneyUI();

        int fromShown = uiCashShown >= 0 ? uiCashShown : before;
        float duration = GetMoneyAnimDuration(after - fromShown);

        StopAnimation();

        if (owner != null && owner.isActiveAndEnabled)
            moneyAnimCo = owner.StartCoroutine(CoAnimateCashUI(fromShown, after, duration));
        else
        {
            uiCashShown = after;

            if (cashText != null)
                cashText.text = $"Cash: {after:n0}$";
        }
    }

    public void StopAnimation()
    {
        if (moneyAnimCo != null && owner != null)
        {
            owner.StopCoroutine(moneyAnimCo);
            moneyAnimCo = null;
        }
    }

    private PlayerStats GetStats()
    {
        return getPlayerStats != null ? getPlayerStats.Invoke() : null;
    }

    private float GetMoneyAnimDuration(int delta)
    {
        delta = Mathf.Abs(delta);

        float t = Mathf.Clamp01(Mathf.Log10(delta + 1f) / 4f);

        float maxDuration = 0.9f;
        float minDuration = 0.18f;

        return Mathf.Lerp(maxDuration, minDuration, t);
    }

    private IEnumerator CoAnimateCashUI(int from, int to, float duration)
    {
        if (cashText == null)
            yield break;

        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;

            float a = duration <= 0f
                ? 1f
                : Mathf.Clamp01(t / duration);

            int value = Mathf.RoundToInt(Mathf.Lerp(from, to, a));

            uiCashShown = value;
            cashText.text = $"Cash: {value:n0}$";

            yield return null;
        }

        uiCashShown = to;
        cashText.text = $"Cash: {to:n0}$";
    }
}