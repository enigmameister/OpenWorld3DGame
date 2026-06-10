using UnityEngine;

public interface IAccountSubPanel
{
    CanvasGroup Group { get; }
    void Open(int accountId);
    void Close();
    void Tick(); // opcjonalnie: hold klawiszy/animacje
}
