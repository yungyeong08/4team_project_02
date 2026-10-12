using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Inspector-editable rod data. Replace Icon later without changing gameplay code.</summary>
[Serializable]
public sealed class FishingRodDefinition
{
    public string Id;
    public string DisplayName;
    public FishRarity Rarity;
    [Min(0)] public int Price;
    public Texture2D Icon;
    [Range(.8f, 1.6f)] public float CaptureZoneMultiplier = 1f;
    [Range(.55f, 1.1f)] public float FishSpeedMultiplier = 1f;
    [Range(.8f, 1.6f)] public float CatchRateMultiplier = 1f;

    public FishingRodDefinition() { }

    public FishingRodDefinition(
        string id, string displayName, FishRarity rarity, int price,
        float captureZoneMultiplier, float fishSpeedMultiplier, float catchRateMultiplier)
    {
        Id = id;
        DisplayName = displayName;
        Rarity = rarity;
        Price = price;
        CaptureZoneMultiplier = captureZoneMultiplier;
        FishSpeedMultiplier = fishSpeedMultiplier;
        CatchRateMultiplier = catchRateMultiplier;
    }

    public string RarityName => FishDataPresentation.GetRarityName(Rarity);
    public Color RarityColor => FishDataPresentation.GetRarityColor(Rarity);

    public string StatSummary =>
        $"포획 바 +{Mathf.RoundToInt((CaptureZoneMultiplier - 1f) * 100f)}%  ·  " +
        $"물고기 속도 {Mathf.RoundToInt(FishSpeedMultiplier * 100f)}%  ·  " +
        $"포획 속도 +{Mathf.RoundToInt((CatchRateMultiplier - 1f) * 100f)}%";
}

public static class FishingRodCatalog
{
    public const string DefaultRodId = "basic_rod";

    public static List<FishingRodDefinition> CreateDefault()
    {
        return new List<FishingRodDefinition>
        {
            new(DefaultRodId, "낡은 낚싯대", FishRarity.Common, 0, 1f, 1f, 1f),
            new("sturdy_rod", "튼튼한 낚싯대", FishRarity.Uncommon, 500, 1.08f, .96f, 1.07f),
            new("river_rod", "강철 낚싯대", FishRarity.Rare, 1400, 1.16f, .91f, 1.14f),
            new("master_rod", "장인의 낚싯대", FishRarity.Epic, 3000, 1.24f, .86f, 1.22f),
            new("legend_rod", "별빛 낚싯대", FishRarity.Legendary, 6500, 1.32f, .81f, 1.30f)
        };
    }
}
