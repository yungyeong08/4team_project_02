using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Tracks purchased fishing rods without duplicating shop or wallet responsibilities.</summary>
public sealed class FishingRodInventory : MonoBehaviour
{
    [SerializeField] private List<string> ownedRodIds = new();
    [SerializeField] private string equippedRodId = "";

    public int OwnedRodCount => ownedRodIds.Count;
    public string EquippedRodId => equippedRodId;
    public event Action InventoryChanged;

    public IReadOnlyList<string> OwnedRodIds => ownedRodIds;

    public bool Owns(string rodId)
    {
        return !string.IsNullOrWhiteSpace(rodId) && ownedRodIds.Contains(rodId);
    }

    public bool TryAddAndEquip(string rodId)
    {
        if (string.IsNullOrWhiteSpace(rodId) || Owns(rodId)) return false;
        ownedRodIds.Add(rodId);
        equippedRodId = rodId;
        InventoryChanged?.Invoke();
        return true;
    }

    public bool TryEquip(string rodId)
    {
        if (!Owns(rodId) || equippedRodId == rodId) return false;
        equippedRodId = rodId;
        InventoryChanged?.Invoke();
        return true;
    }

    public void EnsureDefaultRod()
    {
        if (!Owns(FishingRodCatalog.DefaultRodId)) ownedRodIds.Insert(0, FishingRodCatalog.DefaultRodId);
        if (string.IsNullOrWhiteSpace(equippedRodId) || !Owns(equippedRodId))
            equippedRodId = FishingRodCatalog.DefaultRodId;
    }

    public List<string> CreateSnapshot() => new(ownedRodIds);

    public void Restore(IEnumerable<string> savedRodIds, string savedEquippedRodId)
    {
        ownedRodIds.Clear();
        if (savedRodIds != null)
        {
            foreach (string rodId in savedRodIds)
                if (!string.IsNullOrWhiteSpace(rodId) && !ownedRodIds.Contains(rodId)) ownedRodIds.Add(rodId);
        }
        equippedRodId = savedEquippedRodId ?? "";
        EnsureDefaultRod();
        InventoryChanged?.Invoke();
    }

    public FishingRodDefinition GetEquipped(IReadOnlyList<FishingRodDefinition> catalog)
    {
        if (catalog == null) return null;
        for (int i = 0; i < catalog.Count; i++)
            if (catalog[i] != null && catalog[i].Id == equippedRodId) return catalog[i];
        return null;
    }
}
