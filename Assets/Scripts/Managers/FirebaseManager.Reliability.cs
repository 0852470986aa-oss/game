using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine;

public sealed class AccountLoadout
{
    public List<int> ships;
    public int selectedShip, selectedSkill;
    public long coins;
}

// Pure validation shared by production and offline regression tests. Never invent a balance or ownership.
public static class AccountDataValidation
{
    public static bool Coins(object value, out long coins)
    {
        coins = 0;
        if (value == null || value is bool) return false;
        if (!decimal.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out decimal number)
            || number < 0 || number > long.MaxValue || decimal.Truncate(number) != number) return false;
        coins = (long)number;
        return true;
    }
    public static bool Ships(object value, out List<int> ships)
    {
        ships = new List<int>();
        if (!(value is string text) || string.IsNullOrWhiteSpace(text)) return false;
        foreach(var part in text.Split(','))
        {
            if (!int.TryParse(part.Trim(), out int index) || index < 0) return false;
            if (!ships.Contains(index)) ships.Add(index); // Keep future IDs; don't silently erase ownership.
        }
        if (!ships.Contains(0)) ships.Add(0);
        return true;
    }
}

public partial class FirebaseManager
{
    // One snapshot for balance + ownership + equipped loadout; failure never masquerades as a starter account.
    public void GetAccountLoadout(Action<AccountLoadout> onResult, Action<string> onError)
    {
        if (user == null || dbReference == null) { onError?.Invoke("Account is not ready."); return; }
        string uid = user.UserId;
        var reference = dbReference.Child("users").Child(uid);
        void Read(int attempt)
        {
            reference.GetValueAsync().ContinueWithOnMainThread(task => {
                if (this == null || user == null || user.UserId != uid) { onError?.Invoke("Account changed."); return; }
                if (!task.IsFaulted && !task.IsCanceled && task.Result != null && task.Result.Exists)
                {
                    var data = task.Result;
                    if (AccountDataValidation.Coins(data.Child("coin_balance").Value, out long balance)
                        && AccountDataValidation.Ships(data.Child("unlocked_ships").Value, out var ships)
                        && int.TryParse(data.Child("selected_ship").Value?.ToString(), out int ship)
                        && int.TryParse(data.Child("selected_skill").Value?.ToString(), out int skill))
                    {
                        onResult?.Invoke(new AccountLoadout { coins=balance, ships=ships, selectedShip=ship, selectedSkill=skill });
                        return;
                    }
                }
                if (attempt < 2)
                    Task.Delay(1000 * (attempt + 1)).ContinueWithOnMainThread(_ => {
                        if (this != null && user != null && user.UserId == uid) Read(attempt + 1);
                        else onError?.Invoke("Account changed.");
                    });
                else onError?.Invoke("Could not load account. Retrying; your ships and coins have not been reset.");
            });
        }
        Read(0);
    }

    // A receipt and the balance commit atomically. Retrying a lost acknowledgement cannot award twice.
    private void AddCoinsReliable(int amount, Action<bool> onResult)
    {
        if (amount < 0 || user == null || dbReference == null) { onResult?.Invoke(false); return; }
        string uid = user.UserId, operation = Guid.NewGuid().ToString("N");
        var reference = dbReference.Child("users").Child(uid);
        void Attempt(int attempt)
        {
            if (this == null || user == null || user.UserId != uid) { onResult?.Invoke(false); return; }
            // Warm the cache before transaction; never initialize a missing balance with zero.
            reference.GetValueAsync().ContinueWithOnMainThread(read => {
                if (this == null || user == null || user.UserId != uid) { onResult?.Invoke(false); return; }
                if (read.IsFaulted || read.IsCanceled || read.Result == null || !read.Result.Exists)
                { Retry(attempt,"profile read failed"); return; }
                if (!AccountDataValidation.Coins(read.Result.Child("coin_balance").Value,out _))
                { Retry(attempt,"balance missing or invalid"); return; }
                reference.RunTransaction(data => {
                    if (data.Value == null) return TransactionResult.Success(data);
                    if (data.Child("coin_operations").Child(operation).Value != null) return TransactionResult.Success(data);
                    if (!AccountDataValidation.Coins(data.Child("coin_balance").Value,out long balance)
                        || balance > long.MaxValue - amount) return TransactionResult.Abort();
                    data.Child("coin_balance").Value = balance + amount;
                    data.Child("coin_operations").Child(operation).Value = amount;
                    return TransactionResult.Success(data);
                },false).ContinueWithOnMainThread(saved => {
                    if (this == null || user == null || user.UserId != uid) { onResult?.Invoke(false); return; }
                    bool confirmed = !saved.IsFaulted && !saved.IsCanceled && saved.Result != null
                        && saved.Result.Child("coin_operations").HasChild(operation);
                    if (confirmed) onResult?.Invoke(true);
                    else Retry(attempt, saved.IsFaulted ? "transaction failed: " + saved.Exception.GetBaseException().GetType().Name : "transaction not confirmed");
                });
            });
        }
        void Retry(int attempt, string reason)
        {
            Debug.LogWarning("Coin save attempt " + (attempt+1) + ": " + reason + ". Existing balance preserved.");
            if (attempt >= 2) { onResult?.Invoke(false); return; }
            Task.Delay(1000 * (attempt+1)).ContinueWithOnMainThread(_ => Attempt(attempt+1));
        }
        Attempt(0);
    }
}
