using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public string buttonName = "Fire";
    
    // Store all buttons by name
    private static Dictionary<string, UIButton> allButtons = new Dictionary<string, UIButton>();

    public bool isPressed = false;

    private void Awake()
    {
        allButtons[buttonName] = this;
    }

    private void OnDestroy()
    {
        if (allButtons.TryGetValue(buttonName, out UIButton registeredButton) && registeredButton == this)
            allButtons.Remove(buttonName);
    }

    private void OnDisable()
    {
        isPressed = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPressed = false;
    }

    // Static helper to check button state
    public static bool IsPressed(string name)
    {
        if (allButtons.TryGetValue(name, out UIButton btn))
        {
            return btn.isPressed;
        }
        return false;
    }
}
