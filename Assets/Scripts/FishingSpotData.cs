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

/// <summary>Inspector-editable position, encounter table, and difficulty tuning for one fishing area.</summary>
[Serializable]
public sealed class FishingSpotDefinition
{
    public string Id;
    public string DisplayName;
    public Vector2 WaterCenter;
    public Vector2 WaterSize;
    public Vector2 ShorePosition;
    public Vector2 BobberPosition;
    [Min(.5f)] public float InteractionRange = 1.6f;
    [Range(.5f, 2f)] public float DifficultyMultiplier = 1f;
    public List<FishRarityWeight> RarityWeights = new();

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

        float totalWeight = 0f;
        for (int i = 0; i < catalog.Count; i++) totalWeight += GetSpeciesWeight(catalog, i);
        if (totalWeight <= 0f)
        {
            int start = UnityEngine.Random.Range(0, catalog.Count);
            for (int offset = 0; offset < catalog.Count; offset++)
            {
                FishDefinition fallback = catalog[(start + offset) % catalog.Count];
                if (fallback != null) return fallback;
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

    private float GetSpeciesWeight(IReadOnlyList<FishDefinition> catalog, int index)
    {
        FishDefinition fish = catalog[index];
        if (fish == null) return 0f;
        int speciesInRarity = 0;
        for (int i = 0; i < catalog.Count; i++)
            if (catalog[i] != null && catalog[i].Rarity == fish.Rarity) speciesInRarity++;

        return speciesInRarity > 0 ? GetWeight(fish.Rarity) / speciesInRarity : 0f;
    }
}

