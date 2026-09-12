using UnityEngine;
using UnityEngine.EventSystems;

public class UIJoystick : MonoBehaviour, IDragHandler, IPointerUpHandler, IPointerDownHandler
{
    public static UIJoystick Instance;
    
    private RectTransform background;
    private RectTransform handle;
    private Vector2 inputVector;
    private int activePointerId = int.MinValue;
    public bool IsDragging => activePointerId != int.MinValue;
    public float handleTravelFraction = .4f;

    private void Awake()
    {
        Instance = this;
        background = GetComponent<RectTransform>();
        // The first child may be the MOVE label, not the joystick handle.
        handle = transform.Find("JoystickHandle") as RectTransform;
    }

    public virtual void OnDrag(PointerEventData ped)
    {
        if (ped.pointerId != activePointerId) return;
        
        Vector2 pos;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(background, ped.position, ped.pressEventCamera, out pos))
        {
            // Normalize pos relative to background size
            pos.x = (pos.x / background.sizeDelta.x) * 2;
            pos.y = (pos.y / background.sizeDelta.y) * 2;

            inputVector = new Vector2(pos.x, pos.y);
            inputVector = (inputVector.magnitude > 1.0f) ? inputVector.normalized : inputVector;

            // Move the handle
            if (handle != null) handle.anchoredPosition = new Vector2(
                inputVector.x * background.sizeDelta.x * handleTravelFraction,
                inputVector.y * background.sizeDelta.y * handleTravelFraction);
        }
    }

    public virtual void OnPointerDown(PointerEventData ped)
    {
        if (activePointerId != int.MinValue) return;
        activePointerId = ped.pointerId;
        OnDrag(ped);
    }

    public virtual void OnPointerUp(PointerEventData ped)
    {
        if (ped.pointerId != activePointerId) return;
        activePointerId = int.MinValue;
        inputVector = Vector2.zero;
        if (handle != null)
            handle.anchoredPosition = Vector2.zero;
    }

    private void OnDisable()
    {
        activePointerId = int.MinValue;
        inputVector = Vector2.zero;
        if (handle != null) handle.anchoredPosition = Vector2.zero;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) OnDisable();
    }

    public float GetHorizontal()
    {
        if (activePointerId != int.MinValue) return inputVector.x;
        // Fallback for Keyboard testing in editor
        return Input.GetAxis("Horizontal");
    }

    public float GetVertical()
    {
        if (activePointerId != int.MinValue) return inputVector.y;
        return Input.GetAxis("Vertical");
    }
}
