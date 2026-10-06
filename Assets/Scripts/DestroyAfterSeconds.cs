using UnityEngine;

// ตัวช่วยลบเอฟเฟกต์/วัตถุชั่วคราวเมื่อหมดอายุ ป้องกันวัตถุค้างในฉาก
public class DestroyAfterSeconds : MonoBehaviour
{
    public float lifetime = 1f;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }
}
