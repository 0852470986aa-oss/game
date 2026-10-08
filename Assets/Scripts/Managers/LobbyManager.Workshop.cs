// LobbyManager.Workshop.cs — หน้าโรงซ่อม WORKSHOP ในล็อบบี้ (เฟส 5) (partial class ของ LobbyManager)
// 3 แท็บ: UPGRADE (ตีบวก HP/SPD/ATK แบบ SAFE หรือ CHANCE), ITEMS (ใส่/ถอด/ตีบวกไอเท็ม), SHOP (ซื้อไอเท็ม/เปิดกล่อง)
// เลือกยานได้ด้วยปุ่ม < > (เฉพาะยานที่มี) — ข้อมูลและกติกาอยู่ใน Economy.cs ไฟล์นี้ทำแค่หน้าจอ
// รูปไอคอนไอเท็มโหลดจาก Resources/Images/Items/item_*.png ถ้ายังไม่มีรูปจะแสดงกล่องสีตามความหายาก + ตัวย่อ
// ปุ่ม UPGRADES ในห้องรอ (Host เปิด/ปิด) อยู่ท้ายไฟล์
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;

// ส่วนหน้าจอ WORKSHOP ของ LobbyManager: ตีบวกค่าพลังยาน, จัดการไอเท็ม, ร้านค้า และปุ่ม UPGRADES ในห้องรอ
public partial class LobbyManager
{
    private Button workshopButton;
    private Image workshopOverlay;
    private RectTransform workshopWindow, workshopPage;
    // แท็บของหน้า WORKSHOP: ตีบวก / ไอเท็ม / ร้านค้า
    private enum WorkshopTab { Upgrade, Items, Shop }
    private WorkshopTab workshopTab;
    private int workshopShip;
    private int workshopItemPage;
    private bool workshopBusy;
    private string workshopMessage = "";
    private long workshopCoins = -1;
    private bool economySubscribed;

    // ปุ่ม WORKSHOP บนหน้าหลัก + หน้าต่างลอย (เรียกจาก BuildHomeScreen)
    private void BuildWorkshopUI(RectTransform root)
    {
        workshopButton = UIButton("Workshop", root, "WORKSHOP", 255, 315, 190, 50, OpenWorkshop);
        workshopOverlay = UIPanel("WorkshopOverlay", root, 0, 0, 1280, 720, new Color(.01f, .015f, .03f, .96f)); // ทึบเกือบเต็ม ไม่ให้หน้าโรงเก็บยานด้านหลังโผล่ซ้อน
        workshopOverlay.raycastTarget = true;
        workshopWindow = UIPanel("WorkshopWindow", workshopOverlay.transform, 0, 0, 1100, 640, panelColor).rectTransform;
        workshopOverlay.gameObject.SetActive(false);
        if (!economySubscribed && Application.isPlaying) { Economy.Changed += OnEconomyChanged; economySubscribed = true; }
        RefreshWorkshopButton();
    }

    // แสดงปุ่ม WORKSHOP เฉพาะเมื่อเปิดระบบตีบวกหรือระบบไอเท็มอย่างน้อยหนึ่งอย่าง
    private void RefreshWorkshopButton()
    {
        if (workshopButton != null) workshopButton.gameObject.SetActive(FeatureFlags.Upgrades || FeatureFlags.Items);
    }

    // เรียกเมื่อ Economy.Changed: วาดหน้า WORKSHOP ใหม่ถ้ากำลังเปิดอยู่
    private void OnEconomyChanged()
    {
        if (this == null) { Economy.Changed -= OnEconomyChanged; return; }
        if (workshopOverlay != null && workshopOverlay.gameObject.activeSelf) BuildWorkshopPage();
        RefreshUpgradedStats(); // ค่าพลังหน้าหลัก/โรงเก็บยาน/ห้องรอ รวมตีบวกใหม่ทันที
    }

    // เปิดหน้า WORKSHOP: โหลดข้อมูล Economy เริ่มที่ยานที่ใส่อยู่ ดึงยอดเหรียญจาก Firebase แล้ววาดหน้า
    private void OpenWorkshop()
    {
        if (workshopOverlay == null) return;
        Economy.EnsureLoaded();
        workshopShip = equippedShipIndex;
        workshopMessage = "";
        workshopOverlay.gameObject.SetActive(true);
        workshopOverlay.transform.SetAsLastSibling();
        if (workshopEmbedded) EmbeddedWorkshopStyle(); else SolidOverlay(workshopOverlay, workshopWindow);
        if (FirebaseManager.Instance != null)
            FirebaseManager.Instance.GetCoinBalance(coins => { if (this == null) return; workshopCoins = coins; UpdateCoinDisplay(coins); BuildWorkshopPage(); });
        BuildWorkshopPage();
    }

