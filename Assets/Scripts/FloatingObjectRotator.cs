using UnityEngine;

/// <summary>
/// ทำให้วัตถุหมุนอย่างต่อเนื่อง (สำหรับคริสตัลลอยใน Map1)
/// </summary>
public class FloatingObjectRotator : MonoBehaviour
{
    [SerializeField]
    public float rotationSpeed = 15f;

    void Update()
    {
        transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
    }
}
