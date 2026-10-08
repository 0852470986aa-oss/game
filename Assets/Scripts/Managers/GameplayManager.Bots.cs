// GameplayManager.Bots.cs — สร้างยานบอทในแมตช์ + บทสอนเล่น (partial class ของ GameplayManager)
// Master Client สร้างยานบอทด้วย PhotonNetwork.InstantiateRoomObject (ทุกเครื่องเห็น, Master ควบคุมผ่าน BotController)
// จำนวน/ความยากบอทมาจาก Room Properties (MatchRules.BotCount / BotDifficulty) ที่ห้องรอหรือโหมดเล่นคนเดียวตั้งไว้
using UnityEngine;
using Photon.Pun;
using TMPro;

// ส่วนบอทของ GameplayManager: Master สร้างยานบอทตามค่าห้อง และระบบบทสอนเล่น
public partial class GameplayManager
{
    private bool botsSpawned; // Master สร้างบอทของแมตช์นี้ไปแล้วหรือยัง (กันสร้างซ้ำ)
    private bool botsPresent; // จำนวนบอทในฉากครบตามค่าห้องแล้ว (ใช้รอก่อนเริ่มแมตช์)
    private static readonly string[] BotNames = { "NOVA-7", "ORION", "VEGA", "LYRA", "ATLAS", "RIGEL", "SIRIUS", "ZETA", "KAIRO", "ALTAIR", "DENEB", "POLARIS" }; // ชื่อที่สุ่มใช้ตั้งให้ยานบอทแต่ละลำ

    // นับยานบอทที่มีอยู่ในฉาก
    private static int CountBots()
    {
        int count = 0;
        foreach (var ship in FindObjectsByType<PlayerController>(FindObjectsSortMode.None)) if (ship.IsBot) count++;
        return count;
    }

    // Master สร้างบอทครั้งเดียวต่อแมตช์ (ถ้า Master เปลี่ยนคนแล้วบอทมีอยู่แล้วจะไม่สร้างซ้ำ)
    private void SpawnBotsIfNeeded()
    {
        if (botsSpawned || !PhotonNetwork.IsMasterClient || !PhotonNetwork.InRoom) return;
        var room = PhotonNetwork.CurrentRoom;
        int count = MatchRules.BotCount(room);
        if (count <= 0 || CountBots() >= count) { botsSpawned = true; return; }
        int difficulty = MatchRules.BotDifficulty(room);
        int map = GetCurrentMapIndex();
        int humans = MatchRules.ExpectedHumans(room);
        int total = humans + count;
        // ล้างคะแนนบอทของแมตช์ก่อนทั้งหมด (กันคะแนนค้างจากห้องที่เคยมีบอทเยอะกว่า)
        var resetKills = MatchRules.ResetBotScores(count);
        // โหมดทีม: บอทเข้าทีมที่คนน้อยกว่า (นับคนจริงก่อน) แล้วเขียนทีมของบอทลงห้องก่อนสร้าง
        int[] teamCount = new int[2];
        if (isTeamMode)
            foreach (var pilot in PhotonNetwork.PlayerList)
            {
                int t = MatchRules.TeamOf(pilot.ActorNumber);
                if (t >= 0) teamCount[t]++;
            }
        int[] botTeam = new int[count], botTeamIndex = new int[count];
        for (int i = 0; i < count; i++)
        {
            int t = teamCount[0] <= teamCount[1] ? 0 : 1;
            botTeam[i] = t;
            botTeamIndex[i] = teamCount[t]++;
            if (isTeamMode) resetKills[MatchRules.TeamPrefix + (PlayerController.BotIdBase + i)] = t;
        }
        room.SetCustomProperties(resetKills);
        // สุ่มชื่อไม่ซ้ำกัน
        var names = new System.Collections.Generic.List<string>(BotNames);
        for (int i = 0; i < count; i++)
        {
            int id = PlayerController.BotIdBase + i;
            // เป้าซ้อมใช้ยานถึก (Comet Crusher) และไม่มีสกิลโจมตี / บอทปกติสุ่มยาน สกิล สี
            int ship = difficulty == 0 ? 1 : BattleLoadoutCatalog.ValidShip(Random.Range(0, BattleLoadoutCatalog.Ships.Length));
            int skill = difficulty == 0 ? 1 : BattleLoadoutCatalog.ValidSkill(Random.Range(0, BattleLoadoutCatalog.Skills.Length));
            int paint = Random.Range(1, ShipPaint.Colors.Length);
            string pick = names.Count > 0 ? names[Random.Range(0, names.Count)] : "BOT-" + (i + 1);
            names.Remove(pick);
            string name = (difficulty == 0 ? "TARGET DUMMY" : pick) + " [BOT]";
            string prefabName = BattleLoadoutCatalog.PrefabName(ship);
            Vector2 position;
            Quaternion rotation = Quaternion.Euler(0, 0, 180f);
            // ห้องหลายคน: บอทได้ช่องเกิดต่อจากคนจริงในวงรอบสนาม / 1v1: หาจุดปลอดภัยฝั่งบนแบบเดิม
            if (isTeamMode && TryFindTeamSpawn(GetPrefab(prefabName), map, botTeam[i], botTeamIndex[i], MatchRules.TeamSize(room), out position)) rotation = TeamFacing(botTeam[i]);
            else if (isFreeForAll && TryFindSlotSpawn(GetPrefab(prefabName), map, humans + i, total, out position)) rotation = FaceArenaCenter(position, map);
            else if (!TryFindSafeSpawn(null, map, false, out position)) position = new Vector2(0f, 16f);
            object[] data = { "BOT", id, name, ship, skill, paint, difficulty };
            PhotonNetwork.InstantiateRoomObject(prefabName, position, rotation, 0, data);
        }
        botsSpawned = true;
    }

