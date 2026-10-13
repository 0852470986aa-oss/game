// GameplayManager.BossRaid.cs — วงจรแมตช์บอส Co-op; ไม่ใช้เงื่อนไข Kill ของโหมดดวล
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using ExitGames.Client.Photon;

public partial class GameplayManager
{
    private bool raidSpawnRequested;
    private TMP_Text raidTitle;
    private Image raidHealth;
    private float nextRaidScan;
    private PlayerController raidBoss;
    private int raidMinionRequested;
    private int raidPhaseRequested;
    private float raidMissingSince = -1;

    private void UpdateBossRaid()
    {
        if (!matchStarted) return;
        var room = PhotonNetwork.CurrentRoom;
        if (Time.unscaledTime >= nextRaidScan)
        {
            nextRaidScan = Time.unscaledTime + .2f;
            raidBoss = null;
            foreach (var ship in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                if (ship.IsBoss) { raidBoss = ship; break; }
        }
        if (raidTitle == null && battleHud != null)
        {
            var panel = BattlePanel("BossRaidHUD", battleHud, 0, 190, 600, 60, new Color(.04f, .025f, .08f, .95f));
            raidTitle = BattleLabel("BossNamePhase", panel.transform, "DREADNOUGHT / APPROACHING", 0, 14, 580, 28, 20);
            BattlePanel("BossHPBackground", panel.transform, 0, -15, 560, 14, new Color(.2f, .08f, .09f));
            raidHealth = BattlePanel("BossHP", panel.transform, 0, -15, 560, 14, new Color(.9f, .2f, .25f));
            raidHealth.rectTransform.pivot = new Vector2(0, .5f);
            raidHealth.rectTransform.anchoredPosition = new Vector2(-280, -15);
        }
        if (raidBoss != null && raidTitle != null)
        {
            int phase = BossRaidRules.Phase(raidBoss.currentHp, raidBoss.maxHp);
            raidTitle.text = "DREADNOUGHT / PHASE " + phase + (phase == 3 ? " / ENRAGED" : "")
                + "  " + Mathf.CeilToInt(raidBoss.currentHp) + " / " + Mathf.CeilToInt(raidBoss.maxHp);
            raidHealth.rectTransform.sizeDelta = new Vector2(560 * Mathf.Clamp01(raidBoss.currentHp / Mathf.Max(1, raidBoss.maxHp)), 14);
        }
        if (!PhotonNetwork.IsMasterClient) return;
        if (room.CustomProperties.TryGetValue(ModeResultKey, out object result) && result is int done && done != 0) return;
        // มีซากบอสอยู่ในห้องด้วย จึงไม่ตีความ "ยังโหลดบอสไม่ถึง" เป็นชัยชนะ
        if (raidBoss != null && raidBoss.isDead)
        {
            SetModeResult(1);
            return;
        }
        CheckCoopDefeat(); // คนจริงหมดชีวิต = แพ้ ไม่ปล่อยให้ผู้เล่นต้องดูบอทสู้จนหมดเวลา
        bool spawned = room.CustomProperties.TryGetValue(BossRaidRules.Spawned, out object s) && s is bool yes && yes;
        if (raidBoss != null) raidMissingSince = -1;
        else if (raidMissingSince < 0) raidMissingSince = Time.unscaledTime;
        // Host หลุดระหว่างเขียน property กับสร้างบอส: ให้ Host ใหม่รอเครือข่ายก่อนกู้การเกิด
        bool recoverSpawn = spawned && !raidSpawnRequested && raidBoss == null && Time.unscaledTime - raidMissingSince > 8f;
        if ((!spawned || recoverSpawn) && !raidSpawnRequested && raidBoss == null)
        {
            raidSpawnRequested = true;
            // เก็บการสุ่มรางวัลครั้งเดียวตั้งแต่เริ่ม เพื่อให้ทุกคนใช้ผลเดียวกันเมื่อเปลี่ยน Host
            var spawnProperties = new Hashtable {
                [BossRaidRules.Spawned] = true, [BossRaidRules.PhaseKey] = 1,
                [BossRaidRules.RoundKey] = room.CustomProperties["BattleToken"],
                [BossRaidRules.MinionsKey] = 0
            };
            // Host ใหม่กู้การเกิดบอสต้องไม่สุ่มตั๋วรอบเดิมใหม่
            if (!room.CustomProperties.ContainsKey(BossRaidRules.TicketKey))
                spawnProperties[BossRaidRules.TicketKey] = Random.value < .1f;
            room.SetCustomProperties(spawnProperties);
            SpawnEnemyBots(0, 3, true);
            BroadcastBanner("BOSS CO-OP / 3 LIVES / WATCH THE WARNING LINE");
            return;
        }
        if (raidBoss == null) return;
        int currentPhase = BossRaidRules.Phase(raidBoss.currentHp, raidBoss.maxHp);
        int savedPhase = room.CustomProperties.TryGetValue(BossRaidRules.PhaseKey, out object p) && p is int n ? n : 1;
        if (currentPhase > Mathf.Max(savedPhase, raidPhaseRequested))
        {
            raidPhaseRequested = currentPhase;
            room.SetCustomProperties(new Hashtable { [BossRaidRules.PhaseKey] = currentPhase });
            BroadcastBanner(currentPhase == 3 ? "BOSS ENRAGED!" : "PHASE 2 / SWEEP LASER + REINFORCEMENTS");
        }
        int minionPhase = room.CustomProperties.TryGetValue(BossRaidRules.MinionsKey, out object m) && m is int k ? k : 0;
        if (currentPhase >= 2 && Mathf.Max(minionPhase, raidMinionRequested) < currentPhase)
        {
            raidMinionRequested = currentPhase;
            room.SetCustomProperties(new Hashtable { [BossRaidRules.MinionsKey] = currentPhase });
            // Host ใหม่ใช้เลขถัดจากทุกตัวที่มีอยู่ ป้องกัน id ชนหลังรับช่วงเกม
            foreach (var ship in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                if (ship.IsBot) botSequence = Mathf.Max(botSequence, ship.CombatantId - PlayerController.BotIdBase + 1);
            SpawnEnemyBots(currentPhase == 3 ? 3 : 2, 2, false);
        }
    }

    // ถูกเรียกภายใต้ resultShown guard ของ ShowCoopResult จึงรับรางวัลได้ครั้งเดียวต่อการจบแมตช์
    private string GrantRaidTicket(bool won)
    {
        if (!won || !FeatureFlags.SkinGacha || !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(BossRaidRules.TicketKey, out object v)
            || !(v is bool granted) || !granted) return "";
        Premium.EnsureLoaded();
        string token = PhotonNetwork.CurrentRoom.CustomProperties["BattleToken"] as string;
        if (!BossRaidRules.TryGrantTicket(Premium.Data, token, true)) return "";
        Premium.Save();
        return " / BONUS: 1 SKIN TICKET";
    }
}
