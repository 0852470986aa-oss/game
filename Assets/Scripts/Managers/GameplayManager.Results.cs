// GameplayManager.Results.cs — ส่วนหน้าผลการแข่งของ partial class GameplayManager (Scene SampleScene)
// แสดงผล ชนะ/แพ้/เสมอ บันทึกผลและเหรียญลง Firebase ผ่าน FirebaseManager และระบบ "กลับห้องเดิม" หลังจบแมตช์
// ถูกเรียกจาก EndMatch() ใน GameplayManager.cs และ PlayerController.Health.cs; UI หน้าผลสร้าง/ผูกใน GameplayManager.HUD.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using TMPro;
using UnityEngine.UI;

// ผลการแข่งขัน รางวัล และการกลับห้องเดิม; ไม่เปลี่ยนชื่อ RPC/เมธอดเดิม
// (partial) คลาสเดียวกับ GameplayManager.cs แยกไฟล์ไว้เฉพาะส่วนผลการแข่ง
public partial class GameplayManager
{
    // ชื่อ key ใน Player Properties ที่เก็บ Firebase UID ของผู้เล่น (ใช้บันทึกว่าแข่งกับใคร)
    private const string FirebaseUidProperty = "FirebaseUid";

    // หา Firebase UID ของคู่แข่งเพื่อบันทึกประวัติแมตช์: ลอง Player Property "FirebaseUid" ก่อน
    // ถ้าไม่มีใช้ Photon UserId แทน ถ้าหาไม่ได้เลยคืนค่าว่าง ""
    private string GetOpponentFirebaseUid()
    {
        // คู่แข่งเป็นบอท (ไม่มีบัญชี Firebase)
        if (remotePlayer != null && remotePlayer.IsBot) return "BOT";
        Photon.Realtime.Player opponent = PhotonNetwork.PlayerListOthers.Length > 0
            ? PhotonNetwork.PlayerListOthers[0] : null;

        if (opponent != null)
        {
            if (opponent.CustomProperties != null
                && opponent.CustomProperties.TryGetValue(FirebaseUidProperty, out object firebaseUid)
                && firebaseUid is string uid && !string.IsNullOrWhiteSpace(uid))
                return uid;

            if (!string.IsNullOrWhiteSpace(opponent.UserId)) return opponent.UserId;
        }

        if (remotePlayer != null && remotePlayer.photonView != null && remotePlayer.photonView.Owner != null
            && !string.IsNullOrWhiteSpace(remotePlayer.photonView.Owner.UserId))
            return remotePlayer.photonView.Owner.UserId;

        Debug.LogWarning("Could not resolve opponent Firebase UID for match history.");
        return "";
    }

