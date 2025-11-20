using UnityEngine;

public class NavigationButton : MonoBehaviour
{
    public enum NavButtonType { Ahead, Back, Port, Starboard, Surface, Dive }
    [SerializeField] public NavButtonType buttonType;
    public SubmarineController submarineController;

    public void OnMouseDown()
    {
        // Debug.Log("Navigation button pressed: " + buttonType.ToString());
        if (submarineController != null)
        {
            submarineController.OnNavigationButtonPressed(buttonType);
        }
    }
}