// PlayerController.Authority.cs — กันโกงครึ่งทาง: Host (Master Client) ตัดสินดาเมจแทนคนยิง (เฟส 9)
// เดิม: คนยิงตรวจชนเองแล้วสั่ง TakeDamage ไปที่ยานเป้าหมายตรงๆ (เครื่องที่ถูกแก้โปรแกรมจะยิงแรงเท่าไหร่ก็ได้)
// ใหม่: คนยิงส่ง "คำขอดาเมจ" ไปที่ Host -> Host ตรวจ 5 ข้อ แล้วจึงสั่ง TakeDamage ให้ (ยานรับดาเมจเฉพาะที่มาจาก Host)
//   1) คนส่งต้องเป็นเจ้าของยานที่อ้างว่ายิงจริง (กันปลอมชื่อคนยิงเพื่อขโมย Kill)
//   2) ดาเมจต่อนัดไม่เกินเพดานของยานนั้น (ค่า atk ในแคตตาล็อก x4 เผื่อตีบวก/ไอเท็ม/พาวเวอร์อัป) สกิลไม่เกิน 160
//   3) ระยะห่างคนยิงกับเป้าหมายไม่เกิน 120 หน่วย
//   4) โดนได้ไม่เกิน 25 ครั้งต่อวินาทีต่อคนยิง
//   5) ห้ามยิงตัวเอง/เพื่อนร่วมทีม
// เรียกว่า "ครึ่งทาง" เพราะการตรวจชนยังทำที่เครื่องคนยิง (Host แค่ตรวจว่าสมเหตุสมผล) — ปิด FeatureFlags.HostDamage = กลับไปแบบเดิม
// ดาเมจของ Host เอง บอท และอันตรายในแม็พ (Host เป็นคนสั่ง) ผ่านตามปกติ / ยานชนลาวา (เรียกในเครื่องตัวเอง) ไม่ผ่าน RPC จึงไม่กระทบ
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

// ส่วนกันโกงของ PlayerController (partial): ส่งคำขอดาเมจให้ Host ตรวจ แล้วรับดาเมจเฉพาะที่ Host ส่งมา
public partial class PlayerController
{
    internal const float MaxHitDistance = 120f; // ระยะห่างสูงสุดระหว่างคนยิงกับเป้า ถ้าไกลกว่านี้ Host ปฏิเสธดาเมจ
    internal const int MaxHitsPerSecond = 25; // จำนวนครั้งที่ยิงโดนได้สูงสุดต่อวินาที กันยิงรัวผิดปกติ
    internal const float MaxSkillHit = 160f; // ดาเมจสูงสุดต่อครั้งของสกิลที่ Host ยอมรับ

    // ใช้ระบบ Host ตัดสินดาเมจอยู่หรือไม่ (ออฟไลน์/เล่นคนเดียวไม่ต้อง)
    public static bool HostDamageActive => FeatureFlags.HostDamage && PhotonNetwork.InRoom && !PhotonNetwork.OfflineMode;

    // ส่งดาเมจไปที่ยาน target (เรียกจาก BulletController / SkillController บนเครื่องคนยิง)
    public static void SendDamage(PlayerController target, float damage, int shooterId, bool skill)
    {
        if (target == null || target.photonView == null) return;
        MatchRules.RecordDamage(shooterId, damage, target); // เงื่อนไขชนะ MOST DAMAGE (MatchRules.WinRules.cs)
        if (!target.isDead && shooterId != target.CombatantId) MatchStats.Hit(shooterId, Mathf.Min(damage, Mathf.Max(1f, target.currentHp)), skill); // สถิติหลังแมตช์
        if (!HostDamageActive)
        {
            target.photonView.RPC("TakeDamage", RpcTarget.All, damage, shooterId);
            return;
        }
        target.photonView.RPC(nameof(RequestDamageRPC), RpcTarget.MasterClient, damage, shooterId, skill);
    }