    // ปิดหน้า WORKSHOP (หรือออกจากโหมดฝังในโรงเก็บยาน) แล้วแสดงยานที่ใส่อยู่บนหน้าหลักอีกครั้ง
    private void CloseWorkshop()
    {
        if (CloseEmbeddedWorkshop()) return;
        if (workshopOverlay != null) workshopOverlay.gameObject.SetActive(false);
        UpdateShipDisplay(equippedShipIndex);
    }

    // ผลจากการกระทำ: แสดงข้อความ อัปเดตเหรียญ แล้ววาดหน้าใหม่
    private void WorkshopResult(string message, long balance)
    {
        if (this == null) return;
        workshopBusy = false;
        workshopMessage = message;
        if (balance >= 0) { workshopCoins = balance; UpdateCoinDisplay(balance); }
        BuildWorkshopPage();
    }

    // วาดหน้า WORKSHOP ใหม่ทั้งหน้า: หัวข้อ/เหรียญ แท็บ ตัวเลือกยาน เนื้อหาแท็บ และข้อความผลลัพธ์
    private void BuildWorkshopPage()
    {
        if (workshopWindow == null) return;
        if (workshopPage != null) Destroy(workshopPage.gameObject);
        workshopPage = UIRect("WPage" + (++pageSerial), workshopWindow, 0, 0, 1100, 640);
        var page = workshopPage;
        // ฝังในโรงเก็บยาน: หัวข้อ/เหรียญ/ปุ่มกลับ/แท็บ ใช้ของโรงเก็บยานแทน (LobbyManager.Polish.cs)
        if (!workshopEmbedded)
        {
            UILabel("Title", page, "WORKSHOP", -400, 286, 260, 44, 30, Color.white);
            UILabel("Coins", page, workshopCoins >= 0 ? "ASTRONIUM  " + workshopCoins.ToString("N0") : "ASTRONIUM  ...", 120, 286, 360, 36, 20, new Color(1f, .8f, .35f));
            UIButton("Close", page, "BACK", 450, 286, 170, 54, CloseWorkshop);
        }
        // แท็บ
        string[] tabs = { "SHIP UPGRADE", "ITEMS", "SHOP" };
        for (int i = 0; i < tabs.Length && !workshopEmbedded; i++)
        {
            int tab = i;
            bool enabled = i == 0 ? FeatureFlags.Upgrades : FeatureFlags.Items;
            var button = UIButton("Tab" + i, page, tabs[i], -380 + i * 200, 220, 190, 54, () => { workshopTab = (WorkshopTab)tab; workshopMessage = ""; pendingSellUid = null; BuildWorkshopPage(); });
            StyleTab(button, (int)workshopTab == i, enabled); // แท็บที่เลือกเป็นสีสว่าง (LobbyManager.Polish.cs)
        }
        if (workshopTab == WorkshopTab.Upgrade && !FeatureFlags.Upgrades) workshopTab = WorkshopTab.Items;
        // เลือกยาน (ใช้กับแท็บ UPGRADE / ITEMS)
        if (workshopTab != WorkshopTab.Shop) itemCatalogOpen = false; // ออกจากร้านค้า = ปิดรายการไอเท็มทั้งหมด
        // แท็บไอเท็มแบบใหม่มีการ์ดยาน (รูป + ปุ่มเลือก) ข้างช่องใส่ไอเท็มแทน (ItemsShipCard)
        if (workshopTab == WorkshopTab.Upgrade || (workshopTab == WorkshopTab.Items && !FeatureFlags.ItemsShipCard))
        {
            UIButton("PrevShip", page, "<", 160, 232, 50, 44, () => CycleWorkshopShip(-1));
            UILabel("ShipName", page, ships[workshopShip].name, 330, 232, 280, 40, 20, accentColor);
            UIButton("NextShip", page, ">", 500, 232, 50, 44, () => CycleWorkshopShip(1));
        }
        if (workshopTab == WorkshopTab.Upgrade) BuildUpgradeTab(page);
        else if (workshopTab == WorkshopTab.Items) BuildItemsTab(page);
        else BuildShopTab(page);
        var message = UILabel("Message", page, workshopBusy ? "Processing..." : workshopMessage, 0, workshopEmbedded ? -262 : -296, 1040, 30, 18, new Color(1f, .85f, .5f));
        message.richText = false;
    }

    // เลื่อนไปยานลำก่อนหน้า/ถัดไป (direction = -1/+1) ข้ามยานที่ยังไม่ปลดล็อก แล้ววาดหน้าใหม่
    private void CycleWorkshopShip(int direction)
    {
        for (int step = 1; step <= ships.Length; step++)
        {
            int next = (workshopShip + direction * step + ships.Length * 4) % ships.Length;
            if (unlockedShips == null || unlockedShips.Contains(next)) { workshopShip = next; break; }
        }
        workshopItemPage = 0;
        workshopMessage = "";
        BuildWorkshopPage();
    }

