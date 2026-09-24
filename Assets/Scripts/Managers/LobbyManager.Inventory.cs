using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using System.Collections.Generic;

// ส่วน Inventory ของ LobbyManager; partial คือคลาสเดิม ไม่ต้องเพิ่ม Component
public partial class LobbyManager
{
    private void UpdateShipDisplay(int index)
    {
        if (index < 0 || index >= ships.Length) return;
        ShipData ship = ships[index];

        if (shipNameText != null) shipNameText.text = ship.name;
        if (shipHPText != null) shipHPText.text = "HP: " + ship.hp;
        if (shipATKText != null) shipATKText.text = "ATK: " + ship.atk;
        if (shipSPDText != null) shipSPDText.text = "SPD: " + ship.spd;
        if (shipSkillText != null) shipSkillText.text = ""; // ลบระบบ Skill เดิมทิ้ง
        if (shipImage != null)
        {
            Sprite sp = Resources.Load<Sprite>(ship.spritePath);
            if (sp != null)
            {
                shipImage.sprite = sp;
                shipImage.color = Color.white;
            }
        }
    }

    private void UpdateInventoryDisplay(int index)
    {
        if (index < 0 || index >= ships.Length) return;
        ShipData ship = ships[index];

        if (inventoryShipName != null) inventoryShipName.text = ship.name;
        if (inventoryShipHP != null) inventoryShipHP.text = "HP: " + ship.hp;
        if (inventoryShipATK != null) inventoryShipATK.text = "ATK: " + ship.atk;
        if (inventoryShipSPD != null) inventoryShipSPD.text = "SPD: " + ship.spd;
        if (inventoryShipSkill != null) inventoryShipSkill.text = ""; // ลบระบบ Skill เดิมทิ้ง
        if (inventoryShipImage != null)
        {
            Sprite sp = Resources.Load<Sprite>(ship.spritePath);
            if (sp != null)
            {
                inventoryShipImage.sprite = sp;
                inventoryShipImage.color = Color.white;
            }
        }

        UpdateInventoryActionButton(index);
    }

    private void UpdateInventoryActionButton(int index)
    {
        RefreshHangarCards();
        if (inventoryActionButton == null) return;
        
        if (inventoryActionText == null)
            inventoryActionText = inventoryActionButton.GetComponentInChildren<TMP_Text>();

        // ถ้าปลดล็อกแล้ว
        if (unlockedShips.Contains(index))
        {
            if (index == equippedShipIndex)
            {
                inventoryActionButton.interactable = false;
                inventoryActionButton.GetComponent<Image>().color = new Color(0.5f, 0.5f, 0.5f);
                if (inventoryActionText != null) inventoryActionText.text = "Equipped";
            }
            else
            {
                inventoryActionButton.interactable = true;
                inventoryActionButton.GetComponent<Image>().color = new Color(0.2f, 0.6f, 0.3f);
                if (inventoryActionText != null) inventoryActionText.text = "Equip";
            }
        }
        else // ถ้ายังไม่ปลดล็อก
        {
            inventoryActionButton.interactable = true;
            inventoryActionButton.GetComponent<Image>().color = new Color(0.8f, 0.6f, 0.1f);
            if (inventoryActionText != null) inventoryActionText.text = "Buy (" + ships[index].price + ")";
        }
    }

    public void SelectShip(int index)
    {
        if (hangarBusy || index < 0 || index >= ships.Length) return;
        selectedShipIndex = index;
        UpdateInventoryDisplay(index);
    }

    public void OnInventoryActionClicked()
    {
        if (hangarBusy || !profileLoaded) return;
        int index = selectedShipIndex;
        if (index < 0 || index >= ships.Length) return;

        // สวมใส่ยาน
        if (unlockedShips.Contains(index))
        {
            equippedShipIndex = index;
            if (FirebaseManager.Instance != null)
            {
                FirebaseManager.Instance.SaveSelectedShip(index);
            }
            UpdateShipDisplay(equippedShipIndex); // อัปเดตหน้าล็อบบี้
            UpdateInventoryActionButton(index);   // อัปเดตปุ่ม
        }
        // ซื้อยาน
        else
        {
            ShipData ship = ships[index];
            if (FirebaseManager.Instance != null)
            {
                hangarBusy = true;
                inventoryActionButton.interactable = false; // ป้องกันการกดเบิ้ล
                FirebaseManager.Instance.PurchaseShip(index, ship.price, (success, balance) =>
                {
                    if (this == null) return;
                    hangarBusy = false;
                    if (success)
                    {
                        if (!unlockedShips.Contains(index)) unlockedShips.Add(index);
                        UpdateCoinDisplay(balance);
                        UpdateStatus("Purchased " + ship.name + "!");
                    }
                    else UpdateStatus("Purchase not confirmed. Check coins/connection and try again.");
                    UpdateInventoryActionButton(index);
                });
            }
        }
    }

    // ============================
    //  SKILL FUNCTIONS
    // ============================

    public void SelectSkill(int index)
    {
        if (index < 0 || index >= skills.Length) return;
        selectedSkillIndex = index;
        UpdateSkillDisplay(index);
    }

    private void UpdateSkillDisplay(int index)
    {
        if (index < 0 || index >= skills.Length) return;
        SkillData skill = skills[index];
        if (hangarSkillCards != null)
            for (int i = 0; i < hangarSkillCards.Length; i++)
            {
                hangarSkillCards[i].GetComponent<Image>().color = i == index ? new Color(0.1f, 0.38f, 0.46f) : panelColor;
                hangarSkillStates[i].text = i == equippedSkillIndex ? "INSTALLED" : i == index ? "SELECTED" : "AVAILABLE";
            }
        if (homeSkillText != null)
            homeSkillText.text = "EQUIPPED  /  " + skills[equippedSkillIndex].name;

        if (skillDescText != null)
            skillDescText.text = skill.name + " - " + skill.description;

        if (installSkillButton == null) return;
        if (installSkillText == null) installSkillText = installSkillButton.GetComponentInChildren<TMP_Text>();

        if (index == equippedSkillIndex)
        {
            installSkillButton.interactable = false;
            installSkillButton.GetComponent<Image>().color = new Color(0.5f, 0.5f, 0.5f);
            if (installSkillText != null) installSkillText.text = "Installed";
        }
        else
        {
            installSkillButton.interactable = true;
            installSkillButton.GetComponent<Image>().color = new Color(0.3f, 0.6f, 0.9f);
            if (installSkillText != null) installSkillText.text = "Install";
        }
    }

    public void OnInstallSkillClicked()
    {
        equippedSkillIndex = selectedSkillIndex;
        if (FirebaseManager.Instance != null)
        {
            FirebaseManager.Instance.SaveSelectedSkill(equippedSkillIndex);
        }
        UpdateSkillDisplay(equippedSkillIndex);
        UpdateStatus("Installed " + skills[equippedSkillIndex].name + " Skill!");
    }

    // ============================
    //  PHOTON CALLBACKS
    // ============================
}