    // กดปุ่ม "RETURN TO SAME ROOM" ในหน้าผล: ตั้ง Player Property ReturnRoomToken = BattleToken ของรอบนี้ และ IsReady = false
    // ถ้าไม่ได้อยู่ในห้องแล้วจะกลับ LobbyScene ทันที; การพากลับห้องจริงทำใน CheckSameRoomReturn
    private void RequestSameRoom()
    {
        if (!resultShown || returningToRoom) return;
        if (!PhotonNetwork.InRoom) { SceneManager.LoadScene("LobbyScene"); return; }
        // โหมดเล่นคนเดียว: ปุ่มนี้ = เล่นอีกรอบ (เริ่มแมตช์ใหม่ทันที ไม่ต้องรอใคร)
        if (MatchRules.IsSolo(PhotonNetwork.CurrentRoom) && PhotonNetwork.IsMasterClient)
        {
            returningToRoom = true;
            var replay = new ExitGames.Client.Photon.Hashtable {
                ["Starting"] = false, ["StartTime"] = -1d, ["BattleToken"] = System.Guid.NewGuid().ToString("N"), ["BattleAborted"] = false };
            // Campaign: ผ่านด่านแล้วกดเล่นต่อ = ไปด่านถัดไป
            if (MatchRules.GameMode(PhotonNetwork.CurrentRoom) == MatchRules.ModeCampaign)
            {
                int stage = Campaign.NextStage;
                replay[MatchRules.CampaignStageKey] = stage;
                replay["MapIndex"] = Campaign.Get(stage).map;
                replay[MatchRules.BotDifficultyKey] = Campaign.Get(stage).difficulty;
            }
            PhotonNetwork.CurrentRoom.SetCustomProperties(replay);
            PhotonNetwork.DestroyAll();
            PhotonNetwork.LoadLevel("SampleScene");
            return;
        }
        requestedRematch = true;
        PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("BattleToken", out object token);
        PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable
        { ["ReturnRoomToken"] = token as string ?? "", ["IsReady"] = false });
        if (rematchLabel != null) rematchLabel.text = "WAITING FOR FRIEND...";
    }

    // เรียกทุกเฟรมจาก Update หลังขอกลับห้องเดิม: นับคนที่กดกลับแล้ว (token ตรงกับรอบนี้) แสดง x/y บนปุ่ม
    // เมื่อครบทุกคน Master Client รีเซ็ตสถานะห้อง เพิ่ม MapRevision ลบวัตถุเครือข่าย แล้วโหลด LobbyScene
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
        // ครบทุกคนแล้ว (เฉพาะ Master): รีเซ็ตห้องให้เป็นห้องรอ และเปิดห้องอีกครั้ง
        returningToRoom = true;
        int revision = room.CustomProperties.TryGetValue("MapRevision", out object rev) && rev is int number ? number : 0;
        room.SetCustomProperties(new ExitGames.Client.Photon.Hashtable {
            ["Starting"] = false, ["StartTime"] = -1d, ["BattleToken"] = "",
            ["BattleAborted"] = false, ["MapRevision"] = revision + 1 });
        room.IsOpen = room.IsVisible = true;
        PhotonNetwork.DestroyAll();
        PhotonNetwork.LoadLevel("LobbyScene");
    }

    // แสดงหน้าผลกรณีเสมอ (ทำครั้งเดียว): เหรียญ 0 ทั้งคู่ และบันทึกผลเสมอลง Firebase (RecordDrawMatch)
    // รันบนเครื่องตัวเอง แต่ละเครื่องบันทึกผลของตัวเอง
    private void ShowDrawResultScreen(string remotePlayerName)
    {
        if (resultShown) return;
        resultShown = true;
        // 1) เปิดหน้าผลพร้อมเอฟเฟกต์เด้ง ใส่ชื่อห้องและชื่อผู้เล่น
        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
            StartCoroutine(ScaleTweenRoutine(resultPanel.transform));
        }
        if (resultRoomNumber != null && PhotonNetwork.CurrentRoom != null) resultRoomNumber.text = PhotonNetwork.CurrentRoom.Name;
        if (localResultName != null) localResultName.text = PhotonNetwork.NickName;
        if (remoteResultName != null) remoteResultName.text = remotePlayerName;
        // 2) สถานะ "เสมอ" และเหรียญ 0
        if (localResultStatus != null) { localResultStatus.text = "เสมอ"; localResultStatus.color = Color.white; }
        if (remoteResultStatus != null) { remoteResultStatus.text = "เสมอ"; remoteResultStatus.color = Color.white; }
        if (localResultCoins != null) localResultCoins.text = "0";
        if (remoteResultCoins != null) remoteResultCoins.text = "0";
        // 3) ตั้งหัวข้อ/คะแนน/รูปยาน (null = เสมอ) แล้วบันทึกผลลง Firebase
        PresentResult(null);
        ReportProgress(false, true, 1, 2, 0);
        if (FirebaseManager.Instance != null)
        {
            string opponentUid = GetOpponentFirebaseUid();
            int localScore = PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("Kills", out object kills)
                ? (int)kills : 0;
            FirebaseManager.Instance.RecordDrawMatch(opponentUid, localScore,
                GameplayManager.GetCurrentMapName());
        }
        // 4) ผูกปุ่มกลับ Lobby ให้เรียก LeaveRoom
        if (btnReturnToMenu != null)
        {
            btnReturnToMenu.onClick.RemoveAllListeners();
            btnReturnToMenu.onClick.AddListener(LeaveRoom);
        }
    }

    // แสดงหน้าผลชนะ/แพ้ (ทำครั้งเดียว): ชนะ +190 / แพ้ +10 แล้วบันทึกผลลง Firebase (RecordMatchResult)
    // localScore = Kill ของเรา (-1 = ไปอ่านจาก Player Properties แทน) เรียกจาก EndMatch และ PlayerController.Health.cs
    public void ShowResultScreen(bool isWinner, string localShipName, string remoteShipName, string remotePlayerName, int localScore = -1)
    {
        if (resultShown) return;
        resultShown = true;
        isMatchEnding = true;
        // 1) เปิดหน้าผลพร้อมเอฟเฟกต์เด้ง และใส่ชื่อห้อง
        if (resultPanel != null) 
        {
            resultPanel.SetActive(true);
            StartCoroutine(ScaleTweenRoutine(resultPanel.transform));
        }

        if (resultRoomNumber != null && PhotonNetwork.CurrentRoom != null)
        {
            resultRoomNumber.text = PhotonNetwork.CurrentRoom.Name;
        }

        // 2) ชื่อเรา (รูปยานไปตั้งใน PresentResult)
        // Local Player Settings
        if (localResultName != null) localResultName.text = PhotonNetwork.NickName;
        if (localResultShip != null) 
        {
            // Try load ship sprite, using name or mapping (for now we assume 1,2,3 logic or just hide it if null)
        }

        // 3) ชนะ: เรากรอบเขียว +190 / คู่แข่งกรอบแดง +10 แล้วบันทึกผลชนะลง Firebase
        if (isWinner)
        {
            if (localResultOutline != null) localResultOutline.effectColor = new Color(0.2f, 0.8f, 0.4f); // Green
            if (localResultStatus != null) { localResultStatus.text = "ชัยชนะ +"; localResultStatus.color = new Color(1f, 0.8f, 0.2f); }
            if (localResultCoins != null) localResultCoins.text = WinReward.ToString();
            
            if (remoteResultOutline != null) remoteResultOutline.effectColor = new Color(0.8f, 0.2f, 0.2f); // Red
            if (remoteResultStatus != null) { remoteResultStatus.text = "พ่ายแพ้ +"; remoteResultStatus.color = new Color(1f, 0.4f, 0.4f); }
            if (remoteResultCoins != null) remoteResultCoins.text = LoseReward.ToString();

            // Update Firebase
            if (FirebaseManager.Instance != null)
            {
                string opponentUid = GetOpponentFirebaseUid();
                FirebaseManager.Instance.RecordMatchResult(true, opponentUid, WinReward,
                    ResolveLocalScore(localScore), GameplayManager.GetCurrentMapName());
            }
        }
        else
        {
            // แพ้: เรากรอบแดง +10 / คู่แข่งกรอบเขียว +190 แล้วบันทึกผลแพ้ลง Firebase
            if (localResultOutline != null) localResultOutline.effectColor = new Color(0.8f, 0.2f, 0.2f); // Red
            if (localResultStatus != null) { localResultStatus.text = "พ่ายแพ้ +"; localResultStatus.color = new Color(1f, 0.4f, 0.4f); }
            if (localResultCoins != null) localResultCoins.text = LoseReward.ToString();
            
            if (remoteResultOutline != null) remoteResultOutline.effectColor = new Color(0.2f, 0.8f, 0.4f); // Green
            if (remoteResultStatus != null) { remoteResultStatus.text = "ชัยชนะ +"; remoteResultStatus.color = new Color(1f, 0.8f, 0.2f); }
            if (remoteResultCoins != null) remoteResultCoins.text = WinReward.ToString();

            // Update Firebase
            if (FirebaseManager.Instance != null)
            {
                string opponentUid = GetOpponentFirebaseUid();
                FirebaseManager.Instance.RecordMatchResult(false, opponentUid, LoseReward,
                    ResolveLocalScore(localScore), GameplayManager.GetCurrentMapName());
            }
        }

        // 4) ชื่อคู่แข่ง หัวข้อ VICTORY/DEFEAT และผูกปุ่มกลับ Lobby
        if (remoteResultName != null) remoteResultName.text = remotePlayerName;
        PresentResult(isWinner);
        // เฟส 4: XP / ภารกิจ / Achievement
        ReportProgress(isWinner, false, isWinner ? 1 : 2, 2, isWinner ? WinReward : LoseReward);

        if (btnReturnToMenu != null)
        {
            btnReturnToMenu.onClick.RemoveAllListeners();
            btnReturnToMenu.onClick.AddListener(LeaveRoom);
        }
    }

    // คืน Kill ของเรา: ถ้าส่งมา (>= 0) ใช้ค่านั้น ไม่งั้นอ่าน "Kills" จาก Player Properties (ไม่มี = 0)
    private static int ResolveLocalScore(int suppliedScore)
    {
        if (suppliedScore >= 0) return suppliedScore;
        return PhotonNetwork.LocalPlayer != null
            && PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("Kills", out object kills)
            ? (int)kills : 0;
    }
}