    // ===== แท็บตีบวก =====
    private void BuildUpgradeTab(RectTransform page)
    {
        int ship = workshopShip;
        var state = Economy.ShipState(ship);
        int cap = Economy.UpgradeCap(ship);
        UILabel("CapInfo", page, "Upgrade limit for this ship: +" + cap + "   (cheap ships cap lower, expensive ships cap higher)", 0, 178, 1040, 28, 16, Color.gray);
        float[] baseValues = { ships[ship].hp, ships[ship].spd, ships[ship].atk };
        for (int i = 0; i < 3; i++)
        {
            var stat = (Economy.Stat)i;
            int level = Economy.StatLevel(state, stat);
            float y = 110 - i * 92;
            var row = UIPanel("Stat" + i, page, 0, y, 1040, 80, new Color(.06f, .1f, .17f));
            UILabel("Name", row.transform, Economy.StatNames[i], -400, 16, 220, 30, 20, Color.white);
            // แถบเลเวล
            for (int pip = 0; pip < cap; pip++)
                UIPanel("Pip" + pip, row.transform, -470 + pip * 16, -18, 12, 14, pip < level ? accentColor : new Color(.15f, .2f, .28f));
            float bonus = level * Economy.StatPerLevel[i];
            UILabel("Value", row.transform, "+" + level + "   " + baseValues[i].ToString("0.#") + " -> " + (baseValues[i] * (1 + bonus)).ToString("0.#")
                + "  (+" + Mathf.RoundToInt(bonus * 100) + "%)", -90, 0, 330, 40, 18, Color.white);
            bool maxed = level >= cap;
            var safe = UIButton("Safe", row.transform, maxed ? "MAX" : "SAFE  " + Economy.UpgradeCost(level, true), 230, 0, 190, 54,
                () => DoUpgrade(stat, true));
            var chance = UIButton("Chance", row.transform, maxed ? "MAX" : "CHANCE " + Economy.UpgradeChance(level) + "%  " + Economy.UpgradeCost(level, false),
                430, 0, 190, 54, () => DoUpgrade(stat, false));
            safe.interactable = chance.interactable = !maxed && !workshopBusy;
        }
        // สรุปค่าพลังรวม (ตีบวก + ไอเท็ม)
        var total = Economy.BuildBonus(ship);
        UILabel("Total", page, "IN BATTLE (upgrades + items):  HP " + Mathf.Round(ships[ship].hp * (1 + total.hp)) + "   ATK "
            + (ships[ship].atk * (1 + total.atk)).ToString("0.#") + "   SPD " + (ships[ship].spd * (1 + total.spd)).ToString("0.#")
            + (total.fireRate > 0 ? "   FIRE RATE +" + Mathf.RoundToInt(total.fireRate * 100) + "%" : "")
            + (total.cooldown > 0 ? "   SKILL CD -" + Mathf.RoundToInt(total.cooldown * 100) + "%" : ""), 0, -180, 1040, 30, 17, accentColor);
        UILabel("Rules", page, "SAFE = always succeeds.   CHANCE = half price, may fail (coins spent, level is never lost).   Host can turn upgrades off per room.",
            0, -222, 1040, 28, 14, Color.gray);
    }

    // กดตีบวกค่าพลัง (safe = SAFE จ่ายเต็มสำเร็จแน่, false = CHANCE ลุ้น) ล็อกปุ่มระหว่างรอผลจาก Economy
    private void DoUpgrade(Economy.Stat stat, bool safe)
    {
        if (workshopBusy) return;
        workshopBusy = true;
        BuildWorkshopPage();
        Economy.UpgradeStat(workshopShip, stat, safe, (paid, success, message, balance) => WorkshopResult(message, balance));
    }

