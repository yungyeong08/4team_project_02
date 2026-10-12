using System;
using UnityEngine;

/// <summary>Owns the player's money balance and validates all spending.</summary>
public sealed class PlayerWallet : MonoBehaviour
{
    [SerializeField, Min(0)] private int balance;

    public int Balance => balance;
    public event Action<int> BalanceChanged;

    public void Initialize(int startingBalance)
    {
        balance = Mathf.Max(0, startingBalance);
        BalanceChanged?.Invoke(balance);
    }

    public bool TrySpend(int amount)
    {
        if (amount < 0 || balance < amount) return false;
        balance -= amount;
        BalanceChanged?.Invoke(balance);
        return true;
    }

    public void AddMoney(int amount)
    {
        if (amount <= 0) return;
        balance = (int)System.Math.Min(int.MaxValue, (long)balance + amount);
        BalanceChanged?.Invoke(balance);
    }
}
