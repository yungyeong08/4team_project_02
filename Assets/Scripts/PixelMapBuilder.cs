using System.Collections.Generic;
using UnityEngine;

/// <summary>Builds a medium-sized, data-driven overworld with three replaceable fishing areas.</summary>
[ExecuteAlways]
public sealed class PixelMapBuilder : MonoBehaviour
{
    private const string ConfigResourcePath = "Data/FishingMapConfig";

    [SerializeField] private FishingMapConfig mapConfig;
    [SerializeField] private Color groundColor = new(.35f, .60f, .24f);
    [SerializeField] private Color pathColor = new(.68f, .57f, .34f);
    [SerializeField] private Color waterColor = new(.12f, .48f, .78f);
    [SerializeField] private Color deepWaterColor = new(.08f, .31f, .61f);

    private readonly List<FishingSpotDefinition> runtimeSpots = new();

    public Vector2 PlayerStart => Config.PlayerStart;
    public Vector2 MapSize => Config.MapSize;
    public IReadOnlyList<FishingSpotDefinition> FishingSpots => runtimeSpots;
    public Transform FishingShop { get; private set; }
    public Transform FishMarket { get; private set; }

    private FishingMapConfig Config
    {
        get
        {
            if (mapConfig == null) mapConfig = Resources.Load<FishingMapConfig>(ConfigResourcePath);
            if (mapConfig == null) mapConfig = FishingMapConfig.CreateRuntimeDefault();
            if (mapConfig.FishingSpots == null || mapConfig.FishingSpots.Count == 0) mapConfig.ApplyDefaults();
            mapConfig.EnsureFirstAreaLayout();
            return mapConfig;
        }
    }

    private void OnEnable()
    {
        if (!Application.isPlaying && transform.childCount == 0) Build();
    }

    public void EnsureDecorations() => Build();

    public void Build()
    {
        ClearGeneratedChildren();
        runtimeSpots.Clear();
        runtimeSpots.AddRange(Config.FishingSpots);

        Transform groundRoot = CreateRoot("01 Ground and Paths");
        Transform waterRoot = CreateRoot("02 Water Areas");
        Transform decorationRoot = CreateRoot("03 Trees and Grass");
        Transform gameplayRoot = CreateRoot("04 Gameplay Objects");

        CreateGroundAndPaths(groundRoot);
        for (int i = 0; i < runtimeSpots.Count; i++) CreateFishingArea(waterRoot, gameplayRoot, runtimeSpots[i], i);
        CreateDecorations(decorationRoot);
        CreateShops(gameplayRoot);
        CreateMapBounds(gameplayRoot);
    }