    // ===== แท็บไอเท็ม =====
    private void BuildItemsTab(RectTransform page)
    {
        int ship = workshopShip;
        var state = Economy.ShipState(ship);
        int slots = Economy.SlotCount(ship);
        UILabel("SlotHead", page, Lang.T("SHIP SLOTS") + " (" + state.slots.Count + "/" + slots + ")  " + Lang.T("tap to unequip"), -300, 178, 440, 28, 16, accentColor);
        for (int i = 0; i < 3; i++)
        {
            float x = -440 + i * 190;
            bool open = i < slots;
            var owned = open && i < state.slots.Count ? Economy.FindOwned(state.slots[i]) : null;
            var box = UIPanel("Slot" + i, page, x, 118, 176, 84, open ? new Color(.08f, .14f, .22f) : new Color(.04f, .05f, .08f));
            if (!open) { UILabel("Lock", box.transform, "LOCKED\n(better ship)", 0, 0, 170, 70, 14, Color.gray).textWrappingMode = TextWrappingModes.Normal; continue; }
            if (owned == null) { UILabel("Empty", box.transform, "EMPTY", 0, 0, 170, 70, 16, Color.gray); continue; }
            ItemVisual(box.transform, owned.item, owned.level, -55, 0, 56);
            var def = Economy.FindItem(owned.item);
            UILabel("Name", box.transform, (def != null ? def.name : owned.item) + " +" + owned.level, 30, 0, 110, 70, 14, Color.white).textWrappingMode = TextWrappingModes.Normal;
            var button = box.gameObject.AddComponent<Button>();
            box.raycastTarget = true;
            button.targetGraphic = box;
            var item = owned;
            button.onClick.AddListener(() => { workshopMessage = Economy.ToggleEquip(ship, item); BuildWorkshopPage(); });
        }
        if (FeatureFlags.ItemsShipCard) BuildItemsShipCard(page, ship);
        // คลังไอเท็ม 8 ชิ้นต่อหน้า (4 x 2)
        var items = new List<OwnedItem>(Economy.Data.items);
        if (FeatureFlags.ItemCatalog) items.Sort(CompareRarestFirst); // หายากสุดขึ้นก่อน แล้วเลเวลสูงก่อน
        else items.Sort((a, b) => b.level != a.level ? b.level.CompareTo(a.level) : string.CompareOrdinal(a.item, b.item));
        int pages = Mathf.Max(1, (items.Count + 7) / 8);
        workshopItemPage = Mathf.Clamp(workshopItemPage, 0, pages - 1);
        UILabel("InvHead", page, Lang.T("YOUR ITEMS") + " (" + items.Count + ")", -400, 56, 240, 28, 16, accentColor);
        if (items.Count == 0) UILabel("None", page, "No items yet. Buy some in the SHOP tab or open a Supply Crate.", 0, -40, 900, 40, 18, Color.gray);
        for (int i = 0; i < 8; i++)
        {
            int index = workshopItemPage * 8 + i;
            if (index >= items.Count) break;
            var owned = items[index];
            var def = Economy.FindItem(owned.item);
            if (def == null) continue;
            float x = -390 + (i % 4) * 260, y = -12 - (i / 4) * 114; // เลื่อนลงไม่ให้ทับหัวข้อ YOUR ITEMS
            var cell = UIPanel("Item" + i, page, x, y, 250, 104, new Color(.06f, .1f, .17f));
            ItemVisual(cell.transform, def.id, owned.level, -88, 14, 56);
            bool canSell = FeatureFlags.SellItems;
            var name = UILabel("Name", cell.transform, def.name + " +" + owned.level, canSell ? 2 : 30, 30, canSell ? 124 : 180, 26, 15, Economy.RarityColors[(int)def.rarity]);
            name.richText = false;
            UILabel("Effect", cell.transform, def.Describe(owned.level), 30, 6, 180, 24, 12, Color.white);
            int on = Economy.EquippedOn(owned.uid);
            var item = owned;
            var equip = UIButton("Equip", cell.transform, on == ship ? "UNEQUIP" : on >= 0 ? "MOVE TO SHIP" : "EQUIP", -62, -30, 112, 44,
                () => { workshopMessage = Economy.ToggleEquip(ship, item); BuildWorkshopPage(); });
            var upgrade = UIButton("Up", cell.transform, owned.level >= Economy.ItemMaxLevel ? "MAX" : "+ " + Economy.ItemUpgradeCost(owned) + " (" + Economy.ItemUpgradeChance(owned.level) + "%)",
                62, -30, 112, 44, () => DoItemUpgrade(item));
            upgrade.interactable = owned.level < Economy.ItemMaxLevel && !workshopBusy;
            equip.interactable = !workshopBusy;
            if (canSell)
            {
                // ปุ่ม SELL มุมขวาบนของการ์ด: กดครั้งแรก = ถามยืนยัน (ปุ่มเปลี่ยนเป็นราคา) กดอีกครั้ง = ขาย
                bool confirming = pendingSellUid == owned.uid;
                var sell = UIButton("Sell", cell.transform, confirming ? "+" + Economy.SellPrice(owned) : "SELL", 92, 30, 62, 30, () => DoSellItem(item));
                sell.GetComponent<Image>().color = confirming ? new Color(.85f, .55f, .15f) : new Color(.35f, .18f, .18f);
                sell.interactable = !workshopBusy;
            }
        }
        if (pages > 1)
        {
            // ตัวเลื่อนหน้าอยู่แถวเดียวกับหัวข้อช่องใส่ไอเท็ม (เดิมทับแถวการ์ดแถวแรก)
            // มีการ์ดยาน (ItemsShipCard) = ย้ายขึ้นไปเหนือการ์ดยาน (ที่ว่างเดิมของปุ่มเลือกยาน) ไม่ทับการ์ดยาน/การ์ดไอเท็ม
            float pagerY = FeatureFlags.ItemsShipCard ? 228 : 178, pagerH = 40;
            UIButton("PrevPage", page, "<", 360, pagerY, 54, pagerH, () => { workshopItemPage--; BuildWorkshopPage(); });
            UILabel("PageNo", page, (workshopItemPage + 1) + "/" + pages, 430, pagerY, 80, 32, 16, Color.white);
            UIButton("NextPage", page, ">", 500, pagerY, 54, pagerH, () => { workshopItemPage++; BuildWorkshopPage(); });
        }
    }

