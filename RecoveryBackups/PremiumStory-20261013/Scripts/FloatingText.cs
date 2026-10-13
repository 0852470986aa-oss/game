// ไฟล์: FloatingText.cs — ติดกับ prefab ข้อความลอย (TextMeshPro แบบ world space) ที่ Editor/SceneSetupTool สร้าง
// PlayerController.Health สร้างข้อความนี้และเรียก Setup/Emphasize เพื่อแสดงตัวเลขดาเมจหรือข้อความเหนือยาน
using UnityEngine;
using TMPro;

// ข้อความลอย เช่น ตัวเลขความเสียหาย: เคลื่อนขึ้นและค่อย ๆ จางก่อนถูกทำลาย
// คลาสข้อความลอยหนึ่งชิ้น ลอยขึ้นและจางหายเองแล้วทำลายตัวเอง
public class FloatingText : MonoBehaviour
{
    // ความเร็วลอยขึ้น (หน่วย world ต่อวินาที)
    public float moveSpeed = 1.5f;
    // ความเร็วจาง: ค่า alpha ที่ลดลงต่อวินาที (ถ้าเริ่ม alpha = 1 และค่านี้ = 2 จะหายใน 0.5 วินาที)
    public float fadeSpeed = 2f;
    // คอมโพเนนต์ TextMeshPro และสีปัจจุบัน (alpha ถูกลดทุกเฟรม)
    private TextMeshPro textMesh;
    private Color textColor;

    // Unity เรียกตอนสร้าง: หา TextMeshPro และจำสีเริ่มต้นไว้
    void Awake()
    {
        textMesh = GetComponent<TextMeshPro>();
        if (textMesh != null)
        {
            textColor = textMesh.color;
        }
    }

    // แสดงตัวเลขดาเมจ (ปัดเป็นจำนวนเต็ม) และขยับตำแหน่งแบบสุ่มเล็กน้อย
    public void Setup(float damageAmount)
    {
        // เฟส 9: ผู้เล่นปิดตัวเลขดาเมจได้ในหน้า MORE OPTIONS
        if (!GameSettings.DamageNumbers) { Destroy(gameObject); textMesh = null; return; }
        if (textMesh != null)
        {
            textMesh.text = Mathf.RoundToInt(damageAmount).ToString();
            
            // สุ่มกระจายซ้ายขวาเล็กน้อยเพื่อให้ไม่ทับกัน
            transform.position += new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(0f, 0.5f), 0);
        }
    }

    // ข้อความอื่นที่ไม่ใช่ตัวเลข เช่น BLOCKED
    public void Setup(string message, Color color)
    {
        if (textMesh == null) return;
        textMesh.text = message;
        textColor = new Color(color.r, color.g, color.b, 1f);
        textMesh.color = textColor;
        transform.position += new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0f, 0.3f), 0);
    }

    // เน้นตัวเลข (ดาเมจหนัก): เปลี่ยนสีและขยาย
    public void Emphasize(Color color, float scale)
    {
        if (textMesh == null) return;
        textColor = new Color(color.r, color.g, color.b, textColor.a);
        textMesh.color = textColor;
        transform.localScale *= scale;
    }

    // ทุกเฟรม: เลื่อนขึ้นด้วย moveSpeed และลด alpha ตาม fadeSpeed เมื่อจางหมด (alpha <= 0) ทำลายตัวเอง
    void Update()
    {
        transform.position += new Vector3(0, moveSpeed * Time.deltaTime, 0);

        if (textMesh != null)
        {
            textColor.a -= fadeSpeed * Time.deltaTime;
            textMesh.color = textColor;

            if (textColor.a <= 0)
            {
                Destroy(gameObject);
            }
        }
    }
}
