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

public partial class LobbyManager
{
    private Button workshopButton;
    private Image workshopOverlay;
    private RectTransform workshopWindow, workshopPage;
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
        workshopOverlay = UIPanel("WorkshopOverlay", root, 0, 0, 1280, 720, new Color(0, 0, 0, .72f));
        workshopOverlay.raycastTarget = true;
        workshopWindow = UIPanel("WorkshopWindow", workshopOverlay.transform, 0, 0, 1100, 640, panelColor).rectTransform;
        workshopOverlay.gameObject.SetActive(false);
        if (!economySubscribed && Application.isPlaying) { Economy.Changed += OnEconomyChanged; economySubscribed = true; }
        RefreshWorkshopButton();
    }

    private void RefreshWorkshopButton()
    {
        if (workshopButton != null) workshopButton.gameObject.SetActive(FeatureFlags.Upgrades || FeatureFlags.Items);
    }

    private void OnEconomyChanged()
    {
        if (this == null) { Economy.Changed -= OnEconomyChanged; return; }
        if (workshopOverlay != null && workshopOverlay.gameObject.activeSelf) BuildWorkshopPage();
    }

    private void OpenWorkshop()
    {
        if (workshopOverlay == null) return;
        Economy.EnsureLoaded();
        workshopShip = equippedShipIndex;
        workshopMessage = "";
        workshopOverlay.gameObject.SetActive(true);
        workshopOverlay.transform.SetAsLastSibling();
        if (FirebaseManager.Instance != null)
            FirebaseManager.Instance.GetCoinBalance(coins => { if (this == null) return; workshopCoins = coins; UpdateCoinDisplay(coins); BuildWorkshopPage(); });
        BuildWorkshopPage();
    }

    private void CloseWorkshop()
    {
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

    private void BuildWorkshopPage()
    {
        if (workshopWindow == null) return;
        if (workshopPage != null) Destroy(workshopPage.gameObject);
        workshopPage = UIRect("WPage" + (++pageSerial), workshopWindow, 0, 0, 1100, 640);
        var page = workshopPage;
        UILabel("Title", page, "WORKSHOP", -400, 286, 260, 44, 30, Color.white);
        UILabel("Coins", page, workshopCoins >= 0 ? "ASTRONIUM  " + workshopCoins.ToString("N0") : "ASTRONIUM  ...", 120, 286, 360, 36, 20, new Color(1f, .8f, .35f));
        UIButton("Close", page, "CLOSE", 470, 286, 130, 44, CloseWorkshop);
        // แท็บ
        string[] tabs = { "UPGRADE", "ITEMS", "SHOP" };
        for (int i = 0; i < tabs.Length; i++)
        {
            int tab = i;
            bool enabled = i == 0 ? FeatureFlags.Upgrades : FeatureFlags.Items;
            var button = UIButton("Tab" + i, page, tabs[i], -380 + i * 200, 232, 190, 44, () => { workshopTab = (WorkshopTab)tab; workshopMessage = ""; BuildWorkshopPage(); });
            button.interactable = enabled && (int)workshopTab != i;
        }
        if (workshopTab == WorkshopTab.Upgrade && !FeatureFlags.Upgrades) workshopTab = WorkshopTab.Items;
        // เลือกยาน (ใช้กับแท็บ UPGRADE / ITEMS)
        if (workshopTab != WorkshopTab.Shop)
        {
            UIButton("PrevShip", page, "<", 160, 232, 50, 44, () => CycleWorkshopShip(-1));
            UILabel("ShipName", page, ships[workshopShip].name, 330, 232, 280, 40, 20, accentColor);
            UIButton("NextShip", page, ">", 500, 232, 50, 44, () => CycleWorkshopShip(1));
        }
        if (workshopTab == WorkshopTab.Upgrade) BuildUpgradeTab(page);
        else if (workshopTab == WorkshopTab.Items) BuildItemsTab(page);
        else BuildShopTab(page);
        var message = UILabel("Message", page, workshopBusy ? "Processing..." : workshopMessage, 0, -296, 1040, 30, 18, new Color(1f, .85f, .5f));
        message.richText = false;
    }

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
        UILabel("SlotHead", page, "SHIP SLOTS (" + state.slots.Count + "/" + slots + ")  tap to remove", -300, 178, 440, 28, 16, accentColor);
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
        // คลังไอเท็ม 8 ชิ้นต่อหน้า (4 x 2)
        var items = new List<OwnedItem>(Economy.Data.items);
        items.Sort((a, b) => b.level != a.level ? b.level.CompareTo(a.level) : string.CompareOrdinal(a.item, b.item));
        int pages = Mathf.Max(1, (items.Count + 7) / 8);
        workshopItemPage = Mathf.Clamp(workshopItemPage, 0, pages - 1);
        UILabel("InvHead", page, "YOUR ITEMS (" + items.Count + ")", -400, 56, 240, 28, 16, accentColor);
        if (items.Count == 0) UILabel("None", page, "No items yet. Buy some in the SHOP tab or open a Supply Crate.", 0, -40, 900, 40, 18, Color.gray);
        for (int i = 0; i < 8; i++)
        {
            int index = workshopItemPage * 8 + i;
            if (index >= items.Count) break;
            var owned = items[index];
            var def = Economy.FindItem(owned.item);
            if (def == null) continue;
            float x = -390 + (i % 4) * 260, y = 6 - (i / 4) * 112;
            var cell = UIPanel("Item" + i, page, x, y, 250, 104, new Color(.06f, .1f, .17f));
            ItemVisual(cell.transform, def.id, owned.level, -88, 14, 56);
            var name = UILabel("Name", cell.transform, def.name + " +" + owned.level, 30, 30, 180, 26, 15, Economy.RarityColors[(int)def.rarity]);
            name.richText = false;
            UILabel("Effect", cell.transform, def.Describe(owned.level), 30, 6, 180, 24, 12, Color.white);
            int on = Economy.EquippedOn(owned.uid);
            var item = owned;
            var equip = UIButton("Equip", cell.transform, on == ship ? "REMOVE" : on >= 0 ? "MOVE HERE" : "EQUIP", -40, -30, 140, 34,
                () => { workshopMessage = Economy.ToggleEquip(ship, item); BuildWorkshopPage(); });
            var upgrade = UIButton("Up", cell.transform, owned.level >= Economy.ItemMaxLevel ? "MAX" : "+ " + Economy.ItemUpgradeCost(owned) + " (" + Economy.ItemUpgradeChance(owned.level) + "%)",
                72, -30, 96, 34, () => DoItemUpgrade(item));
            upgrade.interactable = owned.level < Economy.ItemMaxLevel && !workshopBusy;
            equip.interactable = !workshopBusy;
        }
        if (pages > 1)
        {
            UIButton("PrevPage", page, "<", 380, 56, 50, 34, () => { workshopItemPage--; BuildWorkshopPage(); });
            UILabel("PageNo", page, (workshopItemPage + 1) + "/" + pages, 440, 56, 70, 30, 16, Color.white);
            UIButton("NextPage", page, ">", 500, 56, 50, 34, () => { workshopItemPage++; BuildWorkshopPage(); });
        }
    }

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
        // กล่องสุ่ม
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

    private void DoBuy(Economy.ItemDef def)
    {
        if (workshopBusy) return;
        workshopBusy = true;
        BuildWorkshopPage();
        Economy.BuyItem(def, (ok, message, balance) => WorkshopResult(message, balance));
    }

    private void DoOpenCrate()
    {
        if (workshopBusy) return;
        workshopBusy = true;
        BuildWorkshopPage();
        Economy.OpenCrate((ok, message, balance) =>
        {
            WorkshopResult(message, balance);
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

    private void BuildRoomUpgradesButton(RectTransform root)
    {
        roomUpgradesButton = UIButton("RoomUpgrades", root, "UPGRADES ON", -210, -334, 140, 44, OnRoomUpgradesClicked);
    }

    private void RefreshRoomUpgradesButton()
    {
        if (roomUpgradesButton == null) return;
        bool visible = PhotonNetwork.InRoom && (FeatureFlags.Upgrades || FeatureFlags.Items);
        roomUpgradesButton.gameObject.SetActive(visible);
        if (!visible) return;
        roomUpgradesButton.interactable = CanEditRoomSettings();
        SetButtonLabel(roomUpgradesButton, MatchRules.UpgradesAllowed(PhotonNetwork.CurrentRoom) ? "UPGRADES ON" : "UPGRADES OFF");
    }

    private void OnRoomUpgradesClicked()
    {
        if (!CanEditRoomSettings()) return;
        SetRoomRule(MatchRules.UpgradesKey, !MatchRules.UpgradesAllowed(PhotonNetwork.CurrentRoom));
    }
}
