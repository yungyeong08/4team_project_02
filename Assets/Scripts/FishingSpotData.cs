using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class FishRarityWeight
{
    public FishRarity Rarity;
    [Min(0f)] public float Weight;

    public FishRarityWeight(FishRarity rarity, float weight)
    {
        Rarity = rarity;
        Weight = weight;
    }
}

[Serializable]
public sealed class FishPoolEntry
{
    [Tooltip("FishDefinition.Id. Display names may change without breaking this reference.")]
    public string FishId;
    [Min(0f)] public float Weight;

    public FishPoolEntry() { }

    public FishPoolEntry(string fishId, float weight)
    {
        FishId = fishId;
        Weight = weight;
    }
}

/// <summary>Inspector-editable position, encounter table, and difficulty tuning for one fishing area.</summary>
[Serializable]
public sealed class FishingSpotDefinition
{
    public string Id;
    public string DisplayName;
    [Tooltip("Only enabled areas are built and checked during play.")]
    public bool Enabled = true;
    public bool InitiallyUnlocked = true;
    [Tooltip("Optional completed quest ID required to unlock this area. Leave empty to keep it manually unlockable.")]
    public string RequiredQuestId;
    public Vector2 PlayerSpawnPoint;
    public Vector2 WaterCenter;
    public Vector2 WaterSize;
    public Vector2 ShorePosition;
    public Vector2 BobberPosition;
    [Min(.5f)] public float InteractionRange = 1.6f;
    [Range(.5f, 2f)] public float DifficultyMultiplier = 1f;
    [Tooltip("Optional exact fish IDs for this area. Empty preserves the existing rarity-table behavior.")]
    public List<string> FishPoolIds = new();
    [Tooltip("Exact species weights for this area. When populated, these override FishPoolIds and RarityWeights.")]
    public List<FishPoolEntry> FishPool = new();
    public List<FishRarityWeight> RarityWeights = new();

    public FishingSpotDefinition() { }

    public FishingSpotDefinition(
        string id,
        string displayName,
        Vector2 waterCenter,
        Vector2 waterSize,
        Vector2 shorePosition,
        Vector2 bobberPosition,
        float interactionRange,
        float difficultyMultiplier,
        params FishRarityWeight[] rarityWeights)
    {
        Id = id;
        DisplayName = displayName;
        WaterCenter = waterCenter;
        WaterSize = waterSize;
        ShorePosition = shorePosition;
        BobberPosition = bobberPosition;
        InteractionRange = interactionRange;
        DifficultyMultiplier = difficultyMultiplier;
        PlayerSpawnPoint = shorePosition;
        RarityWeights = new List<FishRarityWeight>(rarityWeights);
    }

    public float GetWeight(FishRarity rarity)
    {
        FishRarityWeight entry = RarityWeights?.Find(candidate => candidate != null && candidate.Rarity == rarity);
        return entry != null ? Mathf.Max(0f, entry.Weight) : 0f;
    }

    public FishDefinition SelectFish(IReadOnlyList<FishDefinition> catalog)
    {
        if (catalog == null || catalog.Count == 0) return null;

        if (FishPool != null && FishPool.Count > 0)
        {
            float exactTotal = 0f;
            for (int i = 0; i < catalog.Count; i++) exactTotal += GetExactSpeciesWeight(catalog[i]);
            if (exactTotal > 0f)
            {
                float exactRoll = UnityEngine.Random.value * exactTotal;
                for (int i = 0; i < catalog.Count; i++)
                {
                    FishDefinition fish = catalog[i];
                    exactRoll -= GetExactSpeciesWeight(fish);
                    if (exactRoll <= 0f && fish != null) return fish;
                }
            }
        }

        float totalWeight = 0f;
        for (int i = 0; i < catalog.Count; i++) totalWeight += GetSpeciesWeight(catalog, i);
        if (totalWeight <= 0f)
        {
            int start = UnityEngine.Random.Range(0, catalog.Count);
            for (int offset = 0; offset < catalog.Count; offset++)
            {
                FishDefinition fallback = catalog[(start + offset) % catalog.Count];
                if (fallback != null && (FishPoolIds == null || FishPoolIds.Count == 0 || FishPoolIds.Contains(fallback.Id)))
                    return fallback;
            }
            return null;
        }

        float roll = UnityEngine.Random.value * totalWeight;
        for (int i = 0; i < catalog.Count; i++)
        {
            roll -= GetSpeciesWeight(catalog, i);
            if (roll <= 0f && catalog[i] != null) return catalog[i];
        }

        for (int i = catalog.Count - 1; i >= 0; i--)
            if (catalog[i] != null && GetSpeciesWeight(catalog, i) > 0f) return catalog[i];
        return null;
    }

    public float GetFishWeight(string fishId)
    {
        if (string.IsNullOrWhiteSpace(fishId) || FishPool == null) return 0f;
        FishPoolEntry entry = FishPool.Find(candidate => candidate != null && candidate.FishId == fishId);
        return entry != null ? Mathf.Max(0f, entry.Weight) : 0f;
    }

    private float GetExactSpeciesWeight(FishDefinition fish)
        => fish != null ? GetFishWeight(fish.Id) : 0f;

    private float GetSpeciesWeight(IReadOnlyList<FishDefinition> catalog, int index)
    {
        FishDefinition fish = catalog[index];
        if (fish == null) return 0f;
        if (FishPoolIds != null && FishPoolIds.Count > 0 && !FishPoolIds.Contains(fish.Id)) return 0f;
        int speciesInRarity = 0;
        for (int i = 0; i < catalog.Count; i++)
            if (catalog[i] != null && catalog[i].Rarity == fish.Rarity &&
                (FishPoolIds == null || FishPoolIds.Count == 0 || FishPoolIds.Contains(catalog[i].Id))) speciesInRarity++;

        return speciesInRarity > 0 ? GetWeight(fish.Rarity) / speciesInRarity : 0f;
    }
}

