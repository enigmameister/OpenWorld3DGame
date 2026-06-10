using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CreditCardOperationsUI : MonoBehaviour
{
    public enum Step { Check, NotFound, Select, Ops }

    [Header("Roots")]
    [SerializeField] private CanvasGroup root;
    [SerializeField] private CanvasGroup checkRoot;      // Check_Screen_Root[1]
    [SerializeField] private CanvasGroup selectRoot;     // Select_Card_Root[2]
    [SerializeField] private CanvasGroup opsRoot;        // Root[3]

    [Header("Screens")]
    [SerializeField] private BankCheckCardsScreen checkScreen;     // logika delay + load
    [SerializeField] private BankSelectCardScreen selectScreen;    // lista + wybór
    [SerializeField] private BankCardOpsPanel opsPanel;            // menu + info + subpanelee

    private int _accountId;
    private Step _step;
    private Coroutine _loadingCo;


    private AccountOperationsUI _accountOpsOwner;

    public void OpenForAccount(int accountId)
    {
        _accountOpsOwner = null; // <— dodaj
        _accountId = accountId;
        Show(true);
        GoToCheck();
    }

    public void OpenFromAccountOperations(AccountOperationsUI owner, int accountId)
    {
        _accountOpsOwner = owner;
        _accountId = accountId;
        Show(true);
        GoToCheck();
    }

    public void Close() => Close(reopenAccountOpsOwner: true, returnToDialogueStart: true);

    public void ReturnToDialogueStart()
    {
        // zamknij bez powrotu do AccountOperationsUI
        Close(reopenAccountOpsOwner: false, returnToDialogueStart: true);
    }
    private void Close(bool reopenAccountOpsOwner, bool returnToDialogueStart)
    {
        StopLoadingCo();
        Show(false);

        if (reopenAccountOpsOwner && _accountOpsOwner != null)
        {
            var tmp = _accountOpsOwner;
            _accountOpsOwner = null;
            tmp.ReopenFromCardOperations();
            return;
        }

        if (!returnToDialogueStart) return;

        var dlg = FindFirstObjectByType<BankDialogueUI>();
        if (dlg != null)
            dlg.ReturnToStartSameSession();
    }


    private void Update()
    {
        if (!root || root.alpha < 0.5f) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // jeśli jesteśmy w OPS i subpanel jest otwarty -> NIE obsługuj ESC tutaj
            if (_step == Step.Ops && opsPanel != null && opsPanel.IsSubPanelOpen)
                return;

            if (_step == Step.Ops)
            {
                // wróć do SELECT, ale NATYCHMIAST
                GoToSelectImmediate();
            }
            else
            {
                Close();
            }

        }
    }

    private void GoToCheck()
    {
        StopLoadingCo();
        _step = Step.Check;
        ShowOnly(checkRoot);
        checkScreen.BeginCheck(_accountId, onNoCards: GoToNotFound, onHasCards: (cards) =>
        {
            GoToSelect(cards);
        });
    }

    private void GoToNotFound()
    {
        _step = Step.NotFound;
        ShowOnly(checkRoot); // albo osobny NotFound root — jak masz w hierarchii
        checkScreen.ShowNotFound();
    }

    private void GoToSelect(List<BankCardRecord> cards = null)
    {
        _step = Step.Select;
        ShowOnly(selectRoot);

        // jeśli cards == null → SelectScreen sam pobierze z BankSystem
        selectScreen.Open(_accountId, cards, onSelected: (selectedRecord) =>
        {
            StopLoadingCo();
            _loadingCo = StartCoroutine(CoLoadingThenOpenOps(selectedRecord));
        });

    }

    private IEnumerator CoLoadingThenOpenOps(BankCardRecord rec)
    {
        // pokaż Check screen i tryb "LOADING..."
        _step = Step.Check;
        ShowOnly(checkRoot);

        if (checkScreen != null)
            checkScreen.ShowLoading("LOADING", 3f); // animacja kropek

        yield return new WaitForSecondsRealtime(3f);

        GoToOps(rec);
    }

    private void GoToOps(BankCardRecord rec)
    {
        _step = Step.Ops;
        ShowOnly(opsRoot);

        // ⬇⬇ przekazujemy owner, żeby panel mógł zamykać / przełączać kroki
        opsPanel.Open(this, _accountId, rec);
    }

    private void ShowOnly(CanvasGroup cg)
    {
        SetCG(checkRoot, cg == checkRoot);
        SetCG(selectRoot, cg == selectRoot);
        SetCG(opsRoot, cg == opsRoot);
    }

    private static void SetCG(CanvasGroup cg, bool v)
    {
        if (!cg) return;
        cg.alpha = v ? 1 : 0;
        cg.interactable = v;
        cg.blocksRaycasts = v;
        cg.gameObject.SetActive(v);
    }

    private void Show(bool v)
    {
        if (!root) { gameObject.SetActive(v); return; }
        root.alpha = v ? 1 : 0;
        root.interactable = v;
        root.blocksRaycasts = v;
        root.gameObject.SetActive(v);
    }
    public void OnAccountOperationsButton()
    {
        Close();
    }

    private void GoToSelectImmediate()
    {
        StopLoadingCo();

        _step = Step.Select;
        ShowOnly(selectRoot);

        var bank = BankSystem.Instance;
        var cards = (bank != null)
            ? bank.GetCardsForAccount(_accountId, includeDeleted: false)
            : new List<BankCardRecord>();

        selectScreen.Open(_accountId, cards, selected =>
        {
            StartCoroutine(CoLoadingThenOpenOps(selected));
        });
    }

    private void StopLoadingCo()
    {
        if (_loadingCo != null)
        {
            StopCoroutine(_loadingCo);
            _loadingCo = null;
        }
    }

    public void ReturnToSelectCardRoot()
    {
        GoToCheck();
    }
}