    public FishingSpotDefinition GetNearestFishingSpot(Vector2 playerPosition)
    {
        FishingSpotDefinition nearest = null;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < runtimeSpots.Count; i++)
        {
            // This milestone intentionally exposes only the beginner lake. Later lakes remain as expansion placeholders.
            if (i > 0) continue;
            FishingSpotDefinition spot = runtimeSpots[i];
            float distance = Vector2.Distance(playerPosition, spot.ShorePosition);
            if (distance <= spot.InteractionRange && distance < nearestDistance)
            {
                nearest = spot;
                nearestDistance = distance;
            }
        }
        return nearest;
    }

    public FishingSpotDefinition GetFishingSpot(string id)
    {
        return runtimeSpots.Find(spot => spot != null && spot.Id == id);
    }

    public FishingSpotDefinition GetNearestLockedFishingSpot(Vector2 playerPosition)
    {
        for (int i = 1; i < runtimeSpots.Count; i++)
        {
            FishingSpotDefinition spot = runtimeSpots[i];
            if (spot != null && Vector2.Distance(playerPosition, spot.ShorePosition) <= spot.InteractionRange + .7f)
                return spot;
        }
        return null;
    }

    private Transform CreateRoot(string objectName)
    {
        var root = new GameObject(objectName);
        root.transform.SetParent(transform, false);
        return root.transform;
    }

    private void CreateGroundAndPaths(Transform parent)
    {
        CreateRect(parent, "Grass Ground", Vector2.zero, Config.MapSize, groundColor, -10, false);
        CreateRect(parent, "Village Road", new Vector2(0f, -8f), new Vector2(14f, 2.2f), pathColor, -8, false);
        CreateRect(parent, "North-South Trail", new Vector2(0f, 2f), new Vector2(2.2f, 22f), pathColor, -8, false);
        CreateRect(parent, "Forest Lake Trail", new Vector2(3f, 1f), new Vector2(6f, 1.7f), pathColor, -8, false);
        CreateRect(parent, "Beginner Pond Trail", new Vector2(-3f, -8f), new Vector2(6f, 1.7f), pathColor, -8, false);
        CreateRect(parent, "Deep Lake Trail", new Vector2(-5f, 4.5f), new Vector2(10f, 1.7f), pathColor, -8, false);
        CreateRect(parent, "Southern Entrance", new Vector2(0f, -13f), new Vector2(2.2f, 8f), pathColor, -8, false);
        CreateRect(parent, "Village Square", new Vector2(0f, -8f), new Vector2(8f, 5f), new Color(.57f, .49f, .30f), -7, false);
        CreateRect(parent, "Market Path", new Vector2(-1.4f, -6.3f), new Vector2(1.5f, 3.3f), pathColor, -8, false);
    }

    private void CreateFishingArea(Transform waterParent, Transform gameplayParent, FishingSpotDefinition spot, int index)
    {
        CreateRect(waterParent, spot.DisplayName + " Shore", spot.WaterCenter,
            spot.WaterSize + new Vector2(.55f, .55f), new Color(.78f, .72f, .45f), -7, false);
        Color color = index == 2 ? deepWaterColor : waterColor;
        CreateRect(waterParent, spot.DisplayName, spot.WaterCenter, spot.WaterSize, color, -6, true);

        Vector2 dockDirection = (spot.BobberPosition - spot.ShorePosition).normalized;
        bool horizontal = Mathf.Abs(dockDirection.x) > Mathf.Abs(dockDirection.y);
        Vector2 dockSize = horizontal ? new Vector2(1.15f, .72f) : new Vector2(.72f, 1.15f);
        bool playable = index == 0;
        CreateRect(gameplayParent, spot.DisplayName + (playable ? " Dock" : " Future Dock"), Vector2.Lerp(spot.ShorePosition, spot.BobberPosition, .28f),
            dockSize, playable ? new Color(.48f, .29f, .15f) : new Color(.28f, .28f, .28f), 0, false);

        GameObject marker = CreateRect(gameplayParent, spot.DisplayName + " Fishing Marker",
            spot.ShorePosition + new Vector2(0f, .18f), new Vector2(.34f, .34f),
            playable ? new Color(.75f, .94f, .40f) : new Color(.38f, .40f, .40f),
            1, false);
        marker.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
    }

    private void CreateDecorations(Transform parent)
    {
        Texture2D treeTexture = Resources.Load<Texture2D>("Sprites/Environment/Tree1");
        Texture2D grassTexture = Resources.Load<Texture2D>("Sprites/Environment/grass1");
        if (treeTexture != null) treeTexture.filterMode = FilterMode.Point;
        if (grassTexture != null) grassTexture.filterMode = FilterMode.Point;

        Vector2[] treePositions =
        {
            new(-20f,-13f), new(-17f,-13f), new(-13f,-13f), new(13f,-13f), new(17f,-13f), new(20f,-13f),
            new(-20f,-4f), new(-18f,0f), new(-20f,5f), new(-19f,11f), new(-15f,13f),
            new(20f,-5f), new(19f,0f), new(20f,6f), new(18f,11f), new(14f,13f),
            new(-4f,11.5f), new(2.8f,11f), new(6f,8f), new(8f,6.5f), new(14.5f,6f),
            new(-15.5f,2.5f), new(-14f,-2f), new(-13f,-5f), new(8f,-7f), new(12f,-7f), new(15f,-9f)
        };
        if (treeTexture != null)
            foreach (Vector2 position in treePositions) CreateSpriteDecoration(parent, "Tree", treeTexture, position, 32f, 2, true);

        Vector2[] grassPositions =
        {
            new(-18f,-9f), new(-15f,-7f), new(-13f,-11f), new(-7f,-12f), new(7f,-12f), new(11f,-10f),
            new(-16f,7f), new(-15f,11f), new(-5f,8f), new(-3f,6f), new(4f,7f), new(7f,10f),
            new(15f,3f), new(16f,-1f), new(15f,-4f), new(4f,-3f), new(-5f,-3f), new(-2f,1f)
        };
        if (grassTexture != null)
            foreach (Vector2 position in grassPositions) CreateSpriteDecoration(parent, "Grass", grassTexture, position, 32f, -1, false);
    }

    private void CreateShops(Transform parent)
    {
        Texture2D shopTexture = Resources.Load<Texture2D>("Sprites/Environment/FishingShop");
        if (shopTexture != null)
        {
            shopTexture.filterMode = FilterMode.Point;
            FishingShop = CreateSpriteDecoration(parent, "Fishing Shop", shopTexture, Config.FishingShopPosition, 32f, 2, true).transform;
        }
        else FishingShop = CreateRect(parent, "Fishing Shop", Config.FishingShopPosition, new Vector2(2f, 1.4f), new Color(.25f, .55f, .86f), 2, true).transform;

        FishMarket = CreateRect(parent, "Fish Market", Config.FishMarketPosition, new Vector2(2.1f, 1.45f), new Color(.78f, .34f, .18f), 2, true).transform;
    }

    private void CreateMapBounds(Transform parent)
    {
        float halfWidth = Config.MapSize.x * .5f;
        float halfHeight = Config.MapSize.y * .5f;
        CreateInvisibleCollider(parent, "North Boundary", new Vector2(0f, halfHeight + .25f), new Vector2(Config.MapSize.x + 1f, .5f));
        CreateInvisibleCollider(parent, "South Boundary", new Vector2(0f, -halfHeight - .25f), new Vector2(Config.MapSize.x + 1f, .5f));
        CreateInvisibleCollider(parent, "West Boundary", new Vector2(-halfWidth - .25f, 0f), new Vector2(.5f, Config.MapSize.y + 1f));
        CreateInvisibleCollider(parent, "East Boundary", new Vector2(halfWidth + .25f, 0f), new Vector2(.5f, Config.MapSize.y + 1f));
    }

    private static GameObject CreateRect(Transform parent, string objectName, Vector2 position, Vector2 size, Color color, int sortingOrder, bool blocksPlayer)
    {
        var rectangle = new GameObject(objectName);
        rectangle.transform.SetParent(parent, false);
        rectangle.transform.position = position;
        rectangle.transform.localScale = new Vector3(size.x, size.y, 1f);
        var renderer = rectangle.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateSolidSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        if (blocksPlayer) rectangle.AddComponent<BoxCollider2D>();
        return rectangle;
    }

    private static GameObject CreateSpriteDecoration(Transform parent, string objectName, Texture2D texture, Vector2 position, float pixelsPerUnit, int sortingOrder, bool blocksPlayer)
    {
        var decoration = new GameObject(objectName);
        decoration.transform.SetParent(parent, false);
        decoration.transform.position = position;
        var renderer = decoration.AddComponent<SpriteRenderer>();
        renderer.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), blocksPlayer ? new Vector2(.5f, 0f) : new Vector2(.5f, .5f), pixelsPerUnit);
        renderer.sortingOrder = sortingOrder;
        if (blocksPlayer)
        {
            var collider = decoration.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(.65f, .42f);
            collider.offset = new Vector2(0f, .21f);
        }
        return decoration;
    }

    private static Sprite CreateSolidSprite()
    {
        var texture = new Texture2D(1, 1) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
    }

    private static void CreateInvisibleCollider(Transform parent, string objectName, Vector2 position, Vector2 size)
    {
        var boundary = new GameObject(objectName);
        boundary.transform.SetParent(parent, false);
        boundary.transform.position = position;
        boundary.AddComponent<BoxCollider2D>().size = size;
    }

    private void ClearGeneratedChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
        FishingShop = null;
        FishMarket = null;
    }
}
