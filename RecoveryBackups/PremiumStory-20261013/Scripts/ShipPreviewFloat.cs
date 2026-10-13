using UnityEngine;

// ขยับเฉพาะรูปใน UI ไม่ขยับปุ่ม/ตัวชน/ตำแหน่งยานในระบบเครือข่าย
public sealed class ShipPreviewFloat : MonoBehaviour
{
    private Vector3 origin;
    private Quaternion rotation;
    private float phase;
    private bool capture;
    private void OnEnable() { origin = transform.localPosition; rotation = transform.localRotation; phase = GetInstanceID() % 31; capture = true; }
    private void LateUpdate()
    {
        if (capture) { origin = transform.localPosition; rotation = transform.localRotation; capture = false; }
        float t = Time.unscaledTime * 1.7f + phase;
        transform.localPosition = origin + Vector3.up * (Mathf.Sin(t) * 3f);
        transform.localRotation = rotation * Quaternion.Euler(0, 0, Mathf.Sin(t * .7f) * 2f);
    }
    private void OnDisable() { transform.localPosition = origin; transform.localRotation = rotation; }
}
