using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared map data. Replace this asset later without changing gameplay code.</summary>
[CreateAssetMenu(fileName = "FishingMapConfig", menuName = "Fishing/Fishing Map Config")]
public sealed class FishingMapConfig : ScriptableObject
{
    public Vector2 MapSize = new(24f, 18f);
    public Vector2 PlayerStart = new(4f, -6f);
    public Vector2 FishingShopPosition = new(7f, -2.5f);
    public Vector2 FishMarketPosition = new(3.5f, -2.5f);
    public List<FishingSpotDefinition> FishingSpots = new();

    public void ApplyDefaults()
    {
        MapSize = new Vector2(24f, 18f);
        PlayerStart = new Vector2(4f, -6f);
        FishingShopPosition = new Vector2(7f, -2.5f);
        FishMarketPosition = new Vector2(3.5f, -2.5f);
        FishingSpots = CreateDefaultSpots();
    }

    public bool EnsureFirstAreaLayout()
    {
        bool changed = false;
        // The first playable milestone uses a compact starter map. Keep the other
        // spot definitions below as future content data without building them yet.
        if (Vector2.Distance(MapSize, new Vector2(44f, 30f)) < .05f)
        {
            MapSize = new Vector2(24f, 18f);
            PlayerStart = new Vector2(4f, -6f);
            FishingShopPosition = new Vector2(7f, -2.5f);
            FishMarketPosition = new Vector2(3.5f, -2.5f);
            changed = true;
        }

        if (FishingSpots != null && FishingSpots.Count > 0 &&
            FishingSpots[0] != null && FishingSpots[0].Id == "village_pond" &&
            Vector2.Distance(FishingSpots[0].WaterCenter, new Vector2(-9f, -8f)) < .05f)
        {
            FishingSpots[0] = CreateDefaultSpots()[0];
            changed = true;
        }

        // Migrate the old test-map shop positions that overlapped the beginner fishing interaction.
        if (Vector2.Distance(FishingShopPosition, new Vector2(3.2f, -8f)) < .05f)
        {
            FishingShopPosition = new Vector2(3f, -7.5f);
            changed = true;
        }
        if (Vector2.Distance(FishMarketPosition, new Vector2(-3.2f, -8f)) < .05f)
        {
            FishMarketPosition = new Vector2(-1.5f, -5.5f);
            changed = true;
        }
        return changed;
    }

    public static List<FishingSpotDefinition> CreateDefaultSpots()
    {
        return new List<FishingSpotDefinition>
        {
            new(
                "village_pond", "마을 연못 · 초급",
                new Vector2(-5.5f, 0f), new Vector2(8f, 6f),
                new Vector2(-1.15f, 0f), new Vector2(-2.25f, 0f),
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