    // การ์ดยานในแท็บไอเท็ม (ที่ว่างขวาของช่องใส่ไอเท็ม): ปุ่ม < > + รูปยาน + ชื่อ จะได้รู้ว่ากำลังใส่ไอเท็มให้ลำไหน
    private void BuildItemsShipCard(RectTransform page, int ship)
    {
        var card = UIPanel("ShipCard", page, 300, 114, 470, 92, new Color(.08f, .14f, .22f));
        UIButton("PrevShip", card.transform, "<", -205, 0, 46, 70, () => CycleWorkshopShip(-1));
        var art = UIPanel("ShipArt", card.transform, -110, 0, 120, 84, Color.white);
        art.sprite = BattleLoadoutCatalog.ShipSprite(ship);
        art.color = art.sprite != null ? BattleLoadoutCatalog.ShipTint(ship) : Color.clear;
        art.preserveAspect = true;
        art.raycastTarget = false;
        var name = UILabel("ShipName", card.transform, ships[ship].name, 62, 14, 230, 32, 20, accentColor);
        name.richText = false;
        if (ship == equippedShipIndex) UILabel("InUse", card.transform, "EQUIPPED", 62, -20, 230, 24, 14, new Color(.45f, 1f, .7f));
        UIButton("NextShip", card.transform, ">", 205, 0, 46, 70, () => CycleWorkshopShip(1));
    }

    // เรียงไอเท็ม: EPIC > RARE > COMMON แล้วเลเวลสูงก่อน แล้วชื่อ
    private static int CompareRarestFirst(OwnedItem a, OwnedItem b)
    {
        var da = Economy.FindItem(a.item);
        var db = Economy.FindItem(b.item);
        int ra = da != null ? (int)da.rarity : -1, rb = db != null ? (int)db.rarity : -1;
        if (ra != rb) return rb.CompareTo(ra);
        if (a.level != b.level) return b.level.CompareTo(a.level);
        return string.CompareOrdinal(a.item, b.item);
    }

    // ===== รายการไอเท็มทั้งหมด (ปุ่มในร้านค้า, FeatureFlags.ItemCatalog) =====
    private bool itemCatalogOpen;
    // ปุ่มเปิดรายการ (ใต้ร้านรายวัน เหนือกล่องเสบียง ชิดซ้าย)
    private void BuildCatalogButton(RectTransform page)
    {
        var open = UIButton("AllItems", page, "ALL ITEMS", -430, -56, 200, 36, () => { itemCatalogOpen = true; BuildWorkshopPage(); });
        UiIcon.Attach(open.GetComponentInChildren<TMP_Text>(), "items", .8f);
    }
    // หน้าต่างรายการไอเท็มทั้ง 8 ชนิด เรียงจากหายากสุด (EPIC -> RARE -> COMMON) 2 คอลัมน์ บอกผลที่ +1 และ +5
    private void BuildItemCatalog(RectTransform page)
    {
        var window = UIPanel("ItemCatalog", page, 0, 10, 1060, 470, new Color(.03f, .05f, .09f, .98f));
        window.raycastTarget = true; // กันกดทะลุไปโดนปุ่มร้านค้าด้านหลัง
        UILabel("Head", window.transform, "ALL ITEMS (rarest first)", -250, 206, 500, 34, 20, accentColor);
        UIButton("Close", window.transform, "CLOSE", 440, 206, 150, 40, () => { itemCatalogOpen = false; BuildWorkshopPage(); });
        var list = new List<Economy.ItemDef>(Economy.Items);
        list.Sort((a, b) => a.rarity != b.rarity ? ((int)b.rarity).CompareTo((int)a.rarity) : string.CompareOrdinal(a.name, b.name));
        for (int i = 0; i < list.Count && i < 8; i++)
        {
            var def = list[i];
            Color rarity = Economy.RarityColors[(int)def.rarity];
            float x = i % 2 == 0 ? -262 : 262, y = 140 - (i / 2) * 96;
            var row = UIPanel("Item" + i, window.transform, x, y, 516, 88, new Color(.06f, .1f, .17f));
            UIPanel("RarityBar", row.transform, -255, 0, 6, 88, rarity);
            ItemVisual(row.transform, def.id, 1, -205, 0, 70);
            // ข้อความชิดซ้ายถัดจากรูป: ชื่อ (สีความหายาก) / ผลที่ +1 / ผลที่ +5 (สีทอง) และป้ายความหายากชิดขวา
            var name = UILabel("Name", row.transform, def.name, -30, 24, 250, 28, 17, rarity);
            name.richText = false;
            name.alignment = TextAlignmentOptions.Left;
            UILabel("Rarity", row.transform, Economy.RarityNames[(int)def.rarity], 185, 24, 130, 26, 14, rarity).alignment = TextAlignmentOptions.Right;
            UILabel("Lv1", row.transform, def.Describe(1) + " (+1)", 45, -4, 400, 24, 13, Color.white).alignment = TextAlignmentOptions.Left;
            UILabel("LvMax", row.transform, def.Describe(Economy.ItemMaxLevel) + " (+" + Economy.ItemMaxLevel + ")", 45, -28, 400, 24, 13,
                new Color(1f, .82f, .35f)).alignment = TextAlignmentOptions.Left;
        }
    }

