using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared map data. Replace this asset later without changing gameplay code.</summary>
[CreateAssetMenu(fileName = "FishingMapConfig", menuName = "Fishing/Fishing Map Config")]
public sealed class FishingMapConfig : ScriptableObject
{
    public Vector2 MapSize = new(44f, 30f);
    public Vector2 PlayerStart = new(0f, -10f);
    public Vector2 FishingShopPosition = new(3.2f, -8f);
    public Vector2 FishMarketPosition = new(-3.2f, -8f);
    public List<FishingSpotDefinition> FishingSpots = new();

    public void ApplyDefaults()
    {
        MapSize = new Vector2(44f, 30f);
        PlayerStart = new Vector2(0f, -10f);
        FishingShopPosition = new Vector2(3.2f, -8f);
        FishMarketPosition = new Vector2(-3.2f, -8f);
        FishingSpots = CreateDefaultSpots();
    }

    public static List<FishingSpotDefinition> CreateDefaultSpots()
    {
        return new List<FishingSpotDefinition>
        {
            new(
                "village_pond", "마을 연못 · 초급",
                new Vector2(-9f, -8f), new Vector2(7f, 5f),
                new Vector2(-5.15f, -8f), new Vector2(-6.25f, -8f),
                1.65f, .9f,
                new FishRarityWeight(FishRarity.Common, 70f),
                new FishRarityWeight(FishRarity.Uncommon, 25f),
                new FishRarityWeight(FishRarity.Rare, 5f),
                new FishRarityWeight(FishRarity.Epic, 0f),
                new FishRarityWeight(FishRarity.Legendary, 0f)),
            new(
                "forest_lake", "솔바람 호수 · 중급",
                new Vector2(10f, 1f), new Vector2(10f, 7f),
                new Vector2(4.65f, 1f), new Vector2(6.15f, 1f),
                1.7f, 1.08f,
                new FishRarityWeight(FishRarity.Common, 15f),
                new FishRarityWeight(FishRarity.Uncommon, 45f),
                new FishRarityWeight(FishRarity.Rare, 32f),
                new FishRarityWeight(FishRarity.Epic, 8f),
                new FishRarityWeight(FishRarity.Legendary, 0f)),
            new(
                "ancient_lake", "고요한 심연 · 고급",
                new Vector2(-10f, 8.5f), new Vector2(11f, 7f),
                new Vector2(-10f, 4.65f), new Vector2(-10f, 6.15f),
                1.75f, 1.25f,
                new FishRarityWeight(FishRarity.Common, 0f),
                new FishRarityWeight(FishRarity.Uncommon, 12f),
                new FishRarityWeight(FishRarity.Rare, 43f),
                new FishRarityWeight(FishRarity.Epic, 38f),
                new FishRarityWeight(FishRarity.Legendary, 7f))
        };
    }

    public static FishingMapConfig CreateRuntimeDefault()
    {
        FishingMapConfig config = CreateInstance<FishingMapConfig>();
        config.ApplyDefaults();
        return config;
    }
}
