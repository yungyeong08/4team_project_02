using System;
using System.Collections.Generic;
using UnityEngine;

public enum FishRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

/// <summary>One fish species and every value that drives catching and selling it.</summary>
[Serializable]
public sealed class FishDefinition
{
    public string Id;
    public string DisplayName;
    public Texture2D Icon;
    public FishRarity Rarity;
    [Min(0)] public int SalePrice;
    [Range(.08f, .4f)] public float ReelZoneSize;
    [Min(0f)] public float MovementAcceleration;
    [Min(0f)] public float MaxSpeed;
    [Min(.1f)] public float DirectionChangeInterval;
    [Min(0f)] public float DirectionImpulse;
    [Min(0f)] public float CatchRate;
    [Min(0f)] public float EscapeRate;

    public FishDefinition(
        string id, string displayName, Texture2D icon, FishRarity rarity, int salePrice,
        float reelZoneSize, float movementAcceleration, float maxSpeed,
        float directionChangeInterval, float directionImpulse, float catchRate, float escapeRate)
    {
        Id = id;
        DisplayName = displayName;
        Icon = icon;
        Rarity = rarity;
        SalePrice = salePrice;
        ReelZoneSize = reelZoneSize;
        MovementAcceleration = movementAcceleration;
        MaxSpeed = maxSpeed;
        DirectionChangeInterval = directionChangeInterval;
        DirectionImpulse = directionImpulse;
        CatchRate = catchRate;
        EscapeRate = escapeRate;
    }

    public string RarityName => Rarity switch
    {
        FishRarity.Common => "일반",
        FishRarity.Uncommon => "고급",
        FishRarity.Rare => "희귀",
        FishRarity.Epic => "영웅",
        FishRarity.Legendary => "전설",
        _ => Rarity.ToString()
    };
}

/// <summary>Builds the catalog from fish textures that already exist in Resources.</summary>
public static class FishCatalog
{
    public static List<FishDefinition> CreateDefault()
    {
        var carp = Resources.Load<Texture2D>("Sprites/Fish/common_carp");
        var salmon = Resources.Load<Texture2D>("Sprites/Fish/atlantic_salmon");
        var pike = Resources.Load<Texture2D>("Sprites/Fish/northern_pike");

        SetPixelFilter(carp);
        SetPixelFilter(salmon);
        SetPixelFilter(pike);

        return new List<FishDefinition>
        {
            new("common_carp", "잉어", carp, FishRarity.Common, 200,
                .28f, .16f, .24f, 1.8f, .08f, .34f, .12f),
            new("atlantic_salmon", "대서양 연어", salmon, FishRarity.Uncommon, 450,
                .22f, .28f, .36f, 1.15f, .15f, .27f, .18f),
            new("northern_pike", "노던 파이크", pike, FishRarity.Rare, 900,
                .16f, .43f, .50f, .68f, .24f, .21f, .25f),
            // Temporary high-rarity entries reuse existing art until dedicated sprites arrive.
            new("golden_salmon", "황금 연어", salmon, FishRarity.Epic, 1800,
                .15f, .48f, .56f, .55f, .26f, .24f, .25f),
            new("ancient_pike", "고대 파이크", pike, FishRarity.Legendary, 3200,
                .12f, .60f, .68f, .42f, .34f, .20f, .30f)
        };
    }

    private static void SetPixelFilter(Texture2D texture)
    {
        if (texture != null) texture.filterMode = FilterMode.Point;
    }
}
