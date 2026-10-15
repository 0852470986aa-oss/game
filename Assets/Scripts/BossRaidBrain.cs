// BossRaidBrain.cs — บอส 3 เฟส: Host คุมการเคลื่อนที่/ดาเมจ ทุกเครื่องอ่านสัญญาณเตือนเดียวกันจากห้อง
using UnityEngine;
using Photon.Pun;
using ExitGames.Client.Photon;

public sealed class BossRaidBrain : MonoBehaviour
{
    private PlayerController ship;
    private BossWeaponSpectacle weaponFx;
    private LineRenderer beam;
    private LineRenderer beamGlow;
    private SpriteRenderer chargeGlow;
    private Material beamMaterial;
    private double seenAttack = -1;
    private int lastTick = -1;
    private bool volleyFired;
    private const string AttackKey = BossRaidRules.Prefix + "Attack";
    private const string AngleKey = BossRaidRules.Prefix + "Angle";
    private const string LaserKey = BossRaidRules.Prefix + "Laser";
    private const float Windup = 1.25f, SweepTime = 2f, BeamLength = 45f;

    private void Start()
    {
        ship = GetComponent<PlayerController>();
        weaponFx = gameObject.AddComponent<BossWeaponSpectacle>();
        var line = new GameObject("RaidAttackTelegraph");
        line.transform.SetParent(transform, false);
        beam = line.AddComponent<LineRenderer>();
        beamMaterial = new Material(Shader.Find("Sprites/Default"));
        beam.sharedMaterial = beamMaterial;
        beam.useWorldSpace = true;
        beam.positionCount = 2;
        beam.sortingOrder = 60;
        beam.enabled = false;
        var glowLine = new GameObject("RaidBeamGlow");
        glowLine.transform.SetParent(transform,false);
        beamGlow = glowLine.AddComponent<LineRenderer>();
        beamGlow.sharedMaterial = beamMaterial;
        beamGlow.useWorldSpace = true;
        beamGlow.positionCount = 2;
        beamGlow.sortingOrder = 59;
        beamGlow.enabled = false;
        var charge = new GameObject("RaidWeaponCharge");
        charge.transform.SetParent(transform,false);
        chargeGlow = charge.AddComponent<SpriteRenderer>();
        chargeGlow.sprite = UiFx.Glow;
        chargeGlow.sortingOrder = 61;
        chargeGlow.enabled = false;
    }

    private void OnDestroy() { if (beamMaterial != null) Destroy(beamMaterial); }

