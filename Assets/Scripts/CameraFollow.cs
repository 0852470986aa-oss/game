using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 5f;
    public Vector3 offset = new Vector3(0, 0, -10f);
    public Camera cam;

    // Camera Bounds — กล้องจะไม่หลุดออกนอกขอบแม็พ
    public Vector2 arenaMin = new Vector2(-25f, -25f);
    public Vector2 arenaMax = new Vector2(25f, 25f);

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.orthographicSize = 10f; // ซูมออกให้เห็นกว้างขึ้น
        }
    }

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

        // Clamp Camera Position เพื่อไม่ให้กล้องโชว์นอกแม็พ
        if (cam != null)
        {
            float camHeight = cam.orthographicSize;
            float camWidth = camHeight * cam.aspect;
            smoothedPosition.x = Mathf.Clamp(smoothedPosition.x, arenaMin.x + camWidth, arenaMax.x - camWidth);
            smoothedPosition.y = Mathf.Clamp(smoothedPosition.y, arenaMin.y + camHeight, arenaMax.y - camHeight);
        }

        transform.position = smoothedPosition;
    }
}
