// BotController.cs — สมองของยานบอท (ทำงานเฉพาะเครื่องที่ควบคุมบอท = Master Client)
// ถูกใส่ให้ยานบอทอัตโนมัติจาก PlayerController.Start เมื่อยานนั้นเป็นบอท
// แนวคิด State Machine: เลือกเป้า → ไล่ / สู้ (ส่ายหลบ) / หนีเมื่อเลือดน้อย / หลบอันตรายในแม็พ
// แล้วเขียนคำสั่งลง PlayerController.botMove / botAim / botFire / botSkill (ระบบเดียวกับที่คนกดจอย)
// ความยาก (PlayerController.BotDifficulty): 0 = เป้าซ้อม (ไม่ยิง ขยับช้า), 1 ง่าย, 2 กลาง, 3 ยาก
using UnityEngine;

public class BotController : MonoBehaviour
{
    private enum State { Chase, Fight, Retreat, Evade }

    private PlayerController ship;
    private PlayerController target;
    private State state = State.Chase;

    // ค่าตามความยาก (ตั้งใน Start)
    private float thinkInterval;    // คิดใหม่ทุกกี่วินาที (ยิ่งน้อยยิ่งตอบสนองไว)
    private float aimError;         // เล็งพลาดได้กี่องศา
    private float leadFactor;       // เล็งดักทางล่วงหน้ามากแค่ไหน (0 = ยิงตรงตัว, 1 = ดักเต็มที่)
    private float skillChance;      // โอกาสใช้สกิลเมื่อสถานการณ์เหมาะ
    private float preferredRange;   // ระยะที่ชอบยืนยิง

    private float nextThink;
    private float strafeSign = 1f;
    private float nextStrafeSwap;
    private float aimOffset;
    private Vector2 lastTargetPos;
    private Vector2 targetVelocity;
    private float lastDamagedAt = -10f;
    private float wanderAngle;

    private void Start()
    {
        ship = GetComponent<PlayerController>();
        switch (ship != null ? ship.BotDifficulty : 2)
        {
            case 0: thinkInterval = .6f; aimError = 0f; leadFactor = 0f; skillChance = 0f; preferredRange = 9f; break;
            case 1: thinkInterval = .45f; aimError = 22f; leadFactor = .2f; skillChance = .25f; preferredRange = 9f; break;
            case 3: thinkInterval = .12f; aimError = 4f; leadFactor = .95f; skillChance = .9f; preferredRange = 7.5f; break;
            default: thinkInterval = .25f; aimError = 11f; leadFactor = .6f; skillChance = .55f; preferredRange = 8f; break;
        }
        wanderAngle = Random.value * 360f;
    }

    // เรียกจาก PlayerController.TakeDamage เมื่อบอทโดนยิง (ใช้ตัดสินใจเปิดโล่ / หันไปหาคนยิง)
    public void NotifyDamaged(int attackerId)
    {
        lastDamagedAt = Time.time;
        var attacker = PlayerController.FindCombatant(attackerId);
        if (attacker != null && attacker != ship && !attacker.isDead && !MatchRules.IsAlly(attacker.CombatantId, ship.CombatantId)) target = attacker;
    }

    private void Update()
    {
        if (ship == null || !ship.photonView.IsMine) return;
        if (ship.isDead || ship.HasMatchEnded)
        {
            ship.botMove = Vector2.zero; ship.botFire = false; ship.botSkill = false;
            return;
        }

        // ติดตามความเร็วเป้า (ใช้เล็งดักทาง)
        if (target != null)
        {
            Vector2 pos = target.transform.position;
            targetVelocity = Vector2.Lerp(targetVelocity, (pos - lastTargetPos) / Mathf.Max(Time.deltaTime, .001f), .2f);
            lastTargetPos = pos;
        }

        if (Time.time >= nextThink)
        {
            nextThink = Time.time + thinkInterval * Random.Range(.8f, 1.2f);
            Think();
        }
        Aim();
    }