    private void Update()
    {
        if (ship == null || !PhotonNetwork.InRoom) return;
        var room = PhotonNetwork.CurrentRoom;
        bool ended = room.CustomProperties.TryGetValue("ModeResult", out object result) && result is int r && r != 0;
        if (ship.isDead || ship.HasMatchEnded || ended)
        {
            ship.botMove = Vector2.zero; ship.botFire = ship.botSkill = false;
            beam.enabled = false;
            beamGlow.enabled = chargeGlow.enabled = false;
            weaponFx.Hide();
            return;
        }
        if (!room.CustomProperties.TryGetValue("StartTime", out object start) || !(start is double time) || time < 0) return;
        int phase = BossRaidRules.Phase(ship.currentHp, ship.maxHp);
        double now = PhotonNetwork.Time;
        double attackAt = room.CustomProperties.TryGetValue(AttackKey, out object a) && a is double t ? t : -1;
        bool laser = room.CustomProperties.TryGetValue(LaserKey, out object l) && l is bool b && b;
        float angle = room.CustomProperties.TryGetValue(AngleKey, out object v) && v is float f ? f : 0;
        if (attackAt != seenAttack) { seenAttack = attackAt; lastTick = -1; volleyFired = false; }
        float age = (float)(now - attackAt);
        bool attacking = attackAt >= 0 && age < Windup + (laser ? SweepTime : .2f);
        if (PhotonNetwork.IsMasterClient)
        {
            PlayerController target = null;
            float nearest = float.MaxValue;
            foreach (var candidate in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (candidate.isDead || candidate == ship || MatchRules.TeamOf(candidate.CombatantId) != 0) continue;
                float distance = ((Vector2)(candidate.transform.position - transform.position)).sqrMagnitude;
                if (distance < nearest) { nearest = distance; target = candidate; }
            }
            ship.botFire = ship.botSkill = false; // ปิดปืน/สกิลเดิม ใช้รูปแบบโจมตีที่เตือนล่วงหน้าเท่านั้น
            ship.botMove = Vector2.zero;
            if (target != null)
            {
                Vector2 delta = target.transform.position - transform.position;
                ship.botAim = delta.normalized;
                if (!attacking) ship.botMove = delta.normalized * (delta.magnitude > 12 ? 1 : -.25f) * (phase == 3 ? 1f : .65f);
                double gap = phase == 3 ? 1.1 : 2.5;
                if (attackAt < 0 || age > Windup + (laser ? SweepTime : .2f) + gap)
                {
                    float aim = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90;
                    room.SetCustomProperties(new Hashtable {
                        [AttackKey] = now, [AngleKey] = aim,
                        [LaserKey] = phase >= 2 && (!laser || phase == 3)
                    });
                }
            }
        }
        beam.enabled = attacking;
        beamGlow.enabled = chargeGlow.enabled = attacking;
        if (!attacking) { weaponFx.Hide(); return; }
        float sweep = laser ? Mathf.Lerp(-38f, 38f, Mathf.Clamp01((age - Windup) / SweepTime)) : 0;
        Vector2 direction = Quaternion.Euler(0, 0, angle + sweep) * Vector2.up;
        bool warning = age < Windup;
        GetComponent<BossSpectacleFx>()?.Charge(warning ? Mathf.Clamp01(age/Windup) : 1f,!warning);
        beam.startWidth = beam.endWidth = warning ? .18f : laser ? 1.1f : .12f;
        beam.startColor = beam.endColor = warning ? new Color(1f, .65f, .1f, .65f) : new Color(1f,.8f,.55f);
        Vector2 origin = transform.position;
        weaponFx.Draw(origin, direction, age, Windup, laser);
        beam.SetPosition(0, origin);
        beam.SetPosition(1, origin + direction * BeamLength);
        // Visual shell only: hit width, damage ticks and warning duration remain unchanged.
        beamGlow.SetPosition(0,origin);
        beamGlow.SetPosition(1,origin + direction * BeamLength);
        beamGlow.startWidth = beamGlow.endWidth = warning ? .35f : laser ? 1.8f : .24f;
        beamGlow.startColor = beamGlow.endColor = new Color(1,.2f,.03f,warning ? .08f : .18f);
        float chargeSize = warning ? Mathf.Lerp(.4f,1.8f,Mathf.Clamp01(age/Windup)) : laser ? 2f : .7f;
        chargeGlow.transform.localScale = Vector3.one * chargeSize / (chargeGlow.sprite.bounds.size.y * Mathf.Max(.01f,transform.lossyScale.x));
        chargeGlow.color = new Color(1,warning ? .65f : .2f,.05f,.45f + .15f*Mathf.Sin(age*28));
        if (warning) return;
        if (!laser && !volleyFired)
        {
            volleyFired = true;
            if (PhotonNetwork.IsMasterClient)
                for (int i = -2; i <= 2; i++)
                {
                    float shotAngle = angle + i * (phase == 3 ? 16 : 12);
                    Vector2 muzzle = origin + (Vector2)(Quaternion.Euler(0, 0, shotAngle) * Vector2.up) * 2f;
                    BulletController.SpawnLocal(muzzle, shotAngle, ship.attack, ship.CombatantId, true, 0f);
                    ship.photonView.RPC("FireShotRPC", RpcTarget.Others, muzzle, shotAngle, ship.attack);
                }
        }
        int tick = Mathf.FloorToInt((age - Windup) * 4);
        if (!laser || tick == lastTick) return;
        lastTick = tick; // ทุกเครื่องเดินนาฬิกาเดียวกัน จึงไม่ยิงย้อนหลังเมื่อเปลี่ยน Host
        if (!PhotonNetwork.IsMasterClient) return;
        foreach (var target in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (target.isDead || MatchRules.TeamOf(target.CombatantId) != 0) continue;
            Vector2 offset = (Vector2)target.transform.position - origin;
            float along = Vector2.Dot(offset, direction);
            if (along > 0 && along < BeamLength && (offset - direction * along).magnitude < 1.3f)
                PlayerController.SendDamage(target, phase == 3 ? 12 : 8, ship.CombatantId, true);
        }
    }
}
