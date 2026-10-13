// ไฟล์: UIJoystick.cs — ติดกับฐานจอยสติ๊กบนจอในสนามรบ (สร้างโดย Editor/SceneSetupTool)
// PlayerController หา UIJoystick ในฉากแล้วอ่าน GetHorizontal/GetVertical ทุกเฟรมเพื่อขยับยาน (ทำงานเฉพาะเครื่องตัวเอง)
using UnityEngine;
using UnityEngine.EventSystems;

// รับการสัมผัส/ลากจอยสติ๊กบนหน้าจอ แล้วเปิดค่า inputVector ให้ PlayerController ใช้เคลื่อนยาน
// คลาสจอยสติ๊กเสมือน รับ event กด/ลาก/ปล่อย ติดตามนิ้วเดียว (activePointerId)
public class UIJoystick : MonoBehaviour, IDragHandler, IPointerUpHandler, IPointerDownHandler
{
    // อ้างอิงแบบ static ของจอยสติ๊กในฉาก
    public static UIJoystick Instance;
    
    // background = ฐานจอย, handle = ปุ่มกลางที่ขยับตามนิ้ว (ลูกชื่อ JoystickHandle)
    private RectTransform background;
    private RectTransform handle;
    // ค่าทิศ x,y ช่วง -1..1 (ยาวไม่เกิน 1)
    private Vector2 inputVector;
    // id ของนิ้วที่กำลังลากจอย (int.MinValue = ไม่มีนิ้ว)
    private int activePointerId = int.MinValue;
    public bool IsDragging => activePointerId != int.MinValue;
    // ระยะที่ handle ขยับได้สูงสุด เป็นสัดส่วนของขนาดฐาน (0.4 = 40%)
    public float handleTravelFraction = .4f;

    // Unity เรียกตอนสร้าง: ตั้ง Instance และหา RectTransform ของฐานกับ handle
    private void Awake()
    {
        Instance = this;
        background = GetComponent<RectTransform>();
        // The first child may be the MOVE label, not the joystick handle.
        handle = transform.Find("JoystickHandle") as RectTransform;
    }

    // ลากนิ้ว: แปลงตำแหน่งนิ้วเป็นพิกัดในฐาน ทำให้อยู่ในช่วง -1..1 (เกิน 1 จะ normalize) แล้วขยับ handle
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

    // นิ้วแตะจอย: ถ้ายังไม่มีนิ้วอื่น ให้จำนิ้วนี้แล้วคำนวณทิศทันที
    public virtual void OnPointerDown(PointerEventData ped)
    {
        if (activePointerId != int.MinValue) return;
        activePointerId = ped.pointerId;
        OnDrag(ped);
    }

    // นิ้วที่ลากอยู่ปล่อย: รีเซ็ต input เป็น 0 และคืน handle กลับกลาง
    public virtual void OnPointerUp(PointerEventData ped)
    {
        if (ped.pointerId != activePointerId) return;
        activePointerId = int.MinValue;
        inputVector = Vector2.zero;
        if (handle != null)
            handle.anchoredPosition = Vector2.zero;
    }

    // ตอนถูกปิด: รีเซ็ตนิ้ว/input/handle กันจอยค้าง
    private void OnDisable()
    {
        activePointerId = int.MinValue;
        inputVector = Vector2.zero;
        if (handle != null) handle.anchoredPosition = Vector2.zero;
    }

    // เมื่อแอปเสียโฟกัส: รีเซ็ตเหมือน OnDisable
    private void OnApplicationFocus(bool focused)
    {
        if (!focused) OnDisable();
    }

    // ค่าแกนนอน: ถ้ามีนิ้วลากใช้ค่าจอย ไม่งั้นใช้คีย์บอร์ด (Input Horizontal) สำหรับทดสอบ
    public float GetHorizontal()
    {
        if (activePointerId != int.MinValue) return inputVector.x;
        // Fallback for Keyboard testing in editor
        return Input.GetAxis("Horizontal");
    }

    // ค่าแกนตั้ง: ถ้ามีนิ้วลากใช้ค่าจอย ไม่งั้นใช้คีย์บอร์ด (Input Vertical)
    public float GetVertical()
    {
        if (activePointerId != int.MinValue) return inputVector.y;
        return Input.GetAxis("Vertical");
    }
}
