using System;
using UnityEngine;

[Serializable]
public sealed class DebtSettings
{
    [Min(0)] public int InitialDebt = 30000;
}

/// <summary>Owns debt state and reports only money that was actually paid through the wallet.</summary>
public sealed class PlayerDebt : MonoBehaviour
{
    [SerializeField, Min(0)] private int remainingDebt;
    [SerializeField, Min(0)] private int lifetimeRepaid;
    [SerializeField, Min(0)] private int initialDebt;
    [SerializeField] private bool fullyRepaid;
    [SerializeField] private bool gameCleared;

    public int InitialDebt => initialDebt;
    public int RemainingDebt => remainingDebt;
    public int LifetimeRepaid => lifetimeRepaid;
    public bool IsFullyRepaid => fullyRepaid;
    public bool IsGameCleared => gameCleared;

    public event Action<int> DebtRepaid;
    public event Action DebtChanged;
    public event Action DebtFullyRepaid;
    public event Action GameCleared;

    public void InitializeNewGame(int initialDebt)
    {
        this.initialDebt = Mathf.Max(0, initialDebt);
        remainingDebt = this.initialDebt;
        lifetimeRepaid = 0;
        fullyRepaid = remainingDebt == 0;
        gameCleared = fullyRepaid;
        DebtChanged?.Invoke();
    }

    public void Restore(int savedInitialDebt, int savedRemainingDebt, int savedLifetimeRepaid,
        bool savedFullyRepaid, bool savedGameCleared)
    {
        initialDebt = Mathf.Max(0, savedInitialDebt);
        lifetimeRepaid = Mathf.Clamp(savedLifetimeRepaid, 0, initialDebt);
        remainingDebt = Mathf.Clamp(savedRemainingDebt, 0, initialDebt);
        // The initial/remaining relationship is authoritative and repairs partial or old saves safely.
        int derivedRepaid = Mathf.Max(0, initialDebt - remainingDebt);
        lifetimeRepaid = Mathf.Max(lifetimeRepaid, derivedRepaid);
        remainingDebt = Mathf.Max(0, initialDebt - lifetimeRepaid);
        fullyRepaid = savedFullyRepaid || remainingDebt == 0;
        gameCleared = savedGameCleared && fullyRepaid;
        DebtChanged?.Invoke();
    }

    public void Restore(int savedRemainingDebt, int savedLifetimeRepaid)
        => Restore(Mathf.Max(savedRemainingDebt + savedLifetimeRepaid, savedRemainingDebt),
            savedRemainingDebt, savedLifetimeRepaid, savedRemainingDebt <= 0, savedRemainingDebt <= 0);

    public int TryRepay(PlayerWallet wallet, int requestedAmount)
    {
        if (wallet == null || requestedAmount <= 0 || remainingDebt <= 0) return 0;

        int paidAmount = Mathf.Min(requestedAmount, remainingDebt);
        if (!wallet.TrySpend(paidAmount)) return 0;

        remainingDebt -= paidAmount;
        lifetimeRepaid = Mathf.Min(initialDebt, (int)Math.Min(int.MaxValue, (long)lifetimeRepaid + paidAmount));
        DebtRepaid?.Invoke(paidAmount);
        if (remainingDebt <= 0 && !fullyRepaid)
        {
            remainingDebt = 0;
            fullyRepaid = true;
            DebtFullyRepaid?.Invoke();
        }
        if (fullyRepaid && !gameCleared)
        {
            gameCleared = true;
            GameCleared?.Invoke();
        }
        DebtChanged?.Invoke();
        return paidAmount;
    }
}
