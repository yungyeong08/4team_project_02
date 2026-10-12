using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Tracks caught fish counts. Selling removes fish through this single owner.</summary>
public sealed class FishInventory : MonoBehaviour
{
    [Serializable]
    public sealed class FishProgress
    {
        public string FishId;
        public int Count;
        public int LifetimeCaught;
        public bool Discovered;

        public FishProgress Clone()
        {
            return new FishProgress
            {
                FishId = FishId,
                Count = Count,
                LifetimeCaught = LifetimeCaught,
                Discovered = Discovered
            };
        }
    }

    [SerializeField] private List<FishProgress> fishProgress = new();

    public event Action InventoryChanged;

    public int GetCount(string fishId)
    {
        FishProgress stack = fishProgress.Find(candidate => candidate.FishId == fishId);
        return stack != null ? stack.Count : 0;
    }

    public int GetLifetimeCaught(string fishId)
    {
        FishProgress entry = fishProgress.Find(candidate => candidate.FishId == fishId);
        return entry != null ? entry.LifetimeCaught : 0;
    }

    public bool IsDiscovered(string fishId)
    {
        FishProgress entry = fishProgress.Find(candidate => candidate.FishId == fishId);
        return entry != null && entry.Discovered;
    }

    public void Add(string fishId, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(fishId) || amount <= 0) return;
        FishProgress stack = fishProgress.Find(candidate => candidate.FishId == fishId);
        if (stack == null)
        {
            stack = new FishProgress { FishId = fishId };
            fishProgress.Add(stack);
        }

        stack.Count = (int)Math.Min(int.MaxValue, (long)stack.Count + amount);
        stack.LifetimeCaught = (int)Math.Min(int.MaxValue, (long)stack.LifetimeCaught + amount);
        stack.Discovered = true;
        InventoryChanged?.Invoke();
    }

    public int RemoveAll(string fishId)
    {
        FishProgress stack = fishProgress.Find(candidate => candidate.FishId == fishId);
        if (stack == null || stack.Count <= 0) return 0;

        int removed = stack.Count;
        stack.Count = 0;
        InventoryChanged?.Invoke();
        return removed;
    }

    public List<FishProgress> CreateSnapshot()
    {
        var snapshot = new List<FishProgress>();
        foreach (FishProgress entry in fishProgress)
            if (entry != null && !string.IsNullOrWhiteSpace(entry.FishId)) snapshot.Add(entry.Clone());
        return snapshot;
    }

    public void Restore(IEnumerable<FishProgress> savedProgress)
    {
        fishProgress.Clear();
        if (savedProgress != null)
        {
            foreach (FishProgress saved in savedProgress)
            {
                if (saved == null || string.IsNullOrWhiteSpace(saved.FishId)) continue;
                FishProgress existing = fishProgress.Find(candidate => candidate.FishId == saved.FishId);
                if (existing == null)
                {
                    existing = new FishProgress { FishId = saved.FishId };
                    fishProgress.Add(existing);
                }
                existing.Count = Mathf.Max(existing.Count, Mathf.Max(0, saved.Count));
                existing.LifetimeCaught = Mathf.Max(existing.LifetimeCaught, Mathf.Max(existing.Count, saved.LifetimeCaught));
                existing.Discovered = existing.Discovered || saved.Discovered || existing.LifetimeCaught > 0;
            }
        }
        InventoryChanged?.Invoke();
    }

    public int TotalCount
    {
        get
        {
            int total = 0;
            foreach (FishProgress stack in fishProgress)
            {
                if (stack == null) continue;
                total = (int)Math.Min(int.MaxValue, (long)total + Mathf.Max(0, stack.Count));
            }
            return total;
        }
    }
}
