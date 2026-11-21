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
        FishRarity.Epic => "에픽",
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
        // Icons are assigned by FishingBalanceConfig.asset. IDs remain stable so old saves,
        // inventory stacks, encyclopedia entries, sales and achievements keep working.
        return new List<FishDefinition>
        {
            // IDs stay stable for save/inventory/codex compatibility; names follow the current art mapping.
            new("northern_pike", "은빛피라미", null, FishRarity.Common, 70,
                .34f, .12f, .20f, 2.10f, .06f, .38f, .09f),
            new("ancient_pike", "민물장어", null, FishRarity.Uncommon, 220,
                .28f, .20f, .30f, 1.45f, .11f, .32f, .14f),
            new("golden_salmon", "메기", null, FishRarity.Common, 150,
                .33f, .14f, .22f, 1.90f, .07f, .37f, .10f),
            new("atlantic_salmon", "연어", null, FishRarity.Uncommon, 280,
                .27f, .22f, .32f, 1.36f, .12f, .31f, .15f),
            new("common_carp", "붕어", null, FishRarity.Common, 90,
                .35f, .11f, .19f, 2.20f, .05f, .39f, .08f),
            new("squid", "오징어", null, FishRarity.Uncommon, 320,
                .26f, .24f, .34f, 1.27f, .13f, .30f, .16f),
            new("pufferfish", "복어", null, FishRarity.Uncommon, 380,
                .25f, .26f, .36f, 1.20f, .14f, .29f, .17f),
            new("crucian_carp", "넙치", null, FishRarity.Uncommon, 300,
                .27f, .23f, .33f, 1.32f, .13f, .30f, .16f),
            new("swordfish", "황새치", null, FishRarity.Rare, 800,
                .21f, .34f, .45f, .90f, .20f, .25f, .22f),
            new("bluefin_tuna", "참다랑어", null, FishRarity.Rare, 900,
                .20f, .37f, .48f, .82f, .22f, .24f, .24f),
            new("lake_catfish", "상어", null, FishRarity.Epic, 1500,
                .17f, .48f, .58f, .66f, .28f, .20f, .29f),
            new("silk_carp", "가오리", null, FishRarity.Rare, 700,
                .22f, .32f, .42f, .96f, .18f, .26f, .20f),
            new("red_trout", "아귀", null, FishRarity.Rare, 850,
                .20f, .36f, .47f, .85f, .21f, .24f, .23f),
            new("golden_crucian", "해파리", null, FishRarity.Rare, 650,
                .22f, .31f, .41f, 1.00f, .18f, .26f, .20f),
            new("snakehead", "해삼", null, FishRarity.Uncommon, 400,
                .25f, .25f, .35f, 1.24f, .14f, .29f, .17f),
            new("jade_carp", "멍게", null, FishRarity.Uncommon, 450,
                .24f, .27f, .37f, 1.16f, .15f, .28f, .18f),
            new("golden_minnow", "검은곰치", null, FishRarity.Rare, 1000,
                .18f, .42f, .52f, .70f, .25f, .21f, .27f),
            new("sturgeon", "새우", null, FishRarity.Common, 120,
                .32f, .15f, .23f, 1.80f, .08f, .36f, .11f),
            new("moonlight_carp", "성게", null, FishRarity.Rare, 550,
                .21f, .33f, .43f, .92f, .19f, .25f, .21f),
            new("legendary_golden_carp", "바다거북", null, FishRarity.Epic, 1600,
                .16f, .40f, .49f, .55f, .30f, .19f, .31f),
            new("fish_21", "붉은도미", null, FishRarity.Common, 180,
                .32f, .16f, .24f, 1.75f, .08f, .36f, .11f),
            new("fish_22", "블루탱", null, FishRarity.Uncommon, 240,
                .27f, .22f, .32f, 1.35f, .12f, .31f, .15f),
            new("fish_23", "꽃게", null, FishRarity.Uncommon, 260,
                .26f, .24f, .34f, 1.25f, .14f, .30f, .16f),
            new("fish_24", "해마", null, FishRarity.Uncommon, 350,
                .25f, .26f, .35f, 1.18f, .15f, .29f, .17f),
            new("fish_25", "엔젤피시", null, FishRarity.Uncommon, 420,
                .24f, .28f, .38f, 1.10f, .16f, .28f, .18f),
            new("fish_26", "바닷가재", null, FishRarity.Rare, 520,
                .22f, .32f, .42f, .98f, .18f, .26f, .20f),
            new("fish_27", "흰동가리", null, FishRarity.Uncommon, 300,
                .27f, .21f, .31f, 1.40f, .11f, .32f, .14f),
            new("fish_28", "노랑양쥐돔", null, FishRarity.Rare, 600,
                .21f, .35f, .46f, .88f, .21f, .24f, .23f),
            new("fish_29", "대왕고래", null, FishRarity.Epic, 1400,
                .15f, .28f, .38f, 1.15f, .15f, .18f, .32f),
            new("fish_30", "보라베타", null, FishRarity.Rare, 750,
                .19f, .39f, .50f, .76f, .23f, .22f, .25f)
        };
    }
}
