using UnityEngine;
using UnityEngine.UI;

public class SelectedLoanPanel : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private CanvasGroup root;

    [Header("Buttons")]
    [SerializeField] private Button repayBtn;
    [SerializeField] private Button restructureBtn;
    [SerializeField] private Button deferBtn;
    [SerializeField] private Button changeDueDateBtn;
    [SerializeField] private Button backBtn;

    private LoanMenuNavigation _controller;
    private ActiveLoan _loan;

    private Button[] _buttons;
    private int _index;

    private void Awake()
    {
        _buttons = new[]
        {
            repayBtn,
            restructureBtn,
            deferBtn,
            changeDueDateBtn,
            backBtn
        };
    }

    private void Update()
    {
        if (!IsOpen()) return;

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            _index = Mathf.Max(0, _index - 1);
            RefreshSelection();
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            _index = Mathf.Min(_buttons.Length - 1, _index + 1);
            RefreshSelection();
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            Activate();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Back();
        }
    }

    public void Open(LoanMenuNavigation controller, ActiveLoan loan)
    {
        _controller = controller;
        _loan = loan;
        _index = 0;

        Show(true);
        RefreshUI();
    }

    public void Close()
    {
        Show(false);
    }

    public void Hide()
    {
        Show(false);
    }

    public void RefreshView(ActiveLoan loan)
    {
        _loan = loan;
        RefreshUI();
    }

    private void RefreshUI()
    {
        RefreshSelection();
    }

    private void Activate()
    {
        switch (_index)
        {
            case 0:
                Close();
                _controller?.OpenRepayLoan();
                break;

            case 1:
                Close();
                _controller?.OpenRestructureLoan();
                break;

            case 2:
                Close();
                _controller?.OpenDeferLoan();
                break;

            case 3:
                Close();
                _controller?.OpenChangeDueDate();
                break;

            case 4:
                Back();
                break;
        }
    }

    private void Back()
    {
        _controller?.ConsumeEscapeThisFrame();
        Close();
        _controller?.BackToLoanMenu();
    }

    private void RefreshSelection()
    {
        if (_buttons == null || _buttons.Length == 0) return;

        _index = Mathf.Clamp(_index, 0, _buttons.Length - 1);

        if (_buttons[_index] != null)
            _buttons[_index].Select();
    }

    private void Show(bool v)
    {
        if (!root)
        {
            gameObject.SetActive(v);
            return;
        }

        root.alpha = v ? 1f : 0f;
        root.interactable = v;
        root.blocksRaycasts = v;
        root.gameObject.SetActive(v);
    }

    private bool IsOpen()
    {
        if (!root) return gameObject.activeSelf;
        return root.alpha > 0.01f;
    }

    public void OnClickRepay()
    {
        _index = 0;
        Activate();
    }

    public void OnClickRestructure()
    {
        _index = 1;
        Activate();
    }

    public void OnClickDefer()
    {
        _index = 2;
        Activate();
    }

    public void OnClickChangeDueDate()
    {
        _index = 3;
        Activate();
    }

    public void OnClickBack()
    {
        _index = 4;
        Activate();
    }
}