    // ===== ตัดสินใจ (ทำเป็นช่วง ๆ ตามความยาก) =====
    private void Think()
    {
        if (target == null || target.isDead || target == ship || MatchRules.IsAlly(target.CombatantId, ship.CombatantId)) target = FindNearestEnemy();
        Vector2 me = ship.transform.position;

        // เป้าซ้อม: ลอยไปมาช้า ๆ ไม่ยิง
        if (ship.BotDifficulty == 0)
        {
            wanderAngle += Random.Range(-40f, 40f);
            Vector2 drift = new Vector2(Mathf.Cos(wanderAngle * Mathf.Deg2Rad), Mathf.Sin(wanderAngle * Mathf.Deg2Rad)) * .35f;
            ship.botMove = AvoidWalls(me, drift);
            ship.botFire = false;
            return;
        }

        float hpRatio = ship.currentHp / Mathf.Max(1f, ship.maxHp);
        bool inHazard = ship.IsSlowed || ship.isEnergyOverloaded;
        // เฟส 7B: เป้าหมายของโหมด (เข้าวงยึดจุด / เก็บดาว / กลับเข้าวง Battle Royale)
        Vector2 objective = default;
        bool hasObjective = GameplayManager.Instance != null && GameplayManager.Instance.TryGetObjective(ship, out objective);
        float enemyDistance = target != null ? Vector2.Distance(me, target.transform.position) : float.MaxValue;
        if (hasObjective && enemyDistance > 9f)
        {
            state = State.Chase;
            Vector2 toObjective = objective - me;
            ship.botMove = AvoidWalls(me, toObjective.sqrMagnitude > 1f ? toObjective.normalized : toObjective);
            ship.botFire = target != null && enemyDistance < 16f && HasLineOfSight(me, target.transform.position)
                && Vector2.Angle(ship.transform.up, AimDirection(me)) < 14f + aimError * .5f;
            return;
        }

        if (target == null)
        {
            state = State.Chase;
            wanderAngle += Random.Range(-30f, 30f);
            ship.botMove = AvoidWalls(me, new Vector2(Mathf.Cos(wanderAngle * Mathf.Deg2Rad), Mathf.Sin(wanderAngle * Mathf.Deg2Rad)) * .6f);
            ship.botFire = false;
            return;
        }

        Vector2 toTarget = (Vector2)target.transform.position - me;
        float distance = toTarget.magnitude;
        Vector2 dir = distance > .01f ? toTarget / distance : Vector2.up;

        // เลือกสถานะ
        if (inHazard) state = State.Evade;
        else if (hpRatio < .3f && ship.BotDifficulty >= 2 && distance < preferredRange + 3f) state = State.Retreat;
        else if (distance > preferredRange + 2.5f) state = State.Chase;
        else state = State.Fight;

        // ส่ายซ้ายขวาเปลี่ยนจังหวะเป็นช่วง ๆ (หลบกระสุน)
        if (Time.time >= nextStrafeSwap)
        {
            nextStrafeSwap = Time.time + Random.Range(1.2f, 2.8f);
            strafeSign = Random.value < .5f ? -1f : 1f;
        }
        Vector2 strafe = new Vector2(-dir.y, dir.x) * strafeSign;

        Vector2 move;
        switch (state)
        {
            case State.Evade:
                // ออกจากบึง/แกนพลังงาน: วิ่งออกจากจุดกลางแม็พแมงกะพรุน หรือออกด้านข้าง
                move = ship.isEnergyOverloaded ? (me.sqrMagnitude > .01f ? me.normalized : Vector2.right) : strafe + dir * .3f;
                break;
            case State.Retreat:
                move = -dir * .8f + strafe * .6f;
                break;
            case State.Chase:
                move = dir + strafe * .25f;
                break;
            default:
                float rangeError = distance - preferredRange;
                move = dir * Mathf.Clamp(rangeError * .35f, -.8f, .8f) + strafe * .85f;
                break;
        }
        ship.botMove = AvoidWalls(me, Vector2.ClampMagnitude(move, 1f));

        // ยิงเมื่อหันเข้าเป้าแล้ว อยู่ในระยะ และไม่มีของบัง
        aimOffset = Random.Range(-aimError, aimError);
        float facingError = Vector2.Angle(ship.transform.up, AimDirection(me));
        ship.botFire = distance < 16f && facingError < 14f + aimError * .5f && HasLineOfSight(me, target.transform.position);

        // ใช้สกิลตามชนิด
        if (ship.currentCooldown <= 0 && Random.value < skillChance)
        {
            bool use = false;
            switch (ship.skillType)
            {
                case 0: use = distance < 9f && ship.botFire; break;                                  // STUN: ใกล้และเล็งอยู่
                case 1: use = Time.time - lastDamagedAt < .6f && hpRatio < .7f; break;              // SHIELD: เพิ่งโดนยิงและเลือดลด
                case 2: use = distance < 4.5f; break;                                                // NOVA: ศัตรูอยู่ใกล้
                case 3: use = distance < 15f && HasLineOfSight(me, target.transform.position); break; // SEEKER: มองเห็นเป้า
                case 4: use = (state == State.Retreat && distance < 6f) || (state == State.Chase && distance > 12f); break; // BLINK: หนี/ไล่
                case 5: use = hpRatio < .5f; break;                                                  // HEAL: เลือดเหลือครึ่ง
                case 6: use = hpRatio < .4f || (distance > 9f && distance < 14f); break;             // CLOAK: หนีหรือเข้าประชิด
            }
            if (use) ship.botSkill = true;
        }
    }

