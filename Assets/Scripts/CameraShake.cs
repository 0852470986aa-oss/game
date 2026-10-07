// ไฟล์: CameraShake.cs — สั่นกล้องในสนามรบ เรียกจาก PlayerController (เช่น ส่วน Health) และ HazardController (เช่น อุกกาบาต ฟ้าผ่า)
// ไม่ขยับกล้องเอง แค่คำนวณ currentShakeOffset ให้ CameraFollow นำไปบวกตำแหน่งกล้อง
using UnityEngine;
using System.Collections;

// เอฟเฟกต์สั่นกล้องชั่วคราว เรียกผ่าน CameraShake.Instance ตอนเกิดเหตุการณ์สำคัญ
// Singleton (Instance) อยู่ในฉากสนามรบ ทำงานเฉพาะเครื่องตัวเอง
public class CameraShake : MonoBehaviour
{
    // ตัวอ้างอิงแบบ static ให้สคริปต์อื่นเรียก TriggerShake ได้ทันที
    public static CameraShake Instance;
    // ค่าสั่นของเฟรมปัจจุบัน (CameraFollow อ่านไปบวกตำแหน่งกล้อง) เป็น 0 เมื่อไม่สั่น
    public Vector3 currentShakeOffset = Vector3.zero;
    // สถานะกำลังสั่น และ coroutine ที่กำลังทำงาน (ใช้หยุดอันเก่าเมื่อสั่งสั่นใหม่)
    private bool isShaking = false;
    private Coroutine shakeRoutine;

    // Unity เรียกตอนสร้างวัตถุ: ตั้งตัวเองเป็น Instance
    void Awake()
    {
        Instance = this;
    }

    // สั่งสั่นกล้อง: duration = ระยะเวลา (วินาที, ค่าเริ่ม 0.5), magnitude = ระยะสั่นสูงสุด (หน่วย world, ค่าเริ่ม 0.3)
    // ถ้ากำลังสั่นอยู่จะหยุดอันเก่าแล้วเริ่มใหม่ (ไม่ซ้อนกัน)
    public void TriggerShake(float duration = 0.5f, float magnitude = 0.3f)
    {
        // เฟส 9: ผู้เล่นปรับระดับกล้องสั่นได้ (ปิด / เบา / เต็ม) ในหน้า MORE OPTIONS
        magnitude *= GameSettings.ShakeScale;
        if (magnitude <= 0f) return;
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(Shake(duration, magnitude));
    }

    // Coroutine: ทุกเฟรมสุ่ม offset x,y ในช่วง ±magnitude จนครบ duration แล้วรีเซ็ต offset เป็น 0
    private IEnumerator Shake(float duration, float magnitude)
    {
        isShaking = true;
        float elapsed = 0.0f;

        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            currentShakeOffset = new Vector3(x, y, 0);
            elapsed += Time.deltaTime;
            yield return null;
        }

        currentShakeOffset = Vector3.zero;
        isShaking = false;
        shakeRoutine = null;
    }
}
