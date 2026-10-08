# รายการรูป (อัปเดตหลังนำเข้ารูปชุด "รูปเกมที่ทำเพิ่มเอาคะแนน")

วางรูปตามชื่อไฟล์ด้านล่าง แล้วเกมจะใช้รูปเองอัตโนมัติ ไม่ต้องแก้โค้ด
- ตั้งค่ารูปใน Unity: Texture Type = **Sprite (2D and UI)**
- PNG พื้นหลังโปร่งใส สไตล์เดียวกับชุดใหม่ (ภาพวาดไซไฟ ตัดเส้นเข้ม ส่วนเรืองแสง)

## ✅ ใส่แล้ว (จากรูปชุดใหม่)
| ไฟล์ใน Assets/Resources/Images | มาจาก |
|---|---|
| ship4.png / ship5.png / ship6.png | ยานใหม่: เขียว / ขาว-ฟ้า / แดง-ดำ |
| icon_blink.png / icon_heal.png / icon_cloak.png | แผ่นไอคอนสกิล (ประตูวาร์ป / กากบาทเขียว / ยานกับเงา) |
| Items/item_armor, thrust, cannon, loader, capacitor, aegis, siphon, nano | ตัดจากแผ่นชิ้นส่วนยาน (nano = ย้อมเขียว) |
| PowerUps/powerup_boost, overdrive, damage, shield, repair | คริสตัล 4 สี (repair = ย้อมเขียว) |
| PowerUps/star.png | แกนพลังงานสีส้ม (ชั่วคราว) |
| VFX/VFX_Heal, VFX_Warp, VFX_Cloak, VFX_BigExplosion | แผ่นเอฟเฟกต์ |
| Map_AsteroidStation(.png/_Preview) + Maps/Station/* | แม็พ 4 สถานีอวกาศ |
| Map_MoltenNebula(.png/_Preview) + Maps/Lava/* | แม็พ 5 เนบิวลาลาวา (พื้นหลังหรี่แสงลง, หินลาวาตัดพื้นดำออก) |

ยังไม่ได้ใช้: ยานอีก 3 ลำ (ครีม / ส้ม / ฟ้า-เหลือง), ไอคอนสกิลอีก 9 อัน, เอฟเฟกต์พิกเซลอาร์ต — เก็บไว้ในโฟลเดอร์ Downloads เดิม

## ✅ ชุดที่ 2 (โฟลเดอร์ "แรงค์")
| ไฟล์ | รูป |
|---|---|
| Ranks/rank_bronze ... rank_legend (7 ไฟล์) | โล่ทองแดง → เงิน 2 ดาว → ทองมีปีก 3 ดาว → แพลทินัม → เพชร → มาสเตอร์ม่วงมงกุฎ → เลเจนด์ปีกไฟ |
| Items/crate.png | กล่องเสบียงลายเหลืองดำ |
| PowerUps/star.png | ดาวคริสตัลทอง (แทนแกนพลังงานชั่วคราว) |

**รูปชุดเดิมครบทุกรายการแล้ว** ถ้าต้องการเปลี่ยนรูปไหน ให้วางไฟล์ใหม่ทับชื่อเดิมได้เลย

## ✅ รูปการ์ดโหมด (ใส่แล้ว 9 ต.ค.)
อยู่ที่ `Assets/Resources/Images/Modes/` ประกอบจากยาน/พื้นหลังแม็พ/ภาพระเบิดของเกมเอง — อยากเปลี่ยนรูป วางไฟล์ชื่อเดิมทับ / ลบไฟล์ = กลับไปใช้รูปแผนที่ รูปถูกครอปให้เต็มการ์ด ไม่ยืด วางของสำคัญไว้กลางภาพ
| ไฟล์ | การ์ด | สัดส่วน |
|---|---|---|
| mode_story.png | เนื้อเรื่อง (การ์ดใหญ่แนวตั้ง) | 2:3 เช่น 540×840 |
| mode_duel.png | 1 ต่อ 1 | 1:1 เช่น 780×840 |
| mode_team.png | ต่อสู้แบบทีม | 5:3 เช่น 680×400 |
| mode_ffa.png | ตัวต่อตัว | 5:3 เช่น 680×400 |
| mode_survival.png / mode_hill.png / mode_stars.png / mode_royale.png | โหมดพิเศษ 4 การ์ด | 5:2 เช่น 1000×400 |
| mode_practice.png | ฝึกซ้อม | 3:2 เช่น 1240×840 |

## ✅ แม็พปริซึมรุ่น 3 (9 ต.ค. สร้างใหม่ มุมมองจากด้านบน)
| ไฟล์ใน Assets/Resources/Images | รูป |
|---|---|
| Map_PrismNebula.png (2048×2048) / Map_PrismNebula_Preview.png | พื้นหลังเนบิวลาฟ้า-ม่วง / รูปการ์ดแม็พในล็อบบี้ |
| Maps/Prism/pr_core | เสาโอเบลิสก์กลางบนแท่นหิน |
| Maps/Prism/pr_cluster_a / b / c | กองคริสตัลฟ้า / ม่วง-ชมพู / เขียวฟ้า (เล็ก) |
| Maps/Prism/pr_ridge | แนวสันคริสตัลยาว (ริมซ้าย-ขวา) |
| Maps/Prism/pr_prism | ปริซึมสามเหลี่ยมสะท้อนกระสุน |
| Maps/Prism/pr_warp | แท่นวาร์ปวงกลม |
| Maps/Prism/pr_shard_a / b / c | เศษคริสตัลลอย (ภาพประดับ) |
วาดทับได้ แต่ต้องเป็น PNG พื้นใส ขอบชัด เพราะตัวชนของเสากลางและปริซึมสร้างตามรูปร่างภาพ

## ✅ อื่น ๆ ที่ใส่แล้ว (8–9 ต.ค.)
| ไฟล์ | ใช้ที่ไหน |
|---|---|
| Images/VFX/VFX_CloakLoop.png / VFX_CloakReveal.png | สกิลล่องหน (ออร่าระหว่างล่องหน / แสงวาบตอนเริ่มและหลุด) 8 เฟรมเรียงแนวนอน |
| Images/Maps/Jelly/jellyfish.png / energy_orb.png | แม็พแมงกะพรุน (ตัดใหม่ตามรูปร่าง ไม่มีเศษภาพติดขอบ) |
| Assets/Branding/BOS_Icon.png, BOS_IconBackground.png, BOS_IconForeground.png | ไอคอนแอป BOS (ไม่อยู่ใน Resources) |

## ✅ เสียง (9 ต.ค.)
อยู่ที่ `Assets/Resources/Audio/BOS/` — `SFX_*.wav` 21 ไฟล์ และเพลง `BGM_Lobby.ogg`, `BGM_Battle_0.ogg` … `BGM_Battle_4.ogg` (เลขตามลำดับแม็พ 0–4) อยากเปลี่ยนเสียงไหน วางไฟล์ชื่อเดียวกันใน `Assets/Resources/Audio/` (ทับโดยไม่แตะชุดเดิม) หรือทับในโฟลเดอร์ BOS

## ✅ ชุดไอคอน UI (8 ต.ค. วาดด้วยโค้ด ไอคอนเส้นสีขาว 128×128)
อยู่ที่ `Assets/Resources/Images/UI/icon_<ชื่อ>.png` 45 ไฟล์ — อยากได้รูปสวยกว่า วางไฟล์ใหม่ทับชื่อเดิม (PNG พื้นใส สีขาว เกมย้อมสีตามตัวหนังสือเอง)
play, modes, ship, trophy, missions, profile, friends, settings, upgrade, items, shop, skill, hp, atk, spd, invite, copy, ready, leave, back,
time, target, bot, map, hazard, powerup, mode, sound, control, display, language, kill, death, damage, accuracy, streak, shield, star, lock,
chat, mvp, crate, home, rank, quick

