public interface IHoldInteractable
{
    string HoldLabel { get; }

    void HoldStarted();
    void HoldTick(float deltaTime);
    void HoldEnded();

    bool CanHoldInteract { get; }
}