    // ขายไอเท็ม: กดครั้งแรกถามยืนยัน (บอกราคา) กดซ้ำชิ้นเดิมจึงขายจริง
    private string pendingSellUid;
    // กดปุ่ม SELL ของไอเท็ม 1 ชิ้น
    private void DoSellItem(OwnedItem item)
    {
        if (workshopBusy || item == null) return;
        var def = Economy.FindItem(item.item);
        string name = (def != null ? def.name : item.item) + " +" + item.level;
        if (pendingSellUid != item.uid)
        {
            pendingSellUid = item.uid;
            workshopMessage = "Tap again to sell " + name + " for " + Economy.SellPrice(item) + " Astronium"
                + (Economy.EquippedOn(item.uid) >= 0 ? " (it will be unequipped)." : ".");
            BuildWorkshopPage();
            return;
        }
        pendingSellUid = null;
        workshopBusy = true;
        BuildWorkshopPage();
        Economy.SellItem(item, (ok, message, balance) =>
        {
            WorkshopResult(message, balance);
            if (ok && balance < 0) StartCoroutine(RefreshCoinsLater()); // ไม่รู้ยอดใหม่: อ่านเหรียญอีกครั้ง
        });
    }

    // กดตีบวกไอเท็ม 1 ชิ้น (มีโอกาสสำเร็จตามเลเวล) ล็อกปุ่มระหว่างรอผลจาก Economy
    private void DoItemUpgrade(OwnedItem item)
    {
        if (workshopBusy) return;
        workshopBusy = true;
        BuildWorkshopPage();
        Economy.UpgradeItem(item, (paid, success, message, balance) => WorkshopResult(message, balance));
    }

    // ===== แท็บร้านค้า =====
    private void BuildShopTab(RectTransform page)
    {
        if (FeatureFlags.DailyShop) BuildDailyShop(page);
        else BuildFixedShop(page);
        BuildCrateOffer(page);
        if (FeatureFlags.ItemCatalog)
        {
            BuildCatalogButton(page);
            if (itemCatalogOpen) BuildItemCatalog(page); // วาดทีหลังสุด = ทับร้านค้า
        }
        else itemCatalogOpen = false;
    }

    // ร้านค้ารายวัน: 4 ช่องสุ่มวันละครั้ง (Economy.DailyItems) ช่องละ 1 ชิ้นต่อวัน + นับถอยหลังเวลารีเซ็ต
    private TMP_Text shopTimerLabel;
    private bool shopTimerRunning;
    // วาดร้านรายวัน: หัวข้อ + เวลานับถอยหลัง + การ์ด 4 ช่อง + บรรทัดโอกาสออก
    private void BuildDailyShop(RectTransform page)
    {
        UILabel("ShopHead", page, "DAILY SHOP  (new items every day, 1 of each)", -230, 178, 620, 28, 16, accentColor);
        shopTimerLabel = UILabel("ShopTimer", page, "", 330, 178, 380, 28, 16, new Color(1f, .82f, .35f));
        UiIcon.Attach(shopTimerLabel, "time", .8f);
        UpdateShopTimer();
        if (!shopTimerRunning) StartCoroutine(ShopTimerTick());
        var offers = Economy.DailyItems();
        for (int i = 0; i < offers.Length; i++)
        {
            var def = offers[i];
            int slot = i;
            bool bought = Economy.DailyBought(i);
            int price = Economy.DailyPrice(def.rarity);
            Color rarityColor = Economy.RarityColors[(int)def.rarity];
            float x = -390 + i * 260;
            var cell = UIPanel("Daily" + i, page, x, 70, 250, 150, bought ? new Color(.04f, .06f, .1f) : new Color(.06f, .1f, .17f));
            UIPanel("RarityBar", cell.transform, 0, 72, 250, 6, rarityColor); // แถบสีความหายากด้านบนการ์ด
            UILabel("Rarity", cell.transform, Economy.RarityNames[(int)def.rarity], 0, 54, 230, 22, 14, rarityColor);
            ItemVisual(cell.transform, def.id, 1, -88, 8, 64);
            var name = UILabel("Name", cell.transform, def.name, 30, 22, 180, 26, 16, Color.white);
            name.richText = false;
            UILabel("Effect", cell.transform, def.Describe(1) + " (+1)", 30, -4, 180, 24, 12, Color.white);
            var buy = UIButton("Buy", cell.transform, bought ? "SOLD OUT" : "BUY  " + price.ToString("N0"), 0, -46, 226, 40, () => DoBuyDaily(slot));
            buy.interactable = !bought && !workshopBusy;
            if (!bought) buy.GetComponent<Image>().color = def.rarity == Economy.Rarity.Epic ? new Color(.45f, .2f, .6f)
                : def.rarity == Economy.Rarity.Rare ? new Color(.15f, .35f, .65f) : new Color(.18f, .3f, .4f);
        }
        UILabel("DailyOdds", page, "Each slot: COMMON " + Economy.DailyRarityOdds[0] + "%  /  RARE " + Economy.DailyRarityOdds[1]
            + "%  /  EPIC " + Economy.DailyRarityOdds[2] + "%", 0, -22, 900, 22, 13, Color.gray);
    }

