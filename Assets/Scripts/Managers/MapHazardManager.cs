using UnityEngine;
using Photon.Pun;
using System.Collections;

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
            Vector2 min = GameplayManager.GetArenaMin(mapIndex);
            Vector2 max = GameplayManager.GetArenaMax(mapIndex);
            for (int i = 0; i < 3; i++)
                PhotonNetwork.InstantiateRoomObject("Hazard_SlowZone",
                    new Vector3(Mathf.Lerp(min.x, max.x, .25f + i * .25f), Mathf.Lerp(min.y, max.y, .22f), 0),
                    Quaternion.identity, 0, new object[] { true });
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
        // Wait a few seconds before hazards start
        yield return new WaitForSeconds(5f);

        while (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
        {
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
                hazardPrefabName = ""; // Permanent authored swamp regions are created once above.
                waitTime = Random.Range(10f, 20f);
            }
            else if (mapIndex == 2) // Abandoned Mech Warzone
            {
                hazardPrefabName = "Hazard_MoltenAsteroid";
                Vector2 min = GameplayManager.GetArenaMin(mapIndex);
                Vector2 max = GameplayManager.GetArenaMax(mapIndex);
                spawnPos = new Vector3(Random.Range(min.x + 3f, max.x - 3f), max.y - 2f, 0);
                waitTime = Random.Range(3f, 6f); // เกิดถี่หน่อย
            }

            if (!string.IsNullOrEmpty(hazardPrefabName))
            {
                PhotonNetwork.InstantiateRoomObject(hazardPrefabName, spawnPos, Quaternion.identity);
            }

            yield return new WaitForSeconds(waitTime);
        }
    }
}
