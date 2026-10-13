// ไฟล์: UIButton.cs — ติดกับปุ่มบนจอในสนามรบ (เช่น Fire, Skill) ลงทะเบียนตาม buttonName
// PlayerController เช็คการกดผ่าน UIButton.IsPressed("ชื่อปุ่ม") หรือ isPressed
// ปุ่มยิงที่ GameplayManager.HUD เปิด enableDragAim ลากเพื่อเล็งทิศได้ (PlayerController.Movement อ่าน AimDirection/HasAim)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// รับการกดและลากปุ่มยิง; หน้าตั้งค่าอยู่ใน BattleSettingsPanel.cs
// คลาสปุ่มสัมผัส รับ event กด/ปล่อย/ลากจาก EventSystem ติดตามนิ้วเดียว (activePointerId)
public class UIButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IDragHandler, IInitializePotentialDragHandler
{
    // ชื่อปุ่ม ใช้เป็น key ใน allButtons
    public string buttonName = "Fire";
    
    // Store all buttons by name
    private static Dictionary<string, UIButton> allButtons = new Dictionary<string, UIButton>();

    // true ระหว่างที่นิ้วกดค้างอยู่
    public bool isPressed = false;
    // เปิดโหมดลากเพื่อเล็ง (ใช้กับปุ่มยิง)
    public bool enableDragAim;
    // ทิศเล็ง (เวกเตอร์ยาว 1) และสถานะว่ามีทิศเล็งแล้วหรือยัง
    public Vector2 AimDirection { get; private set; }
    // true = ผู้เล่นลากนิ้วเล็งทิศแล้ว
    public bool HasAim { get; private set; }
    // จุด handle ที่ขยับตามนิ้ว และระยะขยับสูงสุดเป็นสัดส่วนของขนาดปุ่ม (0.4 = 40%)
    public RectTransform aimHandle;
    public float handleTravelFraction = .4f;
    // id ของนิ้วที่กดปุ่มอยู่ (int.MinValue = ไม่มีนิ้วกด)
    private int activePointerId = int.MinValue;

    // ปิดระยะ drag threshold ให้ OnDrag ทำงานทันทีที่นิ้วขยับ
    public void OnInitializePotentialDrag(PointerEventData eventData) { eventData.useDragThreshold = false; }

    // ลากนิ้วบนปุ่ม (เฉพาะโหมด enableDragAim และนิ้วเดียวกับที่กด): คำนวณทิศจากกลางปุ่มไปยังนิ้ว
    // ถ้าลากเกิน 18% ของรัศมีจึงนับเป็นทิศเล็ง แล้วขยับ aimHandle ตามนิ้วแต่ไม่เกินระยะที่กำหนด
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
        if (aimHandle != null) aimHandle.anchoredPosition = Vector2.ClampMagnitude(point,
            Mathf.Min(rect.rect.width, rect.rect.height) * handleTravelFraction);
    }

    // Unity เรียกตอนสร้าง: ลงทะเบียนปุ่มนี้ใน allButtons ตามชื่อ
    private void Awake()
    {
        allButtons[buttonName] = this;
    }

    // ตอนถูกทำลาย: ลบออกจาก allButtons (เฉพาะถ้ายังเป็นปุ่มนี้ที่ลงทะเบียนอยู่)
    private void OnDestroy()
    {
        if (allButtons.TryGetValue(buttonName, out UIButton registeredButton) && registeredButton == this)
            allButtons.Remove(buttonName);
    }

    // ตอนถูกปิด: รีเซ็ตสถานะกด/ทิศเล็ง และคืน handle กลับกลางปุ่ม
    private void OnDisable()
    {
        isPressed = false;
        activePointerId = int.MinValue;
        HasAim = false;
        if (aimHandle != null) aimHandle.anchoredPosition = Vector2.zero;
    }

    // เมื่อแอปเสียโฟกัส (เช่น สลับแอป): รีเซ็ตสถานะเหมือน OnDisable กันปุ่มค้าง
    private void OnApplicationFocus(bool focused)
    {
        if (!focused) OnDisable();
    }

    // นิ้วแตะปุ่ม: ถ้ายังไม่มีนิ้วอื่นกดอยู่ ให้จำนิ้วนี้ ตั้ง isPressed และคำนวณทิศเล็งทันที
    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointerId != int.MinValue) return;
        activePointerId = eventData.pointerId;
        isPressed = true;
        OnDrag(eventData);
    }

    // นิ้วที่กดอยู่ปล่อย: เลิกกด และคืน handle กลับกลางปุ่ม (ทิศเล็งเดิมยังเก็บไว้)
    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId) return;
        isPressed = false;
        activePointerId = int.MinValue;
        if (aimHandle != null) aimHandle.anchoredPosition = Vector2.zero;
    }

    // นิ้วเลื่อนออกนอกปุ่ม: ปุ่มธรรมดาจะเลิกกด แต่ปุ่มโหมดเล็งยังกดค้างต่อ
    public void OnPointerExit(PointerEventData eventData)
    {
        // Keep the firing stick captured when the aiming finger moves outside its artwork.
        if (enableDragAim || eventData.pointerId != activePointerId) return;
        isPressed = false;
        activePointerId = int.MinValue;
    }

    // Static helper to check button state
    // เช็คว่าปุ่มชื่อนี้ถูกกดอยู่หรือไม่ (ไม่พบปุ่มคืน false) PlayerController เรียกทุกเฟรม
    public static bool IsPressed(string name)
    {
        if (allButtons.TryGetValue(name, out UIButton btn))
        {
            return btn.isPressed;
        }
        return false;
    }
}
