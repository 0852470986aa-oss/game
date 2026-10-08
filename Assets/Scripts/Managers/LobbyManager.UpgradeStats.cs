// LobbyManager.UpgradeStats.cs — ค่าพลังยานในล็อบบี้รวมผลตีบวกยาน + ไอเท็ม (partial class ของ LobbyManager)
// เดิม: หน้าหลัก / โรงเก็บยาน / ห้องรอ แสดงค่าพื้นฐานของยานเสมอ ตีบวกแล้วตัวเลขไม่เปลี่ยน
// ใหม่: แสดงค่าที่ใช้ในการต่อสู้จริง + ส่วนที่เพิ่มสีเขียว เช่น "HP: 104 (+14)" และอัปเดตทันทีเมื่อตีบวก/ใส่ไอเท็ม (Economy.Changed)
// ห้องรอ: แสดงค่าตีบวกเฉพาะห้องที่เปิดตีบวกยาน / ยานคนอื่นอ่านจาก Custom Property "StatBonus" ที่แต่ละคนส่งมา (FeatureFlags.ShowRivalStats)
// แยกสี (FeatureFlags.ItemStatColor): ส่วนตีบวกสีเขียว ตามด้วยส่วนจากไอเท็มสีม่วง เช่น "HP: 175 (+30) (+20)"
// ปิด FeatureFlags.Upgrades และ Items = แสดงค่าพื้นฐานเหมือนเดิม
using UnityEngine;

