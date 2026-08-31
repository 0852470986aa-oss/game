using UnityEngine;
using Photon.Pun; // เรียกใช้ Library ของ Photon
using Photon.Realtime;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    void Start()
    {
        // เช็คก่อนว่าเชื่อมต่ออยู่แล้วหรือยัง (จาก LobbyManager)
        if (PhotonNetwork.IsConnected)
        {
            Debug.Log("เชื่อมต่อ Photon อยู่แล้ว! พร้อมเล่นเกม!");
            return;
        }

        // ถ้ายังไม่ได้เชื่อมต่อ ค่อยเริ่มเชื่อมต่อ
        Debug.Log("กำลังเชื่อมต่อเซิร์ฟเวอร์...");
        PhotonNetwork.ConnectUsingSettings();
    }

    // 2. เมื่อเชื่อมต่อกับ Master Server สำเร็จ
    public override void OnConnectedToMaster()
    {
        Debug.Log("เชื่อมต่อสำเร็จ! กำลังเข้าสู่ Lobby...");
        PhotonNetwork.JoinLobby(); // เข้าห้องโถงกลาง
    }

    // 3. เมื่อเข้า Lobby สำเร็จ
    public override void OnJoinedLobby()
    {
        Debug.Log("เข้าสู่ Lobby แล้ว! พร้อมสร้าง/เข้าร่วมห้อง (กรุณากดปุ่มเพื่อเริ่มเกม)");
        // ปิดการเข้าห้องอัตโนมัติ เพื่อรอให้ผู้เล่นกดปุ่มจาก Lobby
    }

    // ฟังก์ชันนี้จะถูกเรียกใช้เมื่อผู้เล่นกดปุ่ม "เล่นเกม" หรือ "สร้างห้อง" ในหน้า Lobby
    public void JoinBattleRoom()
    {
        Debug.Log("กำลังค้นหาหรือสร้างห้อง BattleRoom...");
        PhotonNetwork.JoinOrCreateRoom("BattleRoom", new RoomOptions { MaxPlayers = 4 }, TypedLobby.Default);
    }

    // 4. เมื่อเข้าห้องสำเร็จ
    public override void OnJoinedRoom()
    {
        Debug.Log("เข้าห้องสำเร็จ! พร้อมลุย!");

        // เมื่อเข้าห้องสำเร็จ ให้เปลี่ยน Scene ไปหน้าเล่นเกม (สมมติว่าใช้ SampleScene เป็นหน้าเล่นเกมตอนนี้)
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel("SampleScene");
        }
    }
}