    // RPC ดาเมจที่ยานรับจากเครือข่าย: เมื่อเปิดระบบ Host จะรับเฉพาะที่ Host ส่งมา
    [PunRPC]
    public void TakeDamage(float damage, int killerId, PhotonMessageInfo info)
    {
        if (HostDamageActive && info.Sender != null && !info.Sender.IsMasterClient) return;
        TakeDamage(damage, killerId);
    }

    // รันบนเครื่อง Host: ตรวจคำขอดาเมจ ผ่านแล้วสั่ง TakeDamage ไปทุกเครื่อง (เจ้าของยานเป็นคนหักเลือดเหมือนเดิม)
    [PunRPC]
    public void RequestDamageRPC(float damage, int shooterId, bool skill, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        string reason = HitValidator.Check(this, damage, shooterId, skill, info.Sender);
        if (reason != null) { HitValidator.Reject(shooterId, reason); return; }
        photonView.RPC("TakeDamage", RpcTarget.All, damage, shooterId);
    }
}

// ตัวตรวจคำขอดาเมจ (ทำงานบน Host เท่านั้น)
public static class HitValidator
{
    // เวลาที่ยิงโดนล่าสุดของแต่ละคนยิง (ใช้นับจำนวนครั้งต่อวินาที)
    private static readonly Dictionary<int, Queue<float>> recentHits = new Dictionary<int, Queue<float>>();
    private static readonly Dictionary<int, float> nextNotice = new Dictionary<int, float>();
    // จำนวนคำขอที่ถูกปฏิเสธทั้งหมด (ดูได้ใน Log)
    public static int Rejected { get; private set; }

    // คืน null = ผ่าน, ไม่งั้นคืนเหตุผล
    public static string Check(PlayerController target, float damage, int shooterId, bool skill, Photon.Realtime.Player sender)
    {
        if (target == null || target.isDead) return target == null ? "no target" : "target dead";
        if (float.IsNaN(damage) || damage <= 0f) return "bad damage";
        // Host เอง / บอท (Host คุมบอท) = เชื่อถือได้
        if (sender == null || sender.IsMasterClient) return null;
        var shooter = PlayerController.FindCombatant(shooterId);
        if (shooter == null || shooter.IsBot) return "unknown shooter";
        if (shooter.photonView.OwnerActorNr != sender.ActorNumber) return "spoofed shooter";
        if (shooterId == target.CombatantId || MatchRules.IsAlly(shooterId, target.CombatantId)) return "friendly hit";
        float cap = skill ? PlayerController.MaxSkillHit : BattleLoadoutCatalog.Ships[BattleLoadoutCatalog.ValidShip(shooter.ShipIndex)].atk * 4f + 1f;
        if (damage > cap) return "damage " + damage.ToString("0.#") + " > " + cap.ToString("0.#");
        if (Vector2.Distance(shooter.transform.position, target.transform.position) > PlayerController.MaxHitDistance) return "too far";
        if (!recentHits.TryGetValue(shooterId, out var times)) recentHits[shooterId] = times = new Queue<float>();
        float now = Time.unscaledTime;
        while (times.Count > 0 && now - times.Peek() > 1f) times.Dequeue();
        if (times.Count >= PlayerController.MaxHitsPerSecond) return "hit rate";
        times.Enqueue(now);
        return null;
    }

    // บันทึกการปฏิเสธ และแจ้งใน Kill Feed ของ Host (ไม่เกิน 1 ครั้งต่อ 10 วิต่อคน)
    public static void Reject(int shooterId, string reason)
    {
        Rejected++;
        Debug.LogWarning("HostDamage: rejected hit from " + shooterId + " (" + reason + ")");
        if (reason == "target dead" || reason == "no target") return;
        if (nextNotice.TryGetValue(shooterId, out float next) && Time.unscaledTime < next) return;
        nextNotice[shooterId] = Time.unscaledTime + 10f;
        var shooter = PlayerController.FindCombatant(shooterId);
        if (GameplayManager.Instance != null && shooter != null)
            GameplayManager.Instance.AddFeedLine("Blocked suspicious hit: " + shooter.PilotName, new Color(1f, .45f, .45f));
    }
}
