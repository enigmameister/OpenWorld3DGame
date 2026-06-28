using System.Collections;
using TMPro;
using UnityEngine;

public class CommunicateUI : MonoBehaviour
{
    public static CommunicateUI Instance { get; private set; }

    [Header("Refs")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text messageText;

    private Coroutine hideRoutine;

    private void Awake()
    {
        Instance = this;

        if (root == null)
            root = gameObject;

        HideImmediate();
    }

    public void Show(string message, float duration = 5f)
    {
        if (root != null)
            root.SetActive(true);

        if (messageText != null)
            messageText.text = message;

        if (hideRoutine != null)
            StopCoroutine(hideRoutine);

        hideRoutine = StartCoroutine(CoHideAfter(duration));
    }

    public void HideImmediate()
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        if (root != null)
            root.SetActive(false);
    }

    private IEnumerator CoHideAfter(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (root != null)
            root.SetActive(false);

        hideRoutine = null;
    }
}