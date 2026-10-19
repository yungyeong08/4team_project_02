using System.Collections.Generic;
using UnityEngine;

/// <summary>Single data source for fish and rod balance. Editable without changing scripts.</summary>
[CreateAssetMenu(fileName = "FishingBalanceConfig", menuName = "Fishing/Balance Config")]
public sealed class FishingBalanceConfig : ScriptableObject
{
    public List<FishDefinition> Fish = new();
    public List<FishingRodDefinition> Rods = new();

    public void ApplyDefaults()
    {
        Fish = FishCatalog.CreateDefault();
        Rods = FishingRodCatalog.CreateDefault();
    }

    public bool EnsureValid()
    {
        bool changed = false;
        if (Fish == null || Fish.Count == 0) { Fish = FishCatalog.CreateDefault(); changed = true; }
        if (Rods == null || Rods.Count == 0) { Rods = FishingRodCatalog.CreateDefault(); changed = true; }
        if (Fish != null)
            foreach (FishDefinition fish in Fish)
                if (fish?.Icon != null) fish.Icon.filterMode = FilterMode.Point;
        if (Rods != null)
            foreach (FishingRodDefinition rod in Rods)
                if (rod?.Icon != null) rod.Icon.filterMode = FilterMode.Point;
        return changed;
    }

    public FishingRodDefinition FindRod(string id)
    {
        return Rods?.Find(rod => rod.Id == id);
    }

    public static FishingBalanceConfig LoadOrCreateRuntimeDefault()
    {
        FishingBalanceConfig config = Resources.Load<FishingBalanceConfig>("Data/FishingBalanceConfig");
        if (config == null)
        {
            config = CreateInstance<FishingBalanceConfig>();
            config.ApplyDefaults();
        }
        else config.EnsureValid();
        return config;
    }
}
