using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class RadarMinimap : MonoBehaviour
{
    public float radarRange = 25f;
    public float radarUIRadius = 50f;
    public GameObject blipPrefab;
    private RectTransform panel, map, pilot;
    private Vector2 arenaMin, arenaMax;
    private float scale, refresh;
    private readonly Dictionary<int, RectTransform> enemies = new Dictionary<int, RectTransform>();
    private PlayerController[] players;
    private TMP_Text state;

    private RectTransform Box(string name, Transform parent, Vector2 size, Color color)
    {
        var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        var image = rect.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private void Create(Transform hud)
    {
        arenaMin = GameplayManager.GetArenaMin(GameplayManager.GetCurrentMapIndex());
        arenaMax = GameplayManager.GetArenaMax(GameplayManager.GetCurrentMapIndex());
        panel = Box("ArenaMinimap", hud, new Vector2(184, 178), new Color(.025f, .05f, .1f, .9f));
        panel.anchoredPosition = new Vector2(-526, 253);
        Vector2 span = arenaMax - arenaMin;
        scale = Mathf.Min(162f / span.x, 136f / span.y);
        map = Box("ArenaBounds", panel, span * scale, new Color(.12f, .26f, .34f, .9f));
        map.anchoredPosition = new Vector2(0, -8);
        var interior = Box("Interior", map, map.sizeDelta - Vector2.one * 2, new Color(.025f, .06f, .12f, 1));
        // Authored cover only: no bullets, ships, dynamic hazards or invisible boundary walls.
        foreach (var collider in FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
        {
            if (collider.isTrigger || !collider.enabled || !collider.gameObject.activeInHierarchy
                || collider.GetComponentInParent<PlayerController>() != null
                || collider.GetComponentInParent<BulletController>() != null
                || collider.GetComponentInParent<SkillController>() != null) continue;
            var sprite = collider.GetComponent<SpriteRenderer>();
            if (sprite == null || !sprite.enabled || sprite.sprite == null) continue;
            Bounds bounds = collider.bounds;
            if (bounds.center.x < arenaMin.x || bounds.center.x > arenaMax.x
                || bounds.center.y < arenaMin.y || bounds.center.y > arenaMax.y) continue;
            Vector2 size = (Vector2)bounds.size * scale;
            size = Vector2.Min(size, map.sizeDelta - Vector2.one * 2);
            var cover = Box("Cover", map, Vector2.Max(size, Vector2.one * 2), new Color(.3f, .43f, .49f, .8f));
            cover.anchoredPosition = MapPosition(bounds.center);
        }
        pilot = Box("YourShip", map, new Vector2(6, 9), new Color(.25f, 1f, .85f));
        var nose = Box("Heading", pilot, new Vector2(2, 5), Color.white);
        nose.anchoredPosition = new Vector2(0, 6);
        state = new GameObject("RadarLabel", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        state.transform.SetParent(panel, false);
        state.rectTransform.sizeDelta = new Vector2(176, 23);
        state.rectTransform.anchoredPosition = new Vector2(0, 73);
        state.fontSize = 12;
        state.alignment = TextAlignmentOptions.Center;
        state.raycastTarget = false;
        if (GameplayManager.Instance.playerInfoText != null) state.font = GameplayManager.Instance.playerInfoText.font;
        state.text = "ARENA / RADAR 25";
    }

    private Vector2 MapPosition(Vector2 world)
    {
        Vector2 position = (world - (arenaMin + arenaMax) * .5f) * scale;
        Vector2 edge = map.sizeDelta * .5f - Vector2.one * 5;
        return new Vector2(Mathf.Clamp(position.x, -edge.x, edge.x), Mathf.Clamp(position.y, -edge.y, edge.y));
    }

    private void Update()
    {
        var manager = GameplayManager.Instance;
        if (manager == null || manager.playerInfoText == null) return;
        if (panel == null)
        {
            var hud = manager.playerInfoText.canvas.transform.Find("BattleHUD");
            if (hud == null) return;
            Create(hud);
        }
        var local = manager.localPlayer;
        pilot.gameObject.SetActive(local != null && !local.isDead);
        foreach (var marker in enemies.Values) marker.gameObject.SetActive(false);
        if (local == null || local.isDead || local.HasMatchEnded) return;
        pilot.anchoredPosition = MapPosition(local.transform.position);
        pilot.localRotation = Quaternion.Euler(0, 0, local.transform.eulerAngles.z);
        refresh -= Time.unscaledDeltaTime;
        if (refresh <= 0 || players == null)
        {
            refresh = 1f;
            players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        }
        foreach (var other in players)
        {
            if (other == null || other == local || other.isDead
                || Vector2.Distance(other.transform.position, local.transform.position) > radarRange) continue;
            int id = other.photonView.ViewID;
            if (!enemies.TryGetValue(id, out var marker))
            {
                marker = Box("Rival", map, new Vector2(5, 5), new Color(1f, .3f, .25f));
                enemies[id] = marker;
            }
            marker.gameObject.SetActive(true);
            marker.anchoredPosition = MapPosition(other.transform.position);
        }
        pilot.SetAsLastSibling();
    }

    private void OnDestroy()
    {
        if (panel != null) Destroy(panel.gameObject);
    }
}
