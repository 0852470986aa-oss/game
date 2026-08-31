using UnityEngine;
using System.Collections;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance;
    public Vector3 currentShakeOffset = Vector3.zero;
    private bool isShaking = false;
    private Coroutine shakeRoutine;

    void Awake()
    {
        Instance = this;
    }

    public void TriggerShake(float duration = 0.5f, float magnitude = 0.3f)
    {
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(Shake(duration, magnitude));
    }

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
