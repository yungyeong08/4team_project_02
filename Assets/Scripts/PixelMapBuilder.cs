using System.Collections.Generic;
using UnityEngine;

/// <summary>Builds the current starter area while retaining future spot data in the map config.</summary>
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
        else RefreshSceneReferences();
    }

    public void EnsureDecorations() => Build();

    public void Build()
    {
        ClearGeneratedChildren();
        RefreshFishingSpots();

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

    /// <summary>
    /// Reconnects gameplay references to the editable objects already saved in the scene.
    /// This deliberately does not rebuild the hierarchy, so manual Scene-view changes survive Play Mode.
    /// </summary>
    public void RefreshSceneReferences()
    {
        RefreshFishingSpots();
        FishingShop = FindChildByName("Fishing Shop");
        FishMarket = FindChildByName("Fish Market");
    }

    private void RefreshFishingSpots()
    {
        runtimeSpots.Clear();
        if (Config.FishingSpots.Count > 0 && Config.FishingSpots[0] != null)
            runtimeSpots.Add(Config.FishingSpots[0]);
    }

    private Transform FindChildByName(string objectName)
    {
        Transform[] descendants = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            if (descendants[i] != transform && descendants[i].name == objectName)
                return descendants[i];
        }
        return null;
    }

    public FishingSpotDefinition GetNearestFishingSpot(Vector2 playerPosition)
    {
        FishingSpotDefinition nearest = null;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < runtimeSpots.Count; i++)
        {
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
        CreateRect(parent, "Village Entrance Path", new Vector2(-9f, -5.7f), new Vector2(2f, 6.6f), pathColor, -8, false);
        CreateRect(parent, "Beginner Pond Trail", new Vector2(-4.1f, -2.5f), new Vector2(11.8f, 1.6f), pathColor, -8, false);
        CreateRect(parent, "Pond Approach", new Vector2(.55f, -.5f), new Vector2(1.7f, 5.5f), pathColor, -8, false);
        CreateRect(parent, "Market Lane", new Vector2(3.75f, 1.2f), new Vector2(6.5f, 1.6f), pathColor, -8, false);
        CreateRect(parent, "Market Square", new Vector2(7f, 1.2f), new Vector2(3.5f, 3.2f), new Color(.57f, .49f, .30f), -7, false);
        CreateRect(parent, "Rod Shop Lane", new Vector2(7.4f, -2.1f), new Vector2(1.7f, 5.1f), pathColor, -8, false);
        CreateRect(parent, "Rod Shop Apron", new Vector2(8f, -5.1f), new Vector2(3.5f, 2.3f), new Color(.57f, .49f, .30f), -7, false);
    }

    private void CreateFishingArea(Transform waterParent, Transform gameplayParent, FishingSpotDefinition spot, int index)
    {
        CreateRect(waterParent, spot.DisplayName + " Shore", spot.WaterCenter,
            spot.WaterSize + new Vector2(.55f, .55f), new Color(.78f, .72f, .45f), -7, false);
        if (!CreateWaterTileGrid(waterParent, spot.DisplayName, spot.WaterCenter, spot.WaterSize, -6))
        {
            Color color = index == 2 ? deepWaterColor : waterColor;
            CreateRect(waterParent, spot.DisplayName, spot.WaterCenter, spot.WaterSize, color, -6, true);
        }
        else
        {
            CreateInvisibleCollider(waterParent, spot.DisplayName + " Water Blocker", spot.WaterCenter, spot.WaterSize);
        }

        Vector2 dockDirection = (spot.BobberPosition - spot.ShorePosition).normalized;
        bool horizontal = Mathf.Abs(dockDirection.x) > Mathf.Abs(dockDirection.y);
        Vector2 dockSize = horizontal ? new Vector2(1.15f, .72f) : new Vector2(.72f, 1.15f);
        CreateRect(gameplayParent, spot.DisplayName + " Dock", Vector2.Lerp(spot.ShorePosition, spot.BobberPosition, .28f),
            dockSize, new Color(.48f, .29f, .15f), 0, false);

        GameObject marker = CreateRect(gameplayParent, spot.DisplayName + " Fishing Marker",
            spot.ShorePosition + new Vector2(0f, .18f), new Vector2(.34f, .34f),
            new Color(.75f, .94f, .40f),
            1, false);
        marker.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
    }

    private void CreateDecorations(Transform parent)
    {
        Texture2D treeTexture = Resources.Load<Texture2D>("Sprites/Environment/Tree1");
        Texture2D grassTexture = Resources.Load<Texture2D>("Sprites/Environment/grass1");
        Texture2D flowerTexture = Resources.Load<Texture2D>("Sprites/Environment/flower");
        if (treeTexture != null) treeTexture.filterMode = FilterMode.Point;
        if (grassTexture != null) grassTexture.filterMode = FilterMode.Point;
        if (flowerTexture != null) flowerTexture.filterMode = FilterMode.Point;

        Vector2[] treePositions =
        {
            new(-11f,-8f), new(-11f,-4.5f), new(-11f,0f), new(-10.5f,5f),
            new(-8f,7.5f), new(-4.5f,7.7f), new(-.8f,7.5f), new(3f,7.7f), new(7f,7.5f),
            new(10.8f,6f), new(11f,2f), new(11f,-2f), new(11f,-6.5f),
            new(8f,-8f), new(3f,-7.8f), new(-3f,-7.8f), new(-7f,-8.2f)
        };
        if (treeTexture != null)
            foreach (Vector2 position in treePositions) CreateSpriteDecoration(parent, "Tree", treeTexture, position, 32f, 2, true);

        Vector2[] grassPositions =
        {
            new(-8f,4f), new(-7.2f,5f), new(-5.8f,5.5f), new(-1.5f,5.3f), new(0f,4.8f),
            new(4f,4.8f), new(5f,5.2f), new(9f,4.5f),
            new(-8f,-4.5f), new(-7f,-5f), new(-4.5f,-5.2f),
            new(2.5f,-4.8f), new(3f,-5.5f), new(5f,-6.8f), new(10f,-3.5f), new(9f,-1f)
        };
        if (grassTexture != null)
            foreach (Vector2 position in grassPositions) CreateSpriteDecoration(parent, "Grass", grassTexture, position, 32f, -1, false);

        if (flowerTexture != null && Config.FlowerPositions != null)
            foreach (Vector2 position in Config.FlowerPositions)
                CreateSpriteDecoration(parent, "Flower Patch", flowerTexture, position, 32f, 0, false);
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

        Texture2D marketTexture = Resources.Load<Texture2D>("Sprites/Environment/fishshop");
        if (marketTexture != null)
        {
            marketTexture.filterMode = FilterMode.Point;
            FishMarket = CreateSpriteDecoration(parent, "Fish Market", marketTexture, Config.FishMarketPosition,
                32f, 2, true, new Vector2(1.7f, .5f), new Vector2(0f, .25f)).transform;
        }
        else FishMarket = CreateRect(parent, "Fish Market", Config.FishMarketPosition, new Vector2(2.1f, 1.45f), new Color(.78f, .34f, .18f), 2, true).transform;
    }

    private bool CreateWaterTileGrid(Transform parent, string areaName, Vector2 center, Vector2 size, int sortingOrder)
    {
        FishingWaterTileSet tiles = Config.WaterTiles;
        if (tiles == null || !tiles.IsComplete) return false;

        int columns = Mathf.Max(3, Mathf.RoundToInt(size.x));
        int rows = Mathf.Max(3, Mathf.RoundToInt(size.y));
        float tileWidth = size.x / columns;
        float tileHeight = size.y / rows;
        float pixelsPerUnit = Mathf.Max(1f, Config.WaterTilePixelsPerUnit);
        float left = center.x - size.x * .5f;
        float bottom = center.y - size.y * .5f;
        var spriteCache = new Dictionary<Texture2D, Sprite>();

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                Texture2D texture = tiles.GetTile(column, row, columns, rows);
                if (!spriteCache.TryGetValue(texture, out Sprite sprite))
                {
                    texture.filterMode = FilterMode.Point;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(.5f, .5f), pixelsPerUnit);
                    sprite.name = texture.name + " Runtime Tile";
                    spriteCache.Add(texture, sprite);
                }

                var tile = new GameObject($"{areaName} Tile {column},{row}");
                tile.transform.SetParent(parent, false);
                tile.transform.position = new Vector3(left + (column + .5f) * tileWidth,
                    bottom + (row + .5f) * tileHeight, 0f);
                float spriteWidth = texture.width / pixelsPerUnit;
                float spriteHeight = texture.height / pixelsPerUnit;
                tile.transform.localScale = new Vector3(tileWidth / spriteWidth, tileHeight / spriteHeight, 1f);
                var renderer = tile.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = sortingOrder;
            }
        }
        return true;
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

    private static GameObject CreateSpriteDecoration(Transform parent, string objectName, Texture2D texture,
        Vector2 position, float pixelsPerUnit, int sortingOrder, bool blocksPlayer,
        Vector2? colliderSize = null, Vector2? colliderOffset = null)
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
            collider.size = colliderSize ?? new Vector2(.65f, .42f);
            collider.offset = colliderOffset ?? new Vector2(0f, .21f);
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
