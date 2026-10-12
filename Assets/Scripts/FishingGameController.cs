using System.Collections.Generic;
using UnityEngine;

/// <summary>Playable fishing prototype: move, face the water, cast, wait for a bite, then keep the fish inside the reel zone.</summary>
public sealed class FishingGameController : MonoBehaviour
{
    private enum State { Explore, Waiting, Reeling }
    private State state;
    private PixelCharacter character;
    private Camera gameCamera;
    private GameObject bobber;
    private List<FishDefinition> fishCatalog;
    private FishDefinition currentFish;
    private FishingSpotDefinition activeFishingSpot;
    private float waitTimer;
    private float reelPosition = .4f;
    private float reelVelocity;
    private float fishPosition = .55f;
    private float fishVelocity;
    private float directionTimer;
    private float progress;
    private float escape;
    private float resultTimer;
    private string message = "이동: WASD 또는 방향키";
    private string result = "";
    private bool initialized;
    private PixelMapBuilder map;
    private FishingShopSystem shopSystem;
    private FishMarketSystem fishMarketSystem;
    private FishInventory fishInventory;
    private GUIStyle overlayStyle;

    private void Awake()
    {
        Initialize();
    }

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (initialized || !Application.isPlaying) return;
        // Clear the temporary Scene-view objects created by the earlier prototype.
        for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
        fishCatalog = FishCatalog.CreateDefault();
        BuildWorld();
        initialized = true;
    }

    private void BuildWorld()
    {
        // Clean up only runtime objects created by this prototype before making a fresh test session.
        foreach (var oldPlayer in Object.FindObjectsByType<PixelCharacter>(FindObjectsSortMode.None)) Destroy(oldPlayer.gameObject);
        foreach (var oldMap in GetComponents<PixelMapBuilder>()) Destroy(oldMap);
        gameCamera = Camera.main;
        if (gameCamera == null)
        {
            var cameraObject = new GameObject("Main Camera");
            gameCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }
        gameCamera.orthographic = true;
        // Show more of the map and keep the 64px character from dominating the screen.
        gameCamera.orthographicSize = 5.2f;
        gameCamera.transform.position = new Vector3(0, 0, -10);
        gameCamera.backgroundColor = new Color(.35f, .60f, .24f);

        map = gameObject.AddComponent<PixelMapBuilder>();
        map.Build();

        var player = new GameObject("Player (Pixel Crawler)");
        player.transform.position = map.PlayerStart;
        character = player.AddComponent<PixelCharacter>();
        character.Setup();
        var wallet = player.AddComponent<PlayerWallet>();
        wallet.Initialize(1000);
        var inventory = player.AddComponent<FishingRodInventory>();
        fishInventory = player.AddComponent<FishInventory>();

        shopSystem = gameObject.AddComponent<FishingShopSystem>();
        shopSystem.Setup(character, map.FishingShop, wallet, inventory);
        fishMarketSystem = gameObject.AddComponent<FishMarketSystem>();
        fishMarketSystem.Setup(character, map.FishMarket, wallet, fishInventory, fishCatalog);

        var follow = gameCamera.gameObject.GetComponent<CameraFollow2D>();
        if (follow == null) follow = gameCamera.gameObject.AddComponent<CameraFollow2D>();
        follow.Target = player.transform;
        follow.SetWorldBounds(map.MapSize);
    }

    private static GameObject CreateRect(string objectName, Vector2 position, Vector2 size, Color color, int sortingOrder)
    {
        var go = new GameObject(objectName);
        go.transform.position = position;
        var renderer = go.AddComponent<SpriteRenderer>();
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white); tex.Apply();
        renderer.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
        renderer.color = color; renderer.sortingOrder = sortingOrder;
        go.transform.localScale = size;
        return go;
    }

    private void Update()
    {
        if (!initialized)
        {
            Initialize();
            if (!initialized) return;
        }

        if (resultTimer > 0f)
        {
            resultTimer -= Time.deltaTime;
            if (resultTimer <= 0f)
            {
                resultTimer = 0f;
                result = "";
            }
        }

        if (state == State.Explore)
        {
            if ((shopSystem != null && shopSystem.IsOpen) || (fishMarketSystem != null && fishMarketSystem.IsOpen)) return;
            if (character != null) character.Move();
            if (shopSystem != null && shopSystem.IsPlayerInRange)
            {
                message = "F - 상점 열기";
                return;
            }
            if (fishMarketSystem != null && fishMarketSystem.IsPlayerInRange)
            {
                message = "F - 물고기 판매";
                return;
            }
            FishingSpotDefinition nearbySpot = character != null ? map.GetNearestFishingSpot(character.transform.position) : null;
            message = nearbySpot != null ? $"{nearbySpot.DisplayName}  •  F 키로 낚시하기" : "이동: WASD 또는 방향키";
            if (nearbySpot != null && Input.GetKeyDown(KeyCode.F)) Cast(nearbySpot);
        }
        else if (state == State.Waiting)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) ResetFishing("낚시를 취소했습니다.");
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0) Bite();
        }
        else if (state == State.Reeling) Reel();
    }

    private void Cast(FishingSpotDefinition spot)
    {
        activeFishingSpot = spot;
        state = State.Waiting; character.SetFishing(true);
        result = "";
        resultTimer = 0f;
        message = $"{spot.DisplayName}에 낚싯줄을 던졌습니다…  (ESC: 취소)";
        bobber = CreateRect("Bobber", spot.BobberPosition, new Vector2(.22f, .22f), new Color(1f, .28f, .2f), 4);
        waitTimer = Random.Range(2f, 6f);
    }

    private void Bite()
    {
        if (fishCatalog == null || fishCatalog.Count == 0)
        {
            ResetFishing("물고기 정보를 불러오지 못했습니다.");
            return;
        }
        state = State.Reeling;
        currentFish = activeFishingSpot != null ? activeFishingSpot.SelectFish(fishCatalog) : fishCatalog[Random.Range(0, fishCatalog.Count)];
        if (currentFish == null)
        {
            ResetFishing("이 낚시터의 물고기 정보를 불러오지 못했습니다.");
            return;
        }
        fishPosition = Random.Range(.15f, .85f); reelPosition = .4f; reelVelocity = 0; fishVelocity = Random.Range(-.15f, .15f);
        directionTimer = Random.Range(AdjustedDirectionInterval * .65f, AdjustedDirectionInterval * 1.35f);
        progress = escape = 0;
        message = $"입질! {currentFish.DisplayName} · {currentFish.RarityName}  |  마우스 왼쪽 또는 스페이스";
        if (bobber != null) bobber.GetComponent<SpriteRenderer>().color = Color.yellow;
    }

    private void Reel()
    {
        float input = (Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space)) ? 1f : -1f;
        float zoneSize = AdjustedReelZoneSize;

        // Keep the original weighty control feel; difficulty now comes from reading the fish,
        // not from an overly sensitive player bar.
        reelVelocity += input * 1.65f * Time.deltaTime;
        reelVelocity *= Mathf.Exp(-4f * Time.deltaTime);
        reelVelocity = Mathf.Clamp(reelVelocity, -.55f, .55f);
        reelPosition = Mathf.Clamp(reelPosition + reelVelocity * Time.deltaTime, 0f, 1f - zoneSize);

        const float fishSpeedScale = .72f;
        fishVelocity += Mathf.Sin(Time.time * 3.1f) * currentFish.MovementAcceleration * EffectiveSpotDifficulty * fishSpeedScale * Time.deltaTime;
        directionTimer -= Time.deltaTime;
        if (directionTimer <= 0f)
        {
            float impulse = currentFish.DirectionImpulse * EffectiveSpotDifficulty * fishSpeedScale;
            fishVelocity += Random.Range(-impulse, impulse);
            directionTimer = Random.Range(AdjustedDirectionInterval * .65f, AdjustedDirectionInterval * 1.35f);
        }
        float maxSpeed = currentFish.MaxSpeed * EffectiveSpotDifficulty * fishSpeedScale;
        fishVelocity = Mathf.Clamp(fishVelocity, -maxSpeed, maxSpeed);
        fishPosition += fishVelocity * Time.deltaTime;
        if (fishPosition < .02f || fishPosition > .98f) fishVelocity *= -1f;
        fishPosition = Mathf.Clamp(fishPosition, .02f, .98f);
        bool aligned = fishPosition >= reelPosition && fishPosition <= reelPosition + zoneSize;
        float difficultyRoot = Mathf.Sqrt(EffectiveSpotDifficulty);
        progress = Mathf.Clamp01(progress + (aligned ? currentFish.CatchRate / difficultyRoot : -.08f) * Time.deltaTime);
        escape = Mathf.Clamp01(escape + (aligned ? -.20f : currentFish.EscapeRate * difficultyRoot) * Time.deltaTime);
        if (progress >= 1) ResetFishing("물고기를 잡았습니다! F 키로 다시 낚시할 수 있습니다.");
        else if (escape >= 1) ResetFishing("물고기가 도망갔습니다. F 키로 다시 시도하세요.");
        else if (Input.GetKeyDown(KeyCode.Escape)) ResetFishing("낚시를 취소했습니다.");
    }

    private void ResetFishing(string newMessage)
    {
        bool caughtFish = state == State.Reeling && progress >= 1f;
        result = caughtFish && currentFish != null ? currentFish.DisplayName : "";
        if (caughtFish && currentFish != null && fishInventory != null) fishInventory.Add(currentFish.Id);
        resultTimer = caughtFish ? 3f : 0f;
        state = State.Explore;
        message = newMessage;
        character.SetFishing(false);
        if (bobber != null) Destroy(bobber);
        bobber = null;
        currentFish = null;
        activeFishingSpot = null;
    }

    private void OnGUI()
    {
        if ((shopSystem != null && (shopSystem.IsOpen || shopSystem.IsPlayerInRange)) ||
            (fishMarketSystem != null && (fishMarketSystem.IsOpen || fishMarketSystem.IsPlayerInRange))) return;

        float uiScale = Mathf.Clamp(Screen.height / 1080f, .8f, 2f);
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
        DrawFishingOverlay(Screen.width / uiScale, Screen.height / uiScale);
        GUI.matrix = previousMatrix;
    }

    private void DrawFishingOverlay(float screenWidth, float screenHeight)
    {
        EnsureOverlayStyle();
        GUI.Box(new Rect(screenWidth / 2 - 360, 18, 720, 46), "");
        GUI.Label(new Rect(screenWidth / 2 - 350, 21, 700, 39), message, overlayStyle);
        if (state != State.Reeling) { if (!string.IsNullOrEmpty(result)) GUI.Label(new Rect(screenWidth/2-240, 72, 480, 38), "획득: " + result, overlayStyle); return; }

        float trackWidth = Mathf.Min(1100f, screenWidth - 100f);
        var track = new Rect((screenWidth - trackWidth) * .5f, screenHeight - 190f, trackWidth, 56f);
        GUI.Box(track, "");
        float zoneSize = currentFish != null ? AdjustedReelZoneSize : .2f;
        Rect captureZone = new(track.x + reelPosition * track.width, track.y + 5, zoneSize * track.width, track.height - 10);
        GUI.color = new Color(1f, 1f, 1f, .72f);
        GUI.DrawTexture(captureZone, Texture2D.whiteTexture);
        GUI.color = Color.white;
        if (currentFish?.Icon != null)
        {
            float fishX = Mathf.Lerp(track.x + 32f, track.xMax - 32f, fishPosition);
            GUI.DrawTexture(new Rect(fishX - 32f, track.y - 18f, 64f, 64f), currentFish.Icon, ScaleMode.ScaleToFit, true);
        }

        float barWidth = track.width * .46f;
        Rect progressBar = new(track.x, track.y + 78f, barWidth, 24f);
        Rect escapeBar = new(track.xMax - barWidth, track.y + 78f, barWidth, 24f);
        GUI.color = new Color(.08f, .10f, .08f, .82f);
        GUI.DrawTexture(progressBar, Texture2D.whiteTexture);
        GUI.DrawTexture(escapeBar, Texture2D.whiteTexture);
        GUI.color = new Color(.30f, .82f, .32f, 1f);
        GUI.DrawTexture(new Rect(progressBar.x, progressBar.y, progressBar.width * progress, progressBar.height), Texture2D.whiteTexture);
        GUI.color = new Color(.90f, .28f, .22f, 1f);
        GUI.DrawTexture(new Rect(escapeBar.xMax - escapeBar.width * escape, escapeBar.y, escapeBar.width * escape, escapeBar.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(progressBar.x, progressBar.y + 28f, progressBar.width, 34f), "포획 " + Mathf.RoundToInt(progress * 100) + "%", overlayStyle);
        GUI.Label(new Rect(escapeBar.x, escapeBar.y + 28f, escapeBar.width, 34f), "도주 " + Mathf.RoundToInt(escape * 100) + "%", overlayStyle);
    }

    private void EnsureOverlayStyle()
    {
        if (overlayStyle != null) return;
        overlayStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 20,
            normal = { textColor = Color.white }
        };
    }

    private float SpotDifficulty => activeFishingSpot != null ? Mathf.Max(.5f, activeFishingSpot.DifficultyMultiplier) : 1f;
    private float EffectiveSpotDifficulty => Mathf.Lerp(1f, SpotDifficulty, .55f);
    private float AdjustedDirectionInterval => currentFish != null ? currentFish.DirectionChangeInterval / EffectiveSpotDifficulty * 1.15f : 1f;
    private float AdjustedReelZoneSize => currentFish != null ? Mathf.Clamp(currentFish.ReelZoneSize * 1.25f / Mathf.Sqrt(EffectiveSpotDifficulty), .18f, .42f) : .24f;
}