    // ===== เล็ง (ทำทุกเฟรมให้หันลื่น) =====
    private void Aim()
    {
        if (target == null || ship.BotDifficulty == 0) { ship.botAim = ship.botMove; return; }
        ship.botAim = AimDirection(ship.transform.position);
    }

    private Vector2 AimDirection(Vector2 me)
    {
        if (target == null) return ship.transform.up;
        Vector2 targetPos = target.transform.position;
        float travelTime = Vector2.Distance(me, targetPos) / 18f;
        Vector2 predicted = targetPos + targetVelocity * travelTime * leadFactor;
        Vector2 dir = (predicted - me);
        if (dir.sqrMagnitude < .0001f) return ship.transform.up;
        return Quaternion.Euler(0, 0, aimOffset) * dir.normalized;
    }

    // ===== ตัวช่วย =====
    private PlayerController FindNearestEnemy()
    {
        PlayerController best = null;
        float bestDistance = float.MaxValue;
        foreach (var other in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (other == ship || other.isDead || other.IsCloaked || MatchRules.IsAlly(other.CombatantId, ship.CombatantId)) continue;
            float d = ((Vector2)other.transform.position - (Vector2)ship.transform.position).sqrMagnitude;
            if (d < bestDistance) { bestDistance = d; best = other; }
        }
        return best;
    }

    // มีสิ่งกีดขวางทึบ (ไม่ใช่ Trigger, ไม่ใช่ยาน/กระสุน) อยู่ระหว่างบอทกับเป้าไหม
    private bool HasLineOfSight(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        foreach (var hit in Physics2D.RaycastAll(from, delta.normalized, delta.magnitude))
        {
            if (hit.collider == null || hit.collider.isTrigger) continue;
            if (hit.collider.GetComponentInParent<PlayerController>() != null) continue;
            if (hit.collider.GetComponentInParent<BulletController>() != null || hit.collider.GetComponentInParent<SkillController>() != null) continue;
            return false;
        }
        return true;
    }

    // ถ้าทิศที่จะไปมีกำแพง/สิ่งกีดขวางใกล้ ๆ ให้เบี่ยงไปทางที่ว่าง (ลองหมุน ±45° และ ±90°)
    private Vector2 AvoidWalls(Vector2 me, Vector2 move)
    {
        if (move.sqrMagnitude < .0001f) return move;
        if (IsClear(me, move)) return move;
        foreach (float angle in new[] { 45f, -45f, 90f, -90f, 135f, -135f })
        {
            Vector2 alt = Quaternion.Euler(0, 0, angle * strafeSign) * move;
            if (IsClear(me, alt)) return alt;
        }
        return -move;
    }

    private bool IsClear(Vector2 me, Vector2 direction)
    {
        float probe = 2.2f;
        foreach (var hit in Physics2D.CircleCastAll(me, .9f, direction.normalized, probe))
        {
            if (hit.collider == null || hit.collider.isTrigger) continue;
            if (hit.collider.transform.IsChildOf(ship.transform)) continue;
            if (hit.collider.GetComponentInParent<PlayerController>() != null) continue;
            if (hit.collider.GetComponentInParent<BulletController>() != null || hit.collider.GetComponentInParent<SkillController>() != null) continue;
            return false;
        }
        // ขอบสนาม
        Vector2 ahead = me + direction.normalized * probe;
        int map = GameplayManager.GetCurrentMapIndex();
        Vector2 min = GameplayManager.GetArenaMin(map), max = GameplayManager.GetArenaMax(map);
        return ahead.x > min.x + 1f && ahead.x < max.x - 1f && ahead.y > min.y + 1f && ahead.y < max.y - 1f;
    }
}