    // ข้อความนับถอยหลังร้านรีเซ็ต
    private void UpdateShopTimer()
    {
        if (shopTimerLabel == null) return;
        var left = Economy.ShopResetIn;
        shopTimerLabel.text = "NEW ITEMS IN " + ((int)left.TotalHours).ToString("00") + ":" + left.Minutes.ToString("00") + ":" + left.Seconds.ToString("00");
    }

    // Coroutine อัปเดตเวลานับถอยหลังทุก 1 วิ ขณะหน้าร้านเปิดอยู่ / ข้ามเที่ยงคืน = วาดร้านใหม่ (ของชุดใหม่)
    private System.Collections.IEnumerator ShopTimerTick()
    {
        shopTimerRunning = true;
        string day = Economy.ShopDay;
        var wait = new WaitForSecondsRealtime(1f);
        while (this != null && shopTimerLabel != null)
        {
            UpdateShopTimer();
            if (Economy.ShopDay != day) { day = Economy.ShopDay; BuildWorkshopPage(); }
            yield return wait;
        }
        shopTimerRunning = false;
    }

    // กดซื้อไอเท็มร้านรายวัน (ล็อกปุ่มระหว่างรอผล)
    private void DoBuyDaily(int slot)
    {
        if (workshopBusy) return;
        workshopBusy = true;
        BuildWorkshopPage();
        Economy.BuyDaily(slot, (ok, message, balance) => WorkshopResult(message, balance));
    }

    // ร้านแบบเดิม (ปิด DailyShop): ขายทุกชิ้น COMMON/RARE ราคาปกติ
    private void BuildFixedShop(RectTransform page)
    {
        UILabel("ShopHead", page, "ITEM SHOP  (EPIC items only from Supply Crates)", -220, 178, 640, 28, 16, accentColor);
        int shown = 0;
        foreach (var def in Economy.Items)
        {
            int price = Economy.ShopPrice(def.rarity);
            if (price <= 0) continue;
            float x = -390 + (shown % 4) * 260, y = 110 - (shown / 4) * 112;
            shown++;
            var cell = UIPanel("Shop" + def.id, page, x, y, 250, 104, new Color(.06f, .1f, .17f));
            ItemVisual(cell.transform, def.id, 1, -88, 14, 56);
            UILabel("Name", cell.transform, def.name, 30, 30, 180, 26, 15, Economy.RarityColors[(int)def.rarity]);
            UILabel("Effect", cell.transform, def.Describe(1) + " (+1)", 30, 6, 180, 24, 12, Color.white);
            var item = def;
            var buy = UIButton("Buy", cell.transform, "BUY  " + price, 16, -30, 200, 34, () => DoBuy(item));
            buy.interactable = !workshopBusy;
        }
    }

