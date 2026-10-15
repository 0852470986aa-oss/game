using UnityEngine;

// ฉากสนาม Raid ใช้ชิ้นส่วนเดิมร่วมกับวงโฮโลแกรม ไม่มีตัวชนหรือดาเมจแฝง
public sealed class BossCitadelVisuals : MonoBehaviour
{
    private readonly Transform[] rings = new Transform[3];
    private void Start()
    {
        for (int i = 0; i < rings.Length; i++)
        {
            var go = new GameObject("CitadelEnergyRing" + i);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = i == 1 ? UiFx.Rays20 : UiFx.Ring;
            sr.sortingOrder = -4;
            sr.color = i == 1 ? new Color(.22f,.5f,1f,.12f) : new Color(.45f,.3f,1f,.28f);
            go.transform.localScale = Vector3.one * (28 + i * 13) / sr.sprite.bounds.size.y;
            rings[i] = go.transform;
        }
    }
    private void Update()
    {
        for (int i = 0; i < rings.Length; i++)
            if (rings[i] != null) rings[i].localRotation = Quaternion.Euler(0,0,Time.time * (i % 2 == 0 ? 3 : -4));
    }
}
