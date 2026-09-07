using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IDragHandler, IInitializePotentialDragHandler
{
    public string buttonName = "Fire";
    
    // Store all buttons by name
    private static Dictionary<string, UIButton> allButtons = new Dictionary<string, UIButton>();

    public bool isPressed = false;
    public bool enableDragAim;
    public Vector2 AimDirection { get; private set; }
    public bool HasAim { get; private set; }
    public RectTransform aimHandle;
    private int activePointerId = int.MinValue;

    public void OnInitializePotentialDrag(PointerEventData eventData) { eventData.useDragThreshold = false; }

    public void OnDrag(PointerEventData eventData)
    {
        if (!enableDragAim || eventData.pointerId != activePointerId || !isPressed) return;
        var rect = transform as RectTransform;
        if (rect == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out Vector2 point)) return;
        point -= rect.rect.center;
        float radius = Mathf.Max(1f, Mathf.Min(rect.rect.width, rect.rect.height) * .4f);
        if (point.magnitude > radius * .18f)
        {
            AimDirection = point.normalized;
            HasAim = true;
        }
        if (aimHandle != null) aimHandle.anchoredPosition = Vector2.ClampMagnitude(point, radius);
    }

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
        activePointerId = int.MinValue;
        HasAim = false;
        if (aimHandle != null) aimHandle.anchoredPosition = Vector2.zero;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) OnDisable();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointerId != int.MinValue) return;
        activePointerId = eventData.pointerId;
        isPressed = true;
        OnDrag(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId) return;
        isPressed = false;
        activePointerId = int.MinValue;
        if (aimHandle != null) aimHandle.anchoredPosition = Vector2.zero;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Keep the firing stick captured when the aiming finger moves outside its artwork.
        if (enableDragAim || eventData.pointerId != activePointerId) return;
        isPressed = false;
        activePointerId = int.MinValue;
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