    // รางวัลจบเกม: แมตช์กับบอทได้น้อยกว่า (กันปั๊มเหรียญ)
    private int WinReward => MatchRules.IsBotMatch(PhotonNetwork.CurrentRoom) ? 60 : 190;
    private int LoseReward => MatchRules.IsBotMatch(PhotonNetwork.CurrentRoom) ? 5 : 10;

    // ===== บทสอนเล่น (โหมด TUTORIAL) =====
    private TMP_Text tutorialText;
    private int tutorialStep;
    private Vector2 tutorialStartPos;
    private bool tutorialFired;

    // เรียกทุกเฟรมจาก Update: แสดงขั้นตอนทีละข้อ ทำสำเร็จแล้วไปข้อถัดไป
    private void UpdateTutorial()
    {
        if (!PhotonNetwork.InRoom || !MatchRules.IsTutorial(PhotonNetwork.CurrentRoom) || battleHud == null) return;
        if (tutorialText == null)
        {
            tutorialText = BattleLabel("TutorialHint", battleHud, "", 0, 170, 900, 60, 26);
            tutorialText.color = new Color(1f, .9f, .45f);
        }
        if (localPlayer == null || !matchStarted) { tutorialText.text = "เตรียมตัว... กำลังเริ่มบทสอน"; return; }
        if (fireButton != null && fireButton.isPressed) tutorialFired = true;
        switch (tutorialStep)
        {
            case 0:
                tutorialStartPos = localPlayer.transform.position;
                tutorialStep = 1;
                break;
            case 1:
                tutorialText.text = "1/4  ลากจอยซ้ายล่างเพื่อบินไปรอบ ๆ";
                if (Vector2.Distance(localPlayer.transform.position, tutorialStartPos) > 4f) tutorialStep = 2;
                break;
            case 2:
                tutorialText.text = "2/4  กดปุ่มยิงขวาล่าง (ลากนิ้วเพื่อเล็งทิศ)";
                if (tutorialFired) tutorialStep = 3;
                break;
            case 3:
                tutorialText.text = "3/4  ยิงทำลาย TARGET DUMMY ให้ได้ 1 ครั้ง";
                if (MatchRules.LocalKills() >= 1) tutorialStep = 4;
                break;
            case 4:
                tutorialText.text = "4/4  กดปุ่มสกิลเพื่อใช้ความสามารถพิเศษ";
                if (localPlayer.currentCooldown > 0) tutorialStep = 5;
                break;
            default:
                tutorialText.text = "เยี่ยม! พร้อมลงสนามจริงแล้ว  (ฆ่าให้ครบเพื่อจบบทสอน)";
                break;
        }
    }
}
