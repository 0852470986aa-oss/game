using UnityEngine;
using UnityEngine.EventSystems;

public class UIJoystick : MonoBehaviour, IDragHandler, IPointerUpHandler, IPointerDownHandler
{
    public static UIJoystick Instance;
    
    private RectTransform background;
    private RectTransform handle;
    private Vector2 inputVector;

    private void Awake()
    {
        Instance = this;
        background = GetComponent<RectTransform>();
        if (transform.childCount > 0)
        {
            handle = transform.GetChild(0).GetComponent<RectTransform>();
        }
    }

    public virtual void OnDrag(PointerEventData ped)
    {
        if (handle == null) return;
        
        Vector2 pos;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(background, ped.position, ped.pressEventCamera, out pos))
        {
            // Normalize pos relative to background size
            pos.x = (pos.x / background.sizeDelta.x) * 2;
            pos.y = (pos.y / background.sizeDelta.y) * 2;

            inputVector = new Vector2(pos.x, pos.y);
            inputVector = (inputVector.magnitude > 1.0f) ? inputVector.normalized : inputVector;

            // Move the handle
            handle.anchoredPosition = new Vector2(
                inputVector.x * (background.sizeDelta.x / 2.5f),
                inputVector.y * (background.sizeDelta.y / 2.5f));
        }
    }

    public virtual void OnPointerDown(PointerEventData ped)
    {
        OnDrag(ped);
    }

    public virtual void OnPointerUp(PointerEventData ped)
    {
        inputVector = Vector2.zero;
        if (handle != null)
            handle.anchoredPosition = Vector2.zero;
    }

    private void OnDisable()
    {
        inputVector = Vector2.zero;
        if (handle != null) handle.anchoredPosition = Vector2.zero;
    }

    public float GetHorizontal()
    {
        if (inputVector.x != 0) return inputVector.x;
        // Fallback for Keyboard testing in editor
        return Input.GetAxis("Horizontal");
    }

    public float GetVertical()
    {
        if (inputVector.y != 0) return inputVector.y;
        return Input.GetAxis("Vertical");
    }
}
