using UnityEngine;
using Photon.Pun;
using System.Collections;

public class MapHazardManager : MonoBehaviour
{
    private int mapIndex = 0;

    void Start()
    {
        if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("MapIndex", out object mapProp))
        {
            mapIndex = (int)mapProp;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            SpawnStaticHazards();
            StartCoroutine(SpawnHazardRoutine());
        }
    }

    private void SpawnStaticHazards()
    {
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

        while (PhotonNetwork.InRoom)
        {
            float waitTime = 10f;
            string hazardPrefabName = "";
            Vector3 spawnPos = new Vector3(Random.Range(-5f, 5f), Random.Range(-10f, 10f), 0);

            if (mapIndex == 0) // Electric Jellyfish Core
            {
                hazardPrefabName = "Hazard_Lightning";
                waitTime = Random.Range(3f, 8f);
            }
            else if (mapIndex == 1) // Obelisk Plains
            {
                hazardPrefabName = "Hazard_SlowZone";
                waitTime = Random.Range(10f, 20f);
            }
            else if (mapIndex == 2) // Abandoned Mech Warzone
            {
                hazardPrefabName = "Hazard_MoltenAsteroid";
                spawnPos = new Vector3(Random.Range(-8f, 8f), 12f, 0); // โผล่จากด้านบนสุด
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