// ส่วนค่าพลังรวมตีบวกในล็อบบี้ของ LobbyManager (partial)
public partial class LobbyManager
{
    // ข้อความค่าพลัง 1 ค่า: ค่ารวม + (ส่วนที่เพิ่ม) สีเขียว / ไม่มีโบนัส = ค่าพื้นฐานอย่างเดียว
    private static string UpgradedStat(float baseValue, float bonus, string format)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        float total = baseValue * (1f + bonus);
        string text = total.ToString(format, culture);
        if (bonus <= .0001f) return text;
        return text + " <color=#7CFF9A>(+" + (total - baseValue).ToString(format, culture) + ")</color>";
    }

    // โบนัสตีบวก + ไอเท็มของยาน ship (ยังไม่มีข้อมูล/ปิดระบบ = ไม่มีโบนัส)
    private static ShipBonus LobbyBonus(int ship)
    {
        if (!FeatureFlags.Upgrades && !FeatureFlags.Items) return new ShipBonus();
        return Economy.BuildBonus(ship) ?? new ShipBonus();
    }

    // สีตัวเลขส่วนที่เพิ่ม: ตีบวก = เขียว, ไอเท็ม = ม่วง
    private const string UpgradeBonusColor = "#7CFF9A", ItemBonusColor = "#C58CFF";
    // ชื่อ Custom Property ที่ส่งโบนัสของยานที่ใช้อยู่ให้คนอื่นในห้องเห็น: "ยาน;ตีบวกHP,ATK,SPD;ไอเท็มHP,ATK,SPD"
    private const string StatBonusProperty = "StatBonus";

    // ข้อความค่าพลัง 1 ค่าแบบแยกสี: ค่ารวม (+ตีบวก สีเขียว) (+ไอเท็ม สีม่วง) ส่วนไหนเป็นศูนย์ไม่แสดง
    private static string SplitStat(float baseValue, float upgrade, float item, string format)
    {
        if (!FeatureFlags.ItemStatColor) return UpgradedStat(baseValue, upgrade + item, format);
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        string text = (baseValue * (1f + upgrade + item)).ToString(format, culture);
        if (upgrade > .0001f) text += " <color=" + UpgradeBonusColor + ">(+" + (baseValue * upgrade).ToString(format, culture) + ")</color>";
        if (item > .0001f) text += " <color=" + ItemBonusColor + ">(+" + (baseValue * item).ToString(format, culture) + ")</color>";
        return text;
    }

    // โบนัสแยกส่วนของยานเราเอง: ตีบวก / ไอเท็ม (ปิดระบบ = ศูนย์)
    private static void LocalBonusParts(int ship, out ShipBonus upgrade, out ShipBonus item)
    {
        upgrade = Economy.PartBonus(ship, true);
        item = Economy.PartBonus(ship, false);
    }

    // ข้อความโบนัสของยานที่ใส่อยู่ ส่งให้คนอื่นในห้อง (ค่าเป็นสัดส่วน เช่น 0.24)
    private string LocalStatBonusText()
    {
        int ship = Mathf.Clamp(equippedShipIndex, 0, ships.Length - 1);
        LocalBonusParts(ship, out var up, out var item);
        var c = System.Globalization.CultureInfo.InvariantCulture;
        System.Func<ShipBonus, string> part = b => b.hp.ToString("0.####", c) + "," + b.atk.ToString("0.####", c) + "," + b.spd.ToString("0.####", c);
        return ship + ";" + part(up) + ";" + part(item);
    }

    // อ่านโบนัสของผู้เล่นคนอื่นจาก Custom Property (ไม่มี/ยานไม่ตรง/อ่านไม่ได้ = false)
    private static bool ReadStatBonus(Photon.Realtime.Player player, int ship, out ShipBonus upgrade, out ShipBonus item)
    {
        upgrade = new ShipBonus(); item = new ShipBonus();
        if (player == null || !player.CustomProperties.TryGetValue(StatBonusProperty, out object raw) || !(raw is string text)) return false;
        var parts = text.Split(';');
        if (parts.Length != 3 || !int.TryParse(parts[0], out int sentShip) || sentShip != ship) return false;
        return ParseBonus(parts[1], upgrade) && ParseBonus(parts[2], item);
    }

    // แปลง "hp,atk,spd" เป็นค่าใน ShipBonus (จำกัดไม่เกิน +300% กันค่าผิดปกติ)
    private static bool ParseBonus(string text, ShipBonus into)
    {
        var values = text.Split(',');
        if (values.Length != 3) return false;
        var c = System.Globalization.CultureInfo.InvariantCulture;
        var style = System.Globalization.NumberStyles.Float;
        if (!float.TryParse(values[0], style, c, out float hp) || !float.TryParse(values[1], style, c, out float atk)
            || !float.TryParse(values[2], style, c, out float spd)) return false;
        into.hp = Mathf.Clamp(hp, 0f, 3f); into.atk = Mathf.Clamp(atk, 0f, 3f); into.spd = Mathf.Clamp(spd, 0f, 3f);
        return true;
    }

    // ส่งโบนัสล่าสุดให้คนอื่นในห้อง (ตีบวก/ใส่ไอเท็มระหว่างอยู่ในห้อง)
    private void PublishStatBonus()
    {
        if (!FeatureFlags.ShowRivalStats || !Photon.Pun.PhotonNetwork.InRoom || !profileLoaded) return;
        Photon.Pun.PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { [StatBonusProperty] = LocalStatBonusText() });
    }

    // เขียนค่า HP / ATK / SPD (รวมตีบวก) ลงป้ายสามอัน (prefix = "HP: " ฯลฯ, spdSuffix = ข้อความต่อท้าย SPD)
    private void WriteUpgradedStats(int index, TMPro.TMP_Text hp, TMPro.TMP_Text atk, TMPro.TMP_Text spd, string spdSuffix)
    {
        if (index < 0 || index >= ships.Length) return;
        var ship = ships[index];
        LocalBonusParts(index, out var up, out var item);
        if (hp != null) { hp.richText = true; hp.text = "HP: " + SplitStat(ship.hp, up.hp, item.hp, "0"); }
        if (atk != null) { atk.richText = true; atk.text = "ATK: " + SplitStat(ship.atk, up.atk, item.atk, "0.#"); }
        if (spd != null) { spd.richText = true; spd.text = "SPD: " + SplitStat(ship.spd, up.spd, item.spd, "0.#") + spdSuffix; }
    }

    // บรรทัดค่าพลังในการ์ดห้องรอ (ห้องเปิดตีบวกยาน = รวมโบนัส: ยานเรา = ข้อมูลในเครื่อง, คนอื่น = ค่าที่เขาส่งมา)
    private string RosterStats(int ship, Photon.Realtime.Player player)
    {
        var data = ships[ship];
        bool local = player != null && player.IsLocal;
        var up = new ShipBonus();
        var item = new ShipBonus();
        if (MatchRules.UpgradesAllowed(Photon.Pun.PhotonNetwork.CurrentRoom))
        {
            if (local) LocalBonusParts(ship, out up, out item);
            else if (FeatureFlags.ShowRivalStats) ReadStatBonus(player, ship, out up, out item);
        }
        string hp = SplitStat(data.hp, up.hp, item.hp, "0"), atk = SplitStat(data.atk, up.atk, item.atk, "0.#"), spd = SplitStat(data.spd, up.spd, item.spd, "0.#");
        // แยกสีแล้วข้อความยาวขึ้น: การ์ดหน้าเตรียมพร้อมรบแบ่งเป็น 2 บรรทัด (ช่องสูงขึ้นใน PrepPilotCard)
        if (FeatureFlags.ItemStatColor && prepLayout) return "HP " + hp + "   /   ATK " + atk + "\nSPD " + spd;
        return hp + " HP   /   ATK " + atk + "   /   SPD " + spd;
    }

    // ตีบวก/ใส่ไอเท็มแล้ว (Economy.Changed): อัปเดตค่าพลังหน้าหลัก โรงเก็บยาน และการ์ดห้องรอทันที
    private void RefreshUpgradedStats()
    {
        if (!profileLoaded) return; // ยังโหลดโปรไฟล์ไม่เสร็จ (FinishProfileLoad จะวาดค่าพวกนี้เอง)
        UpdateShipDisplay(equippedShipIndex);
        UpdateInventoryDisplay(selectedShipIndex);
        PublishStatBonus(); // คนอื่นในห้องเห็นค่าใหม่ด้วย
        if (Photon.Pun.PhotonNetwork.InRoom && waitingRoomPanel != null && waitingRoomPanel.activeInHierarchy) UpdateWaitingRoomUI();
    }
}
