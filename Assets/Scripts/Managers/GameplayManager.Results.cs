using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using TMPro;
using UnityEngine.UI;

// ผลการแข่งขัน รางวัล และการกลับห้องเดิม; ไม่เปลี่ยนชื่อ RPC/เมธอดเดิม
public partial class GameplayManager
{
    private void RequestSameRoom()
    {
        if (!resultShown || returningToRoom) return;
        if (!PhotonNetwork.InRoom) { SceneManager.LoadScene("LobbyScene"); return; }
        requestedRematch = true;
        PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("BattleToken", out object token);
        PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable
        { ["ReturnRoomToken"] = token as string ?? "", ["IsReady"] = false });
        if (rematchLabel != null) rematchLabel.text = "WAITING FOR FRIEND...";
    }

    private void CheckSameRoomReturn()
    {
        if (!resultShown || !requestedRematch || returningToRoom || !PhotonNetwork.InRoom) return;
        var room = PhotonNetwork.CurrentRoom;
        room.CustomProperties.TryGetValue("BattleToken", out object token);
        string round = token as string ?? "";
        int returned = 0, present = 0;
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player.IsInactive) continue;
            present++;
            if (player.CustomProperties.TryGetValue("ReturnRoomToken", out object value) && value is string text && text == round) returned++;
        }
        if (rematchLabel != null) rematchLabel.text = "RETURN TO ROOM  " + returned + "/" + present;
        if (!PhotonNetwork.IsMasterClient || returned != present) return;
        returningToRoom = true;
        int revision = room.CustomProperties.TryGetValue("MapRevision", out object rev) && rev is int number ? number : 0;
        room.SetCustomProperties(new ExitGames.Client.Photon.Hashtable {
            ["Starting"] = false, ["StartTime"] = -1d, ["BattleToken"] = "",
            ["BattleAborted"] = false, ["MapRevision"] = revision + 1 });
        room.IsOpen = room.IsVisible = true;
        PhotonNetwork.DestroyAll();
        PhotonNetwork.LoadLevel("LobbyScene");
    }

    private void ShowDrawResultScreen(string remotePlayerName)
    {
        if (resultShown) return;
        resultShown = true;
        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
            StartCoroutine(ScaleTweenRoutine(resultPanel.transform));
        }
        if (resultRoomNumber != null && PhotonNetwork.CurrentRoom != null) resultRoomNumber.text = PhotonNetwork.CurrentRoom.Name;
        if (localResultName != null) localResultName.text = PhotonNetwork.NickName;
        if (remoteResultName != null) remoteResultName.text = remotePlayerName;
        if (localResultStatus != null) { localResultStatus.text = "เสมอ"; localResultStatus.color = Color.white; }
        if (remoteResultStatus != null) { remoteResultStatus.text = "เสมอ"; remoteResultStatus.color = Color.white; }
        if (localResultCoins != null) localResultCoins.text = "0";
        if (remoteResultCoins != null) remoteResultCoins.text = "0";
        PresentResult(null);
        if (btnReturnToMenu != null)
        {
            btnReturnToMenu.onClick.RemoveAllListeners();
            btnReturnToMenu.onClick.AddListener(LeaveRoom);
        }
    }

    public void ShowResultScreen(bool isWinner, string localShipName, string remoteShipName, string remotePlayerName)
    {
        if (resultShown) return;
        resultShown = true;
        isMatchEnding = true;
        if (resultPanel != null) 
        {
            resultPanel.SetActive(true);
            StartCoroutine(ScaleTweenRoutine(resultPanel.transform));
        }

        if (resultRoomNumber != null && PhotonNetwork.CurrentRoom != null)
        {
            resultRoomNumber.text = PhotonNetwork.CurrentRoom.Name;
        }

        // Local Player Settings
        if (localResultName != null) localResultName.text = PhotonNetwork.NickName;
        if (localResultShip != null) 
        {
            // Try load ship sprite, using name or mapping (for now we assume 1,2,3 logic or just hide it if null)
        }

        if (isWinner)
        {
            if (localResultOutline != null) localResultOutline.effectColor = new Color(0.2f, 0.8f, 0.4f); // Green
            if (localResultStatus != null) { localResultStatus.text = "ชัยชนะ +"; localResultStatus.color = new Color(1f, 0.8f, 0.2f); }
            if (localResultCoins != null) localResultCoins.text = "190";
            
            if (remoteResultOutline != null) remoteResultOutline.effectColor = new Color(0.8f, 0.2f, 0.2f); // Red
            if (remoteResultStatus != null) { remoteResultStatus.text = "พ่ายแพ้ +"; remoteResultStatus.color = new Color(1f, 0.4f, 0.4f); }
            if (remoteResultCoins != null) remoteResultCoins.text = "10";

            // Update Firebase
            if (FirebaseManager.Instance != null)
            {
                FirebaseManager.Instance.RecordMatchResult(true, remotePlayerName, 190);
            }
        }
        else
        {
            if (localResultOutline != null) localResultOutline.effectColor = new Color(0.8f, 0.2f, 0.2f); // Red
            if (localResultStatus != null) { localResultStatus.text = "พ่ายแพ้ +"; localResultStatus.color = new Color(1f, 0.4f, 0.4f); }
            if (localResultCoins != null) localResultCoins.text = "10";
            
            if (remoteResultOutline != null) remoteResultOutline.effectColor = new Color(0.2f, 0.8f, 0.4f); // Green
            if (remoteResultStatus != null) { remoteResultStatus.text = "ชัยชนะ +"; remoteResultStatus.color = new Color(1f, 0.8f, 0.2f); }
            if (remoteResultCoins != null) remoteResultCoins.text = "190";

            // Update Firebase
            if (FirebaseManager.Instance != null)
            {
                FirebaseManager.Instance.RecordMatchResult(false, remotePlayerName, 10);
            }
        }

        if (remoteResultName != null) remoteResultName.text = remotePlayerName;
        PresentResult(isWinner);

        if (btnReturnToMenu != null)
        {
            btnReturnToMenu.onClick.RemoveAllListeners();
            btnReturnToMenu.onClick.AddListener(LeaveRoom);
        }
    }
}
