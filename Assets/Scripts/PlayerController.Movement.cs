using UnityEngine;
using Photon.Pun;

// ส่วน Movement ของ PlayerController; partial คือคลาสเดิม ไม่ต้องเพิ่ม Component
public partial class PlayerController
{
    private void HandleAiming()
    {
        if (fireButton == null || !fireButton.isPressed || !fireButton.HasAim) return;
        Vector2 direction = fireButton.AimDirection;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        // Only the right stick rotates the ship; translation remains controlled by the left stick.
        float facing = Mathf.LerpAngle(transform.eulerAngles.z, angle, 1f - Mathf.Exp(-rotationSpeed * BattleSettingsPanel.AimSensitivity * Time.deltaTime));
        transform.rotation = Quaternion.Euler(0, 0, facing);
    }

    private void HandleMovement()
    {
        if (isStunned) return; // ไม่สามารถขยับได้ตอนติด Stun
        if (joystick != null)
        {
            Vector2 input = Vector2.ClampMagnitude(new Vector2(joystick.GetHorizontal(), joystick.GetVertical()), 1f);
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

    private Vector2 ClampToArena(Vector2 position)
    {
        return new Vector2(
            Mathf.Clamp(position.x, arenaMin.x, arenaMax.x),
            Mathf.Clamp(position.y, arenaMin.y, arenaMax.y));
    }
}
