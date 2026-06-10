using UnityEngine;
using TMPro;

public class CarUIManager : MonoBehaviour
{
    public TextMeshProUGUI carNameText;
    public Animator carNameAnimator;

    public void ShowCarName(string name)
    {
        carNameText.text = name;
        carNameAnimator.SetTrigger("Show");
    }
}
