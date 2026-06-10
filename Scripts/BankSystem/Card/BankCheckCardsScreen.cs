using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BankCheckCardsScreen : MonoBehaviour
{
    [SerializeField] private GameObject checkingRoot;     // "CheckingConnectedCards"
    [SerializeField] private GameObject notFoundRoot;     // "NotFoundCards"
    [SerializeField] private TMP_Text checkingText;       // "CHECKING CARDS..."
    [SerializeField] private float delaySeconds = 5f;

    [Header("Loading UI")]
    [SerializeField] private GameObject loadingRoot; // obiekt "Loading" albo jego parent
    [SerializeField] private TMP_Text loadingText;   // TMP na "LOADING..."

    private Coroutine _dotsCo;
    private Coroutine _co;

    public void BeginCheck(int accountId, Action onNoCards, Action<List<BankCardRecord>> onHasCards)
    {
        ResetUiAndCoroutines(); // <-- KLUCZ

        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoCheck(accountId, onNoCards, onHasCards));
    }


    private void ResetUiAndCoroutines()
    {
        if (_co != null) { StopCoroutine(_co); _co = null; }
        if (_dotsCo != null) { StopCoroutine(_dotsCo); _dotsCo = null; }

        // wy³¹cz wszystko i w³¹czymy to co trzeba w CoCheck/ShowLoading
        if (loadingRoot) loadingRoot.SetActive(false);
        if (checkingRoot) checkingRoot.SetActive(false);
        if (notFoundRoot) notFoundRoot.SetActive(false);
    }
    public void ShowNotFound()
    {
        if (checkingRoot) checkingRoot.SetActive(false);
        if (notFoundRoot) notFoundRoot.SetActive(true);
    }

    private IEnumerator CoCheck(int accountId, Action onNoCards, Action<List<BankCardRecord>> onHasCards)
    {
        if (_dotsCo != null) { StopCoroutine(_dotsCo); _dotsCo = null; }
        if (loadingRoot) loadingRoot.SetActive(false);

        if (notFoundRoot) notFoundRoot.SetActive(false);
        if (checkingRoot) checkingRoot.SetActive(true);
        if (checkingText) checkingText.text = "CHECKING CARDS...";

        yield return new WaitForSecondsRealtime(delaySeconds);

        var bank = BankSystem.Instance;
        var list = new List<BankCardRecord>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (bank != null && bank.TryGetAccount(accountId, out var acc))
        {
            foreach (var cardId in acc.issuedCardIds)
            {
                if (string.IsNullOrWhiteSpace(cardId)) continue;
                if (!seen.Add(cardId)) continue;

                if (!bank.TryGetCardRecordEffective(cardId, out var rec)) continue;
                if (rec.status == BankCardStatus.Revoked) continue;

                list.Add(rec);
            }
        }

        if (list.Count == 0) onNoCards?.Invoke();
        else onHasCards?.Invoke(list);
    }

    public void ShowLoading(string baseWord, float seconds)
    {
        // ukryj inne rzeczy
        if (checkingRoot) checkingRoot.SetActive(false);
        if (notFoundRoot) notFoundRoot.SetActive(false);

        if (loadingRoot) loadingRoot.SetActive(true);

        if (_dotsCo != null) StopCoroutine(_dotsCo);
        _dotsCo = StartCoroutine(CoDots(baseWord));
    }

    private IEnumerator CoDots(string baseWord)
    {
        int dots = 0;
        while (true)
        {
            dots = (dots + 1) % 4; // 0..3
            string d = new string('.', dots);
            if (loadingText) loadingText.text = $"{baseWord}{d}";
            yield return new WaitForSecondsRealtime(0.35f);
        }
    }

    private void OnDisable()
    {
        ResetUiAndCoroutines();
    }

}
