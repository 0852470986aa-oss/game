using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using System.Collections.Generic;

public class RadarMinimap : MonoBehaviour
{
    public float radarRange = 25f; // รัศมีที่เรดาร์จะมองเห็น (แม็พใหญ่ขึ้นต้องเพิ่ม)
    public float radarUIRadius = 50f; // รัศมีของ UI เรดาร์บนหน้าจอ (เล็กลง)
    public GameObject blipPrefab;

    private RectTransform radarBG;
    private Dictionary<int, RectTransform> enemyBlips = new Dictionary<int, RectTransform>();

    void Start()
    {
        CreateRadarUI();
    }

    void CreateRadarUI()
    {
        // 1. หา Canvas
        Canvas canvas = null;
        if (GameplayManager.Instance != null && GameplayManager.Instance.playerInfoText != null)
        {
            canvas = GameplayManager.Instance.playerInfoText.canvas;
        }
        if (canvas == null) canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("RadarCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // 2. สร้าง Radar Background
        GameObject bgObj = new GameObject("RadarBackground");
        bgObj.transform.SetParent(canvas.transform, false);
        radarBG = bgObj.AddComponent<RectTransform>();
        radarBG.anchorMin = new Vector2(0, 1); // เปลี่ยนจากขวาเป็นซ้าย (0)
        radarBG.anchorMax = new Vector2(0, 1);
        radarBG.pivot = new Vector2(0, 1);
        radarBG.anchoredPosition = new Vector2(10, -10); // ขยับออกจากขอบซ้าย 10
        radarBG.sizeDelta = new Vector2(radarUIRadius * 2, radarUIRadius * 2);

        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0, 0, 0, 0.4f); // พื้นหลังโปร่งใส (40% opacity)
        
        // ทำให้เป็นวงกลมถ้ามี Sprite วงกลม (ถ้าไม่มีมันจะเป็นสี่เหลี่ยมมืดๆ ไปก่อน)
        Sprite circleSprite = Resources.Load<Sprite>("UI_Circle");
        if (circleSprite != null) bgImg.sprite = circleSprite;

        // 3. สร้าง Center Player Blip (จุดของเราเองตรงกลาง)
        GameObject centerBlip = new GameObject("CenterBlip");
        centerBlip.transform.SetParent(radarBG, false);
        RectTransform centerRect = centerBlip.AddComponent<RectTransform>();
        centerRect.anchoredPosition = Vector2.zero;
        centerRect.sizeDelta = new Vector2(10, 10);
        Image centerImg = centerBlip.AddComponent<Image>();
        centerImg.color = Color.green;
        if (circleSprite != null) centerImg.sprite = circleSprite;
    }

    private PlayerController[] cachedPlayers;
    private float playerCacheTimer = 0f;

    void Update()
    {
        if (GameplayManager.Instance == null || GameplayManager.Instance.localPlayer == null) return;

        PlayerController localPlayer = GameplayManager.Instance.localPlayer;

        // อัปเดตรายชื่อผู้เล่นทุก 2 วินาที แทนทุกเฟรม (ประหยัดเปอร์ฟอร์แมนซ์)
        playerCacheTimer -= Time.deltaTime;
        if (playerCacheTimer <= 0f || cachedPlayers == null)
        {
            playerCacheTimer = 2f;
            cachedPlayers = FindObjectsOfType<PlayerController>();
        }

        foreach (var p in cachedPlayers)
        {
            if (p == null || p == localPlayer) continue; // ข้ามตัวเอง

            int id = p.photonView.ViewID;

            // ถ้าตาย ให้ซ่อนจุด
            if (p.isDead)
            {
                if (enemyBlips.ContainsKey(id))
                {
                    enemyBlips[id].gameObject.SetActive(false);
                }
                continue;
            }

            // คำนวณระยะห่าง
            Vector2 offset = p.transform.position - localPlayer.transform.position;
            float distance = offset.magnitude;

            if (distance <= radarRange)
            {
                // ถ้าอยู่ในระยะ ให้แสดง/สร้างจุด
                if (!enemyBlips.ContainsKey(id))
                {
                    enemyBlips[id] = CreateBlip(Color.red);
                }

                enemyBlips[id].gameObject.SetActive(true);

                // คำนวณตำแหน่งบน UI เรดาร์
                Vector2 normalizedPos = offset / radarRange; // -1 to 1
                enemyBlips[id].anchoredPosition = normalizedPos * radarUIRadius;
            }
            else
            {
                // ถ้าอยู่นอกระยะ ให้ซ่อน
                if (enemyBlips.ContainsKey(id))
                {
                    enemyBlips[id].gameObject.SetActive(false);
                }
            }
        }
    }

    private RectTransform CreateBlip(Color color)
    {
        GameObject blipObj = new GameObject("EnemyBlip");
        blipObj.transform.SetParent(radarBG, false);
        RectTransform blipRect = blipObj.AddComponent<RectTransform>();
        blipRect.sizeDelta = new Vector2(10, 10);
        Image blipImg = blipObj.AddComponent<Image>();
        blipImg.color = color;
        Sprite circleSprite = Resources.Load<Sprite>("UI_Circle");
        if (circleSprite != null) blipImg.sprite = circleSprite;
        return blipRect;
    }
}
