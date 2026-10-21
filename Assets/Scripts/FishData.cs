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

    public FishDefinition() { }

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

    public string RarityName => FishDataPresentation.GetRarityName(Rarity);
    public Color RarityColor => FishDataPresentation.GetRarityColor(Rarity);
}

public static class FishDataPresentation
{
    public static string GetRarityName(FishRarity rarity) => rarity switch
    {
        FishRarity.Common => "일반",
        FishRarity.Uncommon => "고급",
        FishRarity.Rare => "희귀",
        FishRarity.Epic => "영웅",
        FishRarity.Legendary => "전설",
        _ => rarity.ToString()
    };

    public static Color GetRarityColor(FishRarity rarity) => rarity switch
    {
        FishRarity.Common => new Color(.72f, .76f, .72f),
        FishRarity.Uncommon => new Color(.38f, .85f, .43f),
        FishRarity.Rare => new Color(.35f, .65f, 1f),
        FishRarity.Epic => new Color(.75f, .42f, .95f),
        FishRarity.Legendary => new Color(1f, .72f, .20f),
        _ => Color.white
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
            new("northern_pike", "강꼬치고기", pike, FishRarity.Rare, 900,
                .16f, .43f, .50f, .68f, .24f, .21f, .25f),
            new("golden_salmon", "메기", salmon, FishRarity.Common, 260,
                .26f, .19f, .27f, 1.55f, .10f, .32f, .13f),
            new("ancient_pike", "민물장어", pike, FishRarity.Uncommon, 520,
                .20f, .31f, .39f, 1.0f, .17f, .26f, .19f),
            new("squid", "오징어", null, FishRarity.Rare, 780,
                .18f, .36f, .44f, .85f, .20f, .23f, .22f),
            new("pufferfish", "복어", null, FishRarity.Uncommon, 580,
                .21f, .30f, .38f, 1.10f, .16f, .27f, .18f),
            new("crucian_carp", "붕어", null, FishRarity.Common, 160,
                .30f, .13f, .21f, 2.0f, .07f, .36f, .10f),
            new("swordfish", "황새치", null, FishRarity.Epic, 1900,
                .15f, .50f, .58f, .58f, .29f, .18f, .29f),
            new("bluefin_tuna", "참다랑어", null, FishRarity.Legendary, 3200,
                .13f, .58f, .66f, .44f, .34f, .16f, .34f)
        };
    }

    private static void SetPixelFilter(Texture2D texture)
    {
        if (texture != null) texture.filterMode = FilterMode.Point;
    }
}
