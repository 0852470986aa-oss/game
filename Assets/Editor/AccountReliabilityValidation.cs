using System;
using System.IO;
using UnityEngine;

public static class AccountReliabilityValidation
{
    public static void Run()
    {
        if (!Application.isBatchMode) throw new Exception("Batch only; no live account writes");
        int count=0;
        void Check(bool ok,string name) { if(!ok) throw new Exception(name); count++; }
        foreach(object value in new object[]{0,5000L,5000d,"5000","5000.0","5e3",long.MaxValue})
            Check(AccountDataValidation.Coins(value,out _),"Valid balance "+value);
        foreach(object value in new object[]{null,-1,"",true,"NaN","Infinity",1.5d,"9223372036854775808","invalid"})
            Check(!AccountDataValidation.Coins(value,out _),"Invalid balance rejected");
        Check(AccountDataValidation.Coins("5000.0",out long coins) && coins==5000,"Integer double unchanged");
        Check(AccountDataValidation.Ships("0,2,5",out var ships) && ships.Count==3 && ships.Contains(5),"Purchased ships retained");
        Check(AccountDataValidation.Ships("2, 2, 5",out ships) && ships.Count==3 && ships.Contains(0),"Duplicate and starter handling");
        Check(AccountDataValidation.Ships("0,100",out ships) && ships.Contains(100),"Future ownership preserved");
        foreach(object value in new object[]{null,"","0,bad","0,-1"})
            Check(!AccountDataValidation.Ships(value,out _),"Invalid ownership is not starter success");
        string source = File.ReadAllText("Assets/Scripts/Managers/FirebaseManager.Reliability.cs");
        Check(source.Contains("data.Child(\"coin_operations\").Child(operation).Value = amount"),"Receipt inside transaction");
        Check(source.Contains("Guid.NewGuid().ToString(\"N\")"),"Unique operation created outside retry");
        string lobby = File.ReadAllText("Assets/Scripts/Managers/LobbyManager.Views.cs");
        Check(lobby.Contains("GetAccountLoadout(profile =>"),"Lobby uses consistent snapshot");
        Check(!lobby.Contains("GetUnlockedShips(result =>"),"Old starter fallback removed");
        Debug.Log("ACCOUNT_RELIABILITY_PASS: "+count+" checks; validation and source contracts only, no Firebase account writes.");
        PremiumStoryValidation.Run();
    }
}
