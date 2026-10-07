// ส่วนการบังคับยานของ PlayerController: เล็ง/หมุนยานด้วยปุ่มยิง (สติ๊กขวา) และคำนวณ input การเคลื่อนที่จากจอยซ้าย
// ทุกเมธอดในไฟล์นี้ถูกเรียกจาก Update ใน PlayerController.cs เฉพาะเครื่องเจ้าของยาน (photonView.IsMine)
// การขยับจริงทำใน FixedUpdate (PlayerController.cs) ส่วนเครื่องอื่นเห็นตำแหน่งผ่าน OnPhotonSerializeView
using UnityEngine;
using Photon.Pun;

// ส่วน Movement ของ PlayerController; partial คือคลาสเดิม ไม่ต้องเพิ่ม Component
public partial class PlayerController
{
    // หมุนหัวยานไปทางที่ลากปุ่มยิง (AimDirection) แบบนุ่มนวล ความไวขึ้นกับ rotationSpeed ของยานและ AimSensitivity ในหน้าตั้งค่า
    // รันเฉพาะเจ้าของ; มุมที่หมุนแล้วจะถูกส่งให้อีกเครื่องผ่าน OnPhotonSerializeView
    private void HandleAiming()
    {
        // บอท: หันหัวยานไปทางที่ BotController เล็งไว้
        if (IsBot)
        {
            if (botAim.sqrMagnitude < .0001f) return;
            float botAngle = Mathf.Atan2(botAim.y, botAim.x) * Mathf.Rad2Deg - 90f;
            float botFacing = Mathf.LerpAngle(transform.eulerAngles.z, botAngle, 1f - Mathf.Exp(-rotationSpeed * Time.deltaTime));
            transform.rotation = Quaternion.Euler(0, 0, botFacing);
            return;
        }
        if (fireButton == null || !fireButton.isPressed || !fireButton.HasAim) return;
        Vector2 direction = fireButton.AimDirection;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        // Only the right stick rotates the ship; translation remains controlled by the left stick.
        float facing = Mathf.LerpAngle(transform.eulerAngles.z, angle, 1f - Mathf.Exp(-rotationSpeed * BattleSettingsPanel.AimSensitivity * Time.deltaTime));
        transform.rotation = Quaternion.Euler(0, 0, facing);
    }

    // อ่านจอยซ้ายแล้วค่อยๆ ปรับ movementInput เข้าหาทิศที่กด (เร่ง/หน่วงตาม acceleration) ถ้าไม่กดก็ค่อยๆ หยุด
    // รันเฉพาะเจ้าของ; ถ้าไม่มี Rigidbody2D จะขยับ transform ตรงนี้เลย ไม่งั้น FixedUpdate เป็นคนขยับ
    private void HandleMovement()
    {
        if (isStunned) return; // ไม่สามารถขยับได้ตอนติด Stun
        if (joystick != null || IsBot)
        {
            // บอทใช้ทิศจาก BotController แทนจอย
            Vector2 input = IsBot ? Vector2.ClampMagnitude(botMove, 1f)
                : Vector2.ClampMagnitude(new Vector2(joystick.GetHorizontal(), joystick.GetVertical()), 1f);
            if (input.magnitude > 0.1f)
            {
                // Smooth Acceleration แทนการเปลี่ยน velocity ทันที
                float response = Vector2.Dot(movementInput, input) < 0 ? .55f : .35f;
                movementInput = Vector2.MoveTowards(movementInput, input, Time.deltaTime * acceleration * response);
                
                if (playerRigidbody == null)
                {
                    Vector2 targetPosition = (Vector2)transform.position + movementInput * speed * Time.deltaTime;
                    transform.position = ClampToArena(targetPosition);
                }
                
                // คำนวณองศาการเลี้ยวเพื่อเอียงยาน (Tilt)

                // หมุนยานไปในทิศทางที่เดิน (ใช้ rotationSpeed ต่างกันตามยาน)
                
                // เร่งไฟไอพ่น
                if (thrusterEffect != null)
                {
                    var emission = thrusterEffect.emission;
                    emission.rateOverTime = 50f;
                    var main = thrusterEffect.main;
                    main.startSize = 1.2f;
                    main.startSpeed = 4f;
                }
            }
            else
            {
                // Smooth Deceleration
                movementInput = Vector2.MoveTowards(movementInput, Vector2.zero, Time.deltaTime * acceleration * .75f);
                if (movementInput.magnitude < 0.01f) movementInput = Vector2.zero;
                
                // ค่อยๆ คืนยานกลับมาตรงๆ

                // เบาไฟไอพ่นลงเมื่อจอดนิ่ง
                if (thrusterEffect != null)
                {
                    var emission = thrusterEffect.emission;
                    emission.rateOverTime = 10f;
                    var main = thrusterEffect.main;
                    main.startSize = 0.6f;
                    main.startSpeed = 1.5f;
                }
            }
        }
    }

    // บีบตำแหน่งให้อยู่ในกรอบ arenaMin..arenaMax ของแม็พ (ยานออกนอกแม็พไม่ได้)
    private Vector2 ClampToArena(Vector2 position)
    {
        return new Vector2(
            Mathf.Clamp(position.x, arenaMin.x, arenaMax.x),
            Mathf.Clamp(position.y, arenaMin.y, arenaMax.y));
    }
}
