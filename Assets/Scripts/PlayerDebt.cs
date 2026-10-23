using System;
using UnityEngine;

[Serializable]
public sealed class DebtSettings
{
    [Min(0)] public int InitialDebt = 5000;
}

/// <summary>Owns debt state and reports only money that was actually paid through the wallet.</summary>
public sealed class PlayerDebt : MonoBehaviour
{
    [SerializeField, Min(0)] private int remainingDebt;
    [SerializeField, Min(0)] private int lifetimeRepaid;

    public int RemainingDebt => remainingDebt;
    public int LifetimeRepaid => lifetimeRepaid;
    public bool IsFullyRepaid => remainingDebt <= 0;

    public event Action<int> DebtRepaid;
    public event Action DebtChanged;

    public void InitializeNewGame(int initialDebt)
    {
        remainingDebt = Mathf.Max(0, initialDebt);
        lifetimeRepaid = 0;
        DebtChanged?.Invoke();
    }

    public void Restore(int savedRemainingDebt, int savedLifetimeRepaid)
    {
        remainingDebt = Mathf.Max(0, savedRemainingDebt);
        lifetimeRepaid = Mathf.Max(0, savedLifetimeRepaid);
        DebtChanged?.Invoke();
    }

    public int TryRepay(PlayerWallet wallet, int requestedAmount)
    {
        if (wallet == null || requestedAmount <= 0 || remainingDebt <= 0) return 0;

        int paidAmount = Mathf.Min(requestedAmount, remainingDebt);
        if (!wallet.TrySpend(paidAmount)) return 0;

        remainingDebt -= paidAmount;
        lifetimeRepaid = (int)Math.Min(int.MaxValue, (long)lifetimeRepaid + paidAmount);
        DebtRepaid?.Invoke(paidAmount);
        DebtChanged?.Invoke();
        return paidAmount;
    }
}