    // กล่องสุ่ม (แถวล่างของแท็บร้านค้า)
    private void BuildCrateOffer(RectTransform page)
    {
        var crate = UIPanel("Crate", page, 0, -140, 1040, 120, new Color(.1f, .08f, .18f));
        var crateIcon = UIPanel("CrateIcon", crate.transform, -440, 0, 96, 96, new Color(.55f, .4f, .2f));
        var crateSprite = Resources.Load<Sprite>("Images/Items/crate");
        if (crateSprite != null) { crateIcon.sprite = crateSprite; crateIcon.color = Color.white; crateIcon.preserveAspect = true; }
        else UILabel("Mark", crateIcon.transform, "?", 0, 0, 90, 90, 48, Color.white);
        UILabel("CrateName", crate.transform, "SUPPLY CRATE", -180, 32, 400, 32, 22, Color.white);
        UILabel("Odds", crate.transform, "Chances:  Astronium 200-500 " + Economy.CrateOdds[0] + "%   COMMON item " + Economy.CrateOdds[1]
            + "%   RARE item " + Economy.CrateOdds[2] + "%   EPIC item " + Economy.CrateOdds[3] + "%", -50, -8, 680, 26, 14, Color.gray);
        UILabel("Opened", crate.transform, "Opened: " + Economy.Data.cratesOpened, -180, -38, 400, 24, 14, Color.gray);
        var open = UIButton("Open", crate.transform, "OPEN  " + Economy.CratePrice, 400, 0, 200, 60, DoOpenCrate);
        open.interactable = !workshopBusy;
    }

    // กดซื้อไอเท็มจากร้านค้า ล็อกปุ่มระหว่างรอผลแล้วแสดงข้อความผลการซื้อ
    private void DoBuy(Economy.ItemDef def)
    {
        if (workshopBusy) return;
        workshopBusy = true;
        BuildWorkshopPage();
        Economy.BuyItem(def, (ok, message, balance) => WorkshopResult(message, balance));
    }

    // กดเปิดกล่องสุ่ม (Supply Crate) แสดงผลที่ได้ แล้วอ่านยอดเหรียญใหม่อีกครั้ง
    private void DoOpenCrate()
    {
        if (workshopBusy) return;
        workshopBusy = true;
        BuildWorkshopPage();
        Economy.OpenCrate((ok, message, balance) =>
        {
            WorkshopResult(message, balance);
            if (ok) PlayCrateReveal(); // อนิเมชันเปิดกล่อง (LobbyManager.CrateReveal.cs)
            // กล่องที่ได้เหรียญ: เหรียญถูกเพิ่มหลังหัก อ่านยอดใหม่อีกครั้ง
            StartCoroutine(RefreshCoinsLater());
        });
    }

    // ไอคอนไอเท็ม: มีรูปใน Resources ใช้รูป ไม่มีใช้กล่องสีความหายาก + ตัวอักษร 2 ตัวแรก
    private void ItemVisual(Transform parent, string id, int level, float x, float y, float size)
    {
        var def = Economy.FindItem(id);
        Color rarity = def != null ? Economy.RarityColors[(int)def.rarity] : Color.gray;
        var icon = UIPanel("Icon", parent, x, y, size, size, rarity * .55f + new Color(0, 0, 0, .45f));
        var sprite = def != null ? Resources.Load<Sprite>(def.IconPath) : null;
        if (sprite != null) { icon.sprite = sprite; icon.color = Color.white; icon.preserveAspect = true; }
        else UILabel("Initials", icon.transform, id.Length >= 2 ? id.Substring(0, 2) : id, 0, 0, size, size, 22, Color.white);
    }

    // ===== ห้องรอ: Host เปิด/ปิดการใช้ค่าตีบวก =====
    private Button roomUpgradesButton;

    // สร้างปุ่ม UPGRADES ON/OFF ในห้องรอ (เรียกจาก LobbyManager.Views.cs ตอนสร้างหน้าห้องรอ)
    private void BuildRoomUpgradesButton(RectTransform root)
    {
        roomUpgradesButton = UIButton("RoomUpgrades", root, "UPGRADES ON", -210, -334, 140, 44, OnRoomUpgradesClicked);
    }

    // อัปเดตปุ่ม UPGRADES: แสดงเฉพาะตอนอยู่ในห้อง กดได้เฉพาะคนที่แก้กติกาห้องได้ และแสดงสถานะ ON/OFF ปัจจุบัน
    private void RefreshRoomUpgradesButton()
    {
        if (roomUpgradesButton == null) return;
        bool visible = PhotonNetwork.InRoom && (FeatureFlags.Upgrades || FeatureFlags.Items);
        roomUpgradesButton.gameObject.SetActive(visible);
        if (!visible) return;
        roomUpgradesButton.interactable = CanEditRoomSettings();
        SetButtonLabel(roomUpgradesButton, MatchRules.UpgradesAllowed(PhotonNetwork.CurrentRoom) ? "UPGRADES ON" : "UPGRADES OFF");
    }

    // กดปุ่ม UPGRADES: สลับกติกาห้องว่าให้ใช้ค่าตีบวก/ไอเท็มในแมตช์หรือไม่
    private void OnRoomUpgradesClicked()
    {
        if (!CanEditRoomSettings()) return;
        SetRoomRule(MatchRules.UpgradesKey, !MatchRules.UpgradesAllowed(PhotonNetwork.CurrentRoom));
    }
}
