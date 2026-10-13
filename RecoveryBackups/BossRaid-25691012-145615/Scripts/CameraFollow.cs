// ไฟล์: CameraFollow.cs — ติดกับกล้องหลักใน Scene สนามรบ ให้กล้องตามยานของผู้เล่นเครื่องนี้ (GameplayManager.Instance.localPlayer)
// ใช้ขอบแม็พจาก GameplayManager.GetArenaMin/Max ตามแม็พที่เล่น และบวกการสั่นจาก CameraShake
// PlayerController.Warp เรียก SnapToTarget หลังวาร์ป; ทำงานเฉพาะเครื่องตัวเอง ไม่เกี่ยวกับเครือข่าย
using UnityEngine;

// ติดกับกล้องหลักเพื่อไล่ตามเป้าหมาย และจำกัดภาพไม่ให้ออกนอกขอบสนาม
// คลาสกล้องติดตามยานแบบนุ่มนวล (Lerp) ทำงานใน LateUpdate หลังยานขยับเสร็จในเฟรมนั้น
public class CameraFollow : MonoBehaviour
{
    // ยานที่กล้องตาม (ถ้าว่าง จะหา localPlayer เองใน LateUpdate)
    public Transform target;
    // ความเร็วไล่ตาม: ยิ่งมากกล้องยิ่งตามทัน (ใช้คูณ Time.deltaTime เป็นสัดส่วนของ Lerp)
    public float smoothSpeed = 5f;
    // ระยะกล้องจากยาน (z = -10 ให้กล้องอยู่หน้าฉาก 2D)
    public Vector3 offset = new Vector3(0, 0, -10f);
    // คอมโพเนนต์ Camera ของวัตถุนี้ (ใช้ขนาดภาพในการ clamp)
    public Camera cam;

    // Camera Bounds — กล้องจะไม่หลุดออกนอกขอบแม็พ
    public Vector2 arenaMin = new Vector2(-38f, -35.5f);
    public Vector2 arenaMax = new Vector2(38f, 35.5f);

    // Unity เรียกครั้งแรก: อ่านขอบแม็พตาม index แม็พปัจจุบันจาก GameplayManager และตั้ง orthographicSize = 10
    void Start()
    {
        int mapIndex = GameplayManager.GetCurrentMapIndex();
        arenaMin = GameplayManager.GetArenaMin(mapIndex);
        arenaMax = GameplayManager.GetArenaMax(mapIndex);
        cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.orthographicSize = 10f; // ซูมออกให้เห็นกว้างขึ้น
        }
    }

    // ทุกเฟรม (หลัง Update): ถ้ายังไม่มีเป้าหมายให้หา localPlayer ก่อน ถ้ามีแล้วเลื่อนกล้องเข้าหาแบบ Lerp
    // แล้วบวกค่าสั่นจาก CameraShake และ clamp ไม่ให้ภาพเกินขอบแม็พ
    void LateUpdate()
    {
        if (target == null)
        {
            // พยายามหาเป้าหมาย
            if (GameplayManager.Instance != null && GameplayManager.Instance.localPlayer != null)
            {
                target = GameplayManager.Instance.localPlayer.transform;
            }
            return;
        }

        Vector3 desiredPosition = target.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        
        if (CameraShake.Instance != null)
        {
            smoothedPosition += CameraShake.Instance.currentShakeOffset;
        }

        transform.position = ClampToArena(smoothedPosition);
    }

    // Clamp Camera Position เพื่อไม่ให้กล้องโชว์นอกแม็พ
    private Vector3 ClampToArena(Vector3 position)
    {
        if (cam != null)
        {
            // ครึ่งความสูง/ครึ่งความกว้างของภาพ (หน่วย world) ใช้หดขอบ เพื่อให้ขอบภาพไม่เกินขอบแม็พ
            float camHeight = cam.orthographicSize;
            float camWidth = camHeight * cam.aspect;
            position.x = Mathf.Clamp(position.x, arenaMin.x + camWidth, arenaMax.x - camWidth);
            position.y = Mathf.Clamp(position.y, arenaMin.y + camHeight, arenaMax.y - camHeight);
        }
        return position;
    }

    // กระโดดไปที่ยานทันที (ใช้หลังวาร์ป ไม่ให้กล้องเลื่อนข้ามแม็พ)
    public void SnapToTarget()
    {
        if (target == null) return;
        transform.position = ClampToArena(target.position + offset);
    }
}
