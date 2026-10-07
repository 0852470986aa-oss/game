// PlayerController.Bot.cs — ตัวตนของยาน (คน/บอท) และช่องรับคำสั่งจากบอท (partial class ของ PlayerController)
// ยานบอทคือ Prefab ยานเดียวกับผู้เล่น สร้างด้วย PhotonNetwork.InstantiateRoomObject โดย Master Client
// InstantiationData = { "BOT", botId, ชื่อ, ยาน, สกิล, สี, ความยาก } — Master เป็นคนควบคุม (photonView.IsMine บนเครื่อง Master)
// BotController (สมองบอท) เขียนค่า botMove / botAim / botFire / botSkill แทนจอยและปุ่มบนจอ
using UnityEngine;
using Photon.Pun;

public partial class PlayerController : IPunInstantiateMagicCallback
{
    // เลขประจำตัวบอทเริ่มที่ 1000 (ไม่ชนกับ ActorNumber ของคนจริงที่เป็นเลขน้อย ๆ)
    public const int BotIdBase = 1000;

    public bool IsBot { get; private set; }
    public int BotDifficulty { get; private set; }
    private int botId;
    private string botName = "BOT";
    private int botShip;
    private int botSkillIndex;
    private int botPaint;

    // คำสั่งจากสมองบอท (ใช้เฉพาะเมื่อ IsBot)
    [System.NonSerialized] public Vector2 botMove;
    [System.NonSerialized] public Vector2 botAim;
    [System.NonSerialized] public bool botFire;
    [System.NonSerialized] public bool botSkill;

    // เลขผู้ต่อสู้: คน = ActorNumber, บอท = 1000+ (ใช้นับ Kill / เช็คว่าเป็นกระสุนของใคร)
    public int CombatantId => IsBot ? botId : photonView.OwnerActorNr;
    // ชื่อที่แสดงบนจอ
    public string PilotName => IsBot ? botName : photonView.Owner != null ? photonView.Owner.NickName : "PILOT";
    // ยานที่คนบนเครื่องนี้บังคับเอง (ไม่ใช่บอทที่ Master ควบคุม)
    public bool IsLocalHuman => photonView.IsMine && !IsBot;
    public int ShipIndex => IsBot ? botShip : photonView.Owner != null
        && photonView.Owner.CustomProperties.TryGetValue("ShipType", out object value) && value is int ship ? BattleLoadoutCatalog.ValidShip(ship) : 0;

    // Photon เรียกตอนยานถูกสร้างบนแต่ละเครื่อง (ก่อน Start): อ่านว่าเป็นบอทหรือไม่
    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        object[] data = photonView.InstantiationData;
        if (data == null || data.Length < 7 || !(data[0] is string tag) || tag != "BOT") return;
        IsBot = true;
        botId = data[1] is int id ? id : BotIdBase;
        botName = data[2] as string ?? "BOT";
        botShip = data[3] is int ship ? BattleLoadoutCatalog.ValidShip(ship) : 0;
        botSkillIndex = data[4] is int skill ? BattleLoadoutCatalog.ValidSkill(skill) : 0;
        botPaint = data[5] is int paint ? ShipPaint.Valid(paint) : 0;
        BotDifficulty = data[6] is int level ? Mathf.Clamp(level, 0, 3) : 1;
        // เฟส 7B: data[7] = บอส (เลือด x6 ตัวใหญ่ แรงขึ้น) ใช้ในโหมด Survival/Campaign
        IsBoss = data.Length > 7 && data[7] is bool boss && boss;
    }

    public bool IsBoss { get; private set; }

    // บอส: เลือด 6 เท่า แรง 1.4 เท่า ช้าลงนิดหน่อย ตัวใหญ่ 1.6 เท่า (ทุกเครื่องคำนวณเหมือนกัน)
    private void ApplyBossStats()
    {
        if (!IsBoss) return;
        maxHp *= 6f;
        attack *= 1.4f;
        speed *= .85f;
        transform.localScale *= 1.6f;
    }

    // RPC มาจากผู้ควบคุมยานลำนี้จริงไหม (คน = เจ้าของยาน, บอท = Master Client)
    private bool FromController(PhotonMessageInfo info)
    {
        if (IsBot) return info.Sender != null && info.Sender.IsMasterClient;
        return info.Sender == photonView.Owner;
    }

    // หายานจากเลขผู้ต่อสู้ (ใช้หาชื่อคนฆ่า / ทิศคนยิง)
    public static PlayerController FindCombatant(int combatantId)
    {
        foreach (var ship in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            if (ship.CombatantId == combatantId) return ship;
        return null;
    }

    // ตั้งค่าสถานะยานบอทจาก Catalog (เรียกแทนการอ่าน Custom Properties ของผู้เล่น)
    private void LoadBotLoadout(out int shipIndex, out int skillIndex)
    {
        shipIndex = botShip;
        skillIndex = botSkillIndex;
    }

    // Master Client เปลี่ยนคน (Master เดิมหลุด): เครื่อง Master ใหม่เป็นเจ้าของยานบอทแทน
    // เปลี่ยนฟิสิกส์ให้ขยับเองได้ และถ้าบอทตายค้างอยู่ (Coroutine เกิดใหม่หายไปกับเครื่องเดิม) ให้เริ่มนับเกิดใหม่ที่นี่
    public void OnBotControlTransferred()
    {
        if (!IsBot) return;
        ConfigureShipPhysics(photonView.IsMine);
        if (!photonView.IsMine) return;
        if (playerRigidbody != null) { playerRigidbody.position = transform.position; playerRigidbody.linearVelocity = Vector2.zero; }
        if (isDead && !matchEnded) StartCoroutine(RespawnRoutine());
    }
}
