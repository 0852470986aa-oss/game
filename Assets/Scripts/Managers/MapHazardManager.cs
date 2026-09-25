using UnityEngine;
using Photon.Pun;
using System.Collections;

// Local presentation only. Gravity is evaluated by each ship owner, then normal ship sync replicates it.
public class JellyArenaVisuals : MonoBehaviour
{
    private static JellyArenaVisuals active;
    private readonly Vector2[] wells = { new Vector2(-17, 10), new Vector2(17,-10),
        new Vector2(19,16), new Vector2(-19,-16), new Vector2(-27,0), new Vector2(27,0) };
    private readonly System.Collections.Generic.List<SpriteRenderer> ornaments = new System.Collections.Generic.List<SpriteRenderer>();
    private readonly System.Collections.Generic.List<Vector3> origins = new System.Collections.Generic.List<Vector3>();
    private readonly System.Collections.Generic.List<SpriteRenderer> debris = new System.Collections.Generic.List<SpriteRenderer>();
    private SpriteRenderer Add(Sprite sprite, Vector2 point, float size, string label, int order)
    {
        var item = new GameObject(label);
        item.transform.SetParent(transform, false);
        item.transform.localPosition = point;
        var art = item.AddComponent<SpriteRenderer>();
        art.sprite = sprite;
        art.sortingOrder = order;
        item.transform.localScale = Vector3.one * (size / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));
        return art;
    }
    void Awake()
    {
        var sheet = SkillSheetVisual.Load("Props_Jellyfish");
        var jelly = System.Array.Find(sheet, s => s.name == "Props_Jellyfish_1");
        var orb = System.Array.Find(sheet, s => s.name == "Props_Jellyfish_2");
        if (jelly == null || orb == null) { enabled = false; return; }
        foreach (Transform child in transform)
            if (child.name != "Background") child.gameObject.SetActive(false);
        active = this;
        // A visible core anchors the map visually and matches the EnergyCore zone.
        var core = Add(orb, Vector2.zero, 10, "EnergyCoreArtwork", -1);
        ornaments.Add(core); origins.Add(core.transform.localPosition);
        foreach (Vector2 point in wells)
        {
            var art = Add(orb, point, 5, "GravityWell", -1);
            ornaments.Add(art); origins.Add(art.transform.localPosition);
        }
        foreach (Vector2 point in new[] { new Vector2(-16,18), new Vector2(18,-17), new Vector2(22,14) })
        {
            var art = Add(jelly, point, 10, "FloatingJellyfish", -2);
            ornaments.Add(art); origins.Add(art.transform.localPosition);
        }
        var rocks = SkillSheetVisual.Load("Obs_Asteroids");
        var rock = System.Array.Find(rocks, s => s.name == "Obs_Asteroids_1");
        if (rock != null)
            for (int i=0; i<24; i++) debris.Add(Add(rock, Vector2.zero, 1, "VortexDecoration", -6));
    }
    public static Vector2 PullAt(Vector2 position)
    {
        if (active == null || !active.isActiveAndEnabled) return Vector2.zero;
        Vector2 pull = Vector2.zero;
        foreach (Vector2 point in active.wells)
        {
            Vector2 delta = (Vector2)active.transform.TransformPoint(point) - position;
            float distance = delta.magnitude;
            if (distance > .15f && distance < 7)
                pull += delta.normalized * (2.8f * (1-distance/7) * Mathf.Min(1,distance));
        }
        return Vector2.ClampMagnitude(pull, 2.8f);
    }
    void Update()
    {
        float time = (float)(PhotonNetwork.Time % 10000);
        for (int i=0;i<ornaments.Count;i++)
        {
            var art = ornaments[i];
            // Index 0 is the core, followed by the gravity wells.
            if (i <= wells.Length) art.transform.localRotation = Quaternion.Euler(0,0,time*12+i*47);
            else art.transform.localPosition = origins[i] + Vector3.up * Mathf.Sin(time*.65f+i)*.65f;
            art.color = new Color(1,1,1,i <= wells.Length ? .78f+.14f*Mathf.Sin(time*2+i) : .78f);
        }
        for (int i=0;i<debris.Count;i++)
        {
            float progress = Mathf.Repeat(time/18f+i/(float)debris.Count,1);
            float radius = 30*(1-progress);
            float angle = i*2.4f+progress*Mathf.PI*4;
            var art = debris[i];
            art.transform.localPosition = new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,0);
            art.transform.localRotation = Quaternion.Euler(0,0,time*24+i*37);
            float size = Mathf.Lerp(1.3f,.08f,progress)/Mathf.Max(art.sprite.bounds.size.x,art.sprite.bounds.size.y);
            art.transform.localScale = Vector3.one*size;
            art.color = new Color(.55f,.8f,1,.35f*Mathf.Min(progress*8,(1-progress)*5));
        }
    }
    void OnDisable() { if (active == this) active = null; }
    void OnEnable() { active = this; }
}

public class MapHazardManager : MonoBehaviourPunCallbacks
{
    private int mapIndex = 0;
    private Coroutine hazardLoop;

    public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
    {
        if (hazardLoop != null) StopCoroutine(hazardLoop);
        hazardLoop = PhotonNetwork.IsMasterClient ? StartCoroutine(SpawnHazardRoutine()) : null;
    }

    void Start()
    {
        if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("MapIndex", out object mapProp))
        {
            mapIndex = (int)mapProp;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            SpawnStaticHazards();
            hazardLoop = StartCoroutine(SpawnHazardRoutine());
        }
    }

    private void SpawnStaticHazards()
    {
        if (mapIndex == 1)
        {
            // Spawn only after layout and players have finished initializing.
        }
        // Obstacle ทั้งหมด (กำแพง, เสาหิน, Cover) ถูกสร้างใน GameplayManager.GenerateMapObstacles() แล้ว
        // ที่นี่เหลือแค่ Hazard พิเศษที่ต้อง Sync ผ่าน Network เท่านั้น
        if (mapIndex == 0)
        {
            // Energy Core ตรงกลาง (ดูดเลือด + บูสต์ Fire Rate)
            PhotonNetwork.InstantiateRoomObject("Hazard_EnergyCore", Vector3.zero, Quaternion.identity);
        }
    }

    private IEnumerator SpawnHazardRoutine()
    {
        while (PhotonNetwork.InRoom && GameplayManager.Instance != null && !GameplayManager.Instance.MatchInputAllowed)
            yield return null;
        // Wait a few seconds before hazards start
        yield return new WaitForSeconds(5f);

        while (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
        {
            if (GameplayManager.Instance == null || !GameplayManager.Instance.MatchInputAllowed)
            { yield return null; continue; }
            float waitTime = 10f;
            string hazardPrefabName = "";
            float hazardHalfWidth = mapIndex == 1 ? 56f : 30f;
            Vector3 spawnPos = new Vector3(Random.Range(-hazardHalfWidth, hazardHalfWidth), Random.Range(-28f, 28f), 0);

            if (mapIndex == 0) // Electric Jellyfish Core
            {
                var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
                var active = System.Array.FindAll(players, p => !p.isDead);
                if (active.Length > 0)
                {
                    Vector2 target = active[Random.Range(0, active.Length)].transform.position;
                    target += Random.insideUnitCircle * 2f;
                    Vector2 min = GameplayManager.GetArenaMin(mapIndex);
                    Vector2 max = GameplayManager.GetArenaMax(mapIndex);
                    spawnPos = new Vector3(Mathf.Clamp(target.x, min.x + 2, max.x - 2), Mathf.Clamp(target.y, min.y + 2, max.y - 2), 0);
                }
                hazardPrefabName = "Hazard_Lightning";
                waitTime = Random.Range(3f, 8f);
            }
            else if (mapIndex == 1) // Obelisk Plains
            {
                SpawnPrismSwamp();
                hazardPrefabName = ""; // Permanent authored swamp regions are created once above.
                waitTime = Random.Range(4f, 6f);
            }
            else if (mapIndex == 2) // Abandoned Mech Warzone
            {
                hazardPrefabName = "Hazard_MoltenAsteroid";
                Vector2 min = GameplayManager.GetArenaMin(mapIndex);
                Vector2 max = GameplayManager.GetArenaMax(mapIndex);
                spawnPos = new Vector3(Random.Range(min.x + 1f, max.x - 1f), max.y + 3f, 0);
                waitTime = Random.Range(3f, 6f); // เกิดถี่หน่อย
            }

            if (!string.IsNullOrEmpty(hazardPrefabName))
            {
                PhotonNetwork.InstantiateRoomObject(hazardPrefabName, spawnPos, Quaternion.identity);
            }

            yield return new WaitForSeconds(waitTime);
        }
    }

    private void SpawnPrismSwamp()
    {
        int count = 0;
        foreach (var hazard in FindObjectsByType<HazardController>(FindObjectsSortMode.None))
            if (hazard.type == HazardController.HazardType.SlowZone) count++;
        if (count >= 6) return;
        Vector2 min = GameplayManager.GetArenaMin(1), max = GameplayManager.GetArenaMax(1);
        for (int attempt = 0; attempt < 60; attempt++)
        {
            Vector2 point = new Vector2(Random.Range(min.x + 6, max.x - 6), Random.Range(min.y + 6, -9));
            if (Vector2.Distance(point, new Vector2(0,-16)) < 9) continue;
            if (Physics2D.OverlapCircleAll(point, 3).Length > 0) continue;
            PhotonNetwork.InstantiateRoomObject("Hazard_SlowZone", point, Quaternion.identity, 0,
                new object[] { false, Random.Range(85,88) });
            break;
        }
    }
}
