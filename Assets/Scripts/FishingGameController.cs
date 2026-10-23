using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Playable fishing prototype: move, face the water, cast, wait for a bite, then keep the fish inside the reel zone.</summary>
public sealed class FishingGameController : MonoBehaviour
{
    [Header("HUD Style (presentation only)")]
    [SerializeField] private FishingHudSettings hudSettings = new();
    [Header("Opening / temporary audio")]
    [SerializeField] private FishingOpeningSettings openingSettings = new();
    [SerializeField] private FishingAudioSettings audioSettings = new();
    [SerializeField] private DebtSettings debtSettings = new();

    private enum State { Explore, Waiting, Reeling }
    private State state;
    private PixelCharacter character;
    private Camera gameCamera;
    private GameObject bobber;
    private FishingBalanceConfig balanceConfig;
    private List<FishDefinition> fishCatalog;
    private List<FishingRodDefinition> rodCatalog;
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
    private float resultDelayTimer;
    private string locationTitle = "초보자 마을";
    private string actionHint = "WASD 또는 방향키로 이동";
    private bool actionAvailable;
    private FishDefinition resultFish;
    private bool resultSuccess;
    private string resultDetails = "";
    private bool initialized;
    private PixelMapBuilder map;
    private FishingShopSystem shopSystem;
    private FishMarketSystem fishMarketSystem;
    private FishInventory fishInventory;
    private FishingRodInventory rodInventory;
    private PlayerProgressUI progressUI;
    private GameSaveSystem saveSystem;
    private EarlyGameQuestSystem earlyGameProgress;
    private FishingAudioDirector audioDirector;
    private FishingOpeningSequence openingSequence;
    private PlayerDebt playerDebt;
    private Coroutine castSequence;
    private float reelUiReveal;
    private float resultUiReveal;
    private bool firstCatchResult;
    private bool newFishResult;
    private float newFishUiReveal;
    private float actionPromptVisibility;
    private string lastRegionId = "";
    private string regionTitle = "";
    private string regionSubtitle = "";
    private float regionNoticeTimer;
    private const float RegionNoticeDuration = 1.75f;

    public bool IsFishingActive => state != State.Explore;
    public bool IsResultVisible => resultTimer > 0f || resultDelayTimer > 0f;
    public bool IsCinematicActive => openingSequence != null && openingSequence.IsBlocking;
    public string EquippedRodId => rodInventory != null ? rodInventory.EquippedRodId : "";

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
        FishingUiTheme.Configure(hudSettings);
        // Clear the temporary Scene-view objects created by the earlier prototype.
        for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
        balanceConfig = FishingBalanceConfig.LoadOrCreateRuntimeDefault();
        fishCatalog = balanceConfig.Fish;
        rodCatalog = balanceConfig.Rods;
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
        // FishingDemo's camera is generated/maintained separately from StartScene and
        // previously had no listener, so AudioSources played silently.
        if (gameCamera.GetComponent<AudioListener>() == null &&
            Object.FindFirstObjectByType<AudioListener>() == null)
            gameCamera.gameObject.AddComponent<AudioListener>();
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
        wallet.Initialize(100);
        playerDebt = player.AddComponent<PlayerDebt>();
        playerDebt.InitializeNewGame(debtSettings.InitialDebt);
        rodInventory = player.AddComponent<FishingRodInventory>();
        rodInventory.EnsureDefaultRod();
        fishInventory = player.AddComponent<FishInventory>();

        audioDirector = gameObject.AddComponent<FishingAudioDirector>();
        audioDirector.Setup(character, FishingAudioConfig.LoadOrFallback(audioSettings));

        shopSystem = gameObject.AddComponent<FishingShopSystem>();
        shopSystem.Setup(character, map.FishingShop, wallet, rodInventory, rodCatalog);
        shopSystem.SetAudio(audioDirector);
        fishMarketSystem = gameObject.AddComponent<FishMarketSystem>();
        fishMarketSystem.Setup(character, map.FishMarket, wallet, fishInventory, fishCatalog);
        fishMarketSystem.SetAudio(audioDirector);

        earlyGameProgress = gameObject.AddComponent<EarlyGameQuestSystem>();
        earlyGameProgress.Setup(player.transform, wallet, map.GetFishingSpot(EarlyGameQuestSystem.FirstLakeId),
            map.FishMarket, map.FishingShop, playerDebt);
        playerDebt.DebtRepaid += earlyGameProgress.NotifyDebtRepaid;
        earlyGameProgress.QuestCompleted += (_, _) => audioDirector?.PlayQuestCompleteDelayed();
        shopSystem.RodPurchased += earlyGameProgress.NotifyRodPurchased;
        fishMarketSystem.FishSold += (soldCount, _) => earlyGameProgress.NotifyFishSold(soldCount);

        saveSystem = gameObject.AddComponent<GameSaveSystem>();
        saveSystem.Setup(wallet, fishInventory, rodInventory, player.transform, map.PlayerStart, map.MapSize,
            fishCatalog, rodCatalog, earlyGameProgress, playerDebt, debtSettings.InitialDebt);

        progressUI = gameObject.AddComponent<PlayerProgressUI>();
        progressUI.Setup(wallet, fishInventory, rodInventory, fishCatalog, rodCatalog, saveSystem, this,
            shopSystem, fishMarketSystem, earlyGameProgress, playerDebt);
        progressUI.SetAudio(audioDirector);
        earlyGameProgress.SetUiBlocker(() =>
            IsCinematicActive ||
            (progressUI != null && progressUI.IsBlockingGameplay) ||
            (shopSystem != null && shopSystem.IsOpen) ||
            (fishMarketSystem != null && fishMarketSystem.IsOpen) || IsFishingActive || IsResultVisible);
        shopSystem.SetInputBlocker(() => IsCinematicActive || IsFishingActive || IsResultVisible ||
            (progressUI != null && progressUI.IsBlockingGameplay) || (fishMarketSystem != null && fishMarketSystem.IsOpen));
        fishMarketSystem.SetInputBlocker(() => IsCinematicActive || IsFishingActive || IsResultVisible ||
            (progressUI != null && progressUI.IsBlockingGameplay) || (shopSystem != null && shopSystem.IsOpen));

        var follow = gameCamera.gameObject.GetComponent<CameraFollow2D>();
        if (follow == null) follow = gameCamera.gameObject.AddComponent<CameraFollow2D>();
        follow.Target = player.transform;
        follow.SetWorldBounds(map.MapSize);

        openingSequence = gameObject.AddComponent<FishingOpeningSequence>();
        openingSequence.Setup(earlyGameProgress, saveSystem, character, gameCamera, follow,
            map.GetFishingSpot(EarlyGameQuestSystem.FirstLakeId), openingSettings);
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

        if (regionNoticeTimer > 0f) regionNoticeTimer -= Time.unscaledDeltaTime;

        if (resultDelayTimer > 0f)
        {
            resultDelayTimer -= Time.unscaledDeltaTime;
        }
        else if (resultTimer > 0f)
        {
            resultTimer -= Time.deltaTime;
            resultUiReveal = Mathf.MoveTowards(resultUiReveal, 1f, Time.unscaledDeltaTime * 6f);
            if (newFishResult)
                newFishUiReveal = Mathf.MoveTowards(newFishUiReveal, 1f, Time.unscaledDeltaTime / .2f);
            if (resultTimer <= 0f)
            {
                resultTimer = 0f;
                resultFish = null;
                resultDetails = "";
                newFishResult = false;
            }
        }

        if (IsCinematicActive) return;

        if (state == State.Explore)
        {
            if (progressUI != null && progressUI.IsBlockingGameplay) return;
            if ((shopSystem != null && shopSystem.IsOpen) || (fishMarketSystem != null && fishMarketSystem.IsOpen)) return;
            if (character != null) character.Move();
            if (shopSystem != null && shopSystem.IsPlayerInRange)
            {
                actionHint = "";
                actionAvailable = false;
                return;
            }
            if (fishMarketSystem != null && fishMarketSystem.IsPlayerInRange)
            {
                actionHint = "";
                actionAvailable = false;
                return;
            }
            FishingSpotDefinition nearbySpot = character != null ? map.GetNearestFishingSpot(character.transform.position) : null;
            FishingSpotDefinition lockedSpot = nearbySpot == null && character != null
                ? map.GetNearestLockedFishingSpot(character.transform.position)
                : null;
            UpdateRegionNotice(nearbySpot ?? lockedSpot);
            actionHint = nearbySpot != null
                ? "낚시하기"
                : lockedSpot != null ? "아직 들어갈 수 없는 지역입니다" : "";
            actionAvailable = nearbySpot != null;
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
        earlyGameProgress?.NotifyFishingStarted(spot.Id);
        state = State.Waiting; character.SetFishing(true);
        resultFish = null;
        resultDetails = "";
        resultTimer = 0f;
        newFishResult = false;
        newFishUiReveal = 0f;
        locationTitle = spot.DisplayName;
        actionHint = "입질을 기다리는 중…  ·  ESC 취소";
        actionAvailable = false;
        waitTimer = 999f;
        audioDirector?.PlayCast();
        if (castSequence != null) StopCoroutine(castSequence);
        castSequence = StartCoroutine(FinishCast(spot));
    }

    private IEnumerator FinishCast(FishingSpotDefinition spot)
    {
        yield return new WaitForSeconds(.28f);
        if (state != State.Waiting || activeFishingSpot != spot) yield break;
        bobber = CreateRect("Bobber", spot.BobberPosition, new Vector2(.22f, .22f), new Color(1f, .28f, .2f), 4);
        audioDirector?.PlaySplash();
        FishingWorldFx.SpawnRipple(spot.BobberPosition, new Color(.7f, .92f, 1f, .68f), .5f, .72f);
        waitTimer = Random.Range(2f, 6f);
        castSequence = null;
    }

    private void Bite()
    {
        if (fishCatalog == null || fishCatalog.Count == 0)
        {
            ResetFishing("물고기 정보를 불러오지 못했습니다.");
            return;
        }
        state = State.Reeling;
        reelUiReveal = 0f;
        currentFish = activeFishingSpot != null ? activeFishingSpot.SelectFish(fishCatalog) : fishCatalog[Random.Range(0, fishCatalog.Count)];
        if (currentFish == null)
        {
            ResetFishing("이 낚시터의 물고기 정보를 불러오지 못했습니다.");
            return;
        }
        fishPosition = Random.Range(.15f, .85f); reelPosition = .4f; reelVelocity = 0; fishVelocity = Random.Range(-.15f, .15f);
        directionTimer = Random.Range(AdjustedDirectionInterval * .65f, AdjustedDirectionInterval * 1.35f);
        progress = escape = 0;
        locationTitle = $"입질!  {currentFish.DisplayName} · {currentFish.RarityName}";
        actionHint = "SPACE를 누르면 오른쪽 · 놓으면 왼쪽";
        actionAvailable = true;
        if (bobber != null) bobber.GetComponent<SpriteRenderer>().color = Color.yellow;
        audioDirector?.PlayBite();
        if (activeFishingSpot != null) FishingWorldFx.SpawnRipple(activeFishingSpot.BobberPosition,
            new Color(1f, .9f, .35f, .86f), .42f, .82f);
        if (bobber != null) StartCoroutine(ShakeBobber());
    }

    private IEnumerator ShakeBobber()
    {
        if (bobber == null) yield break;
        Vector3 origin = bobber.transform.position;
        float elapsed = 0f;
        while (elapsed < .28f && bobber != null)
        {
            elapsed += Time.deltaTime;
            bobber.transform.position = origin + (Vector3)(Random.insideUnitCircle * .07f);
            yield return null;
        }
        if (bobber != null) bobber.transform.position = origin;
    }

    private void Reel()
    {
        reelUiReveal = Mathf.MoveTowards(reelUiReveal, 1f, Time.deltaTime * 6f);
        bool reelHeld = Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space);
        audioDirector?.SetReeling(reelHeld);
        float input = reelHeld ? 1f : -1f;
        float zoneSize = AdjustedReelZoneSize;
        float rodFishSpeedMultiplier = EquippedRod != null ? EquippedRod.FishSpeedMultiplier : 1f;

        // Keep the original weighty control feel; difficulty now comes from reading the fish,
        // not from an overly sensitive player bar.
        reelVelocity += input * 1.65f * Time.deltaTime;
        reelVelocity *= Mathf.Exp(-4f * Time.deltaTime);
        reelVelocity = Mathf.Clamp(reelVelocity, -.55f, .55f);
        reelPosition = Mathf.Clamp(reelPosition + reelVelocity * Time.deltaTime, 0f, 1f - zoneSize);

        const float fishSpeedScale = .72f;
        fishVelocity += Mathf.Sin(Time.time * 3.1f) * currentFish.MovementAcceleration * EffectiveSpotDifficulty * fishSpeedScale * rodFishSpeedMultiplier * Time.deltaTime;
        directionTimer -= Time.deltaTime;
        if (directionTimer <= 0f)
        {
            float impulse = currentFish.DirectionImpulse * EffectiveSpotDifficulty * fishSpeedScale * rodFishSpeedMultiplier;
            fishVelocity += Random.Range(-impulse, impulse);
            directionTimer = Random.Range(AdjustedDirectionInterval * .65f, AdjustedDirectionInterval * 1.35f);
        }
        float maxSpeed = currentFish.MaxSpeed * EffectiveSpotDifficulty * fishSpeedScale * rodFishSpeedMultiplier;
        fishVelocity = Mathf.Clamp(fishVelocity, -maxSpeed, maxSpeed);
        fishPosition += fishVelocity * Time.deltaTime;
        if (fishPosition < .02f || fishPosition > .98f) fishVelocity *= -1f;
        fishPosition = Mathf.Clamp(fishPosition, .02f, .98f);
        bool aligned = fishPosition >= reelPosition && fishPosition <= reelPosition + zoneSize;
        float difficultyRoot = Mathf.Sqrt(EffectiveSpotDifficulty);
        float rodCatchMultiplier = EquippedRod != null ? EquippedRod.CatchRateMultiplier : 1f;
        progress = Mathf.Clamp01(progress + (aligned ? currentFish.CatchRate * rodCatchMultiplier / difficultyRoot : -.08f) * Time.deltaTime);
        escape = Mathf.Clamp01(escape + (aligned ? -.20f : currentFish.EscapeRate * difficultyRoot) * Time.deltaTime);
        if (progress >= 1) ResetFishing("물고기를 잡았습니다! F 키로 다시 낚시할 수 있습니다.");
        else if (escape >= 1) ResetFishing("물고기가 도망갔습니다. F 키로 다시 시도하세요.");
        else if (Input.GetKeyDown(KeyCode.Escape)) ResetFishing("낚시를 취소했습니다.");
    }

    private void ResetFishing(string newMessage)
    {
        audioDirector?.StopReel();
        bool caughtFish = state == State.Reeling && progress >= 1f;
        bool escapedFish = state == State.Reeling && escape >= 1f;
        resultFish = caughtFish ? currentFish : null;
        resultSuccess = caughtFish;
        firstCatchResult = caughtFish && fishInventory != null && fishInventory.TotalLifetimeCaught == 0;
        newFishResult = false;
        resultDetails = caughtFish && currentFish != null
            ? $"{currentFish.DisplayName} · {currentFish.RarityName} · 판매가 {currentFish.SalePrice:N0} G"
            : escapedFish ? "물고기를 놓쳤습니다. 다시 도전해 보세요." : "";
        if (caughtFish && currentFish != null && fishInventory != null)
        {
            string caughtSpotId = activeFishingSpot != null ? activeFishingSpot.Id : "";
            string usedRodId = rodInventory != null ? rodInventory.EquippedRodId : "";
            newFishResult = fishInventory.Add(currentFish.Id);
            earlyGameProgress?.NotifyFishCaught(caughtSpotId, usedRodId);
            audioDirector?.PlayCatchSequence(newFishResult);
            if (activeFishingSpot != null) FishingWorldFx.SpawnCatchSplash(activeFishingSpot.BobberPosition);
        }
        resultTimer = caughtFish || escapedFish ? 5.5f : 0f;
        resultDelayTimer = caughtFish || escapedFish ? .34f : 0f;
        resultUiReveal = 0f;
        newFishUiReveal = 0f;
        state = State.Explore;
        locationTitle = resultSuccess ? "낚시 성공" : escapedFish ? "낚시 실패" : "낚시 취소";
        actionHint = newMessage;
        actionAvailable = false;
        character?.SetFishing(false);
        reelVelocity = 0f;
        fishVelocity = 0f;
        directionTimer = 0f;
        progress = 0f;
        escape = 0f;
        if (bobber != null) Destroy(bobber);
        bobber = null;
        currentFish = null;
        activeFishingSpot = null;
        if (castSequence != null)
        {
            StopCoroutine(castSequence);
            castSequence = null;
        }
    }

    private void OnGUI()
    {
        if ((progressUI != null && progressUI.IsBlockingGameplay) ||
            (shopSystem != null && shopSystem.IsOpen) ||
            (fishMarketSystem != null && fishMarketSystem.IsOpen) || IsCinematicActive) return;

        float uiScale = Mathf.Clamp(Screen.height / 1080f, .8f, 2f);
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
        DrawFishingOverlay(Screen.width / uiScale, Screen.height / uiScale);
        GUI.matrix = previousMatrix;
    }

    private void DrawFishingOverlay(float screenWidth, float screenHeight)
    {
        FishingUiTheme.Ensure();
        FishingHudSettings hud = FishingUiTheme.Settings;

        if (IsResultVisible)
        {
            if (resultDelayTimer <= 0f && resultTimer > 0f) DrawResultWindow(screenWidth, screenHeight);
            return;
        }

        if (Event.current.type == EventType.Repaint)
            actionPromptVisibility = Mathf.MoveTowards(actionPromptVisibility,
                state == State.Explore && actionAvailable && !string.IsNullOrWhiteSpace(actionHint) ? 1f : 0f,
                Time.unscaledDeltaTime * 6f);

        if (state == State.Explore)
        {
            DrawRegionNotice(screenWidth, hud);
            if (actionPromptVisibility > 0f)
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, actionPromptVisibility);
                var prompt = new Rect(screenWidth * .5f - 158f, screenHeight - 104f, 316f, 56f);
                FishingUiTheme.DrawInteractionPrompt(prompt, "F", actionHint);
                GUI.color = old;
            }
            return;
        }

        if (state == State.Waiting)
        {
            FishingUiTheme.DrawNotice(new Rect(screenWidth * .5f - 215f, screenHeight - 112f, 430f, 48f),
                "입질을 기다리는 중…   ·   ESC 취소", FishingUiTheme.WaterBright);
            return;
        }

        Color previousColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(reelUiReveal));
        float trackWidth = Mathf.Min(1120f, screenWidth - 100f) * Mathf.Lerp(.96f, 1f, reelUiReveal);
        var track = new Rect((screenWidth - trackWidth) * .5f, screenHeight - 188f, trackWidth, 54f);
        Color trackColor = new(.018f, .018f, .022f, .94f);
        Color borderColor = new(.33f, .34f, .38f, 1f);
        FishingUiTheme.DrawFlatRect(track, trackColor);
        FishingUiTheme.DrawFlatOutline(track, borderColor, 2f);

        GUI.Label(new Rect(track.x - 34f, track.y + 9f, 28f, 34f), "<", FishingUiTheme.Heading);
        GUI.Label(new Rect(track.xMax + 8f, track.y + 9f, 28f, 34f), ">", FishingUiTheme.Heading);

        float zoneSize = currentFish != null ? AdjustedReelZoneSize : .2f;
        float laneX = track.x + 3f;
        float laneWidth = track.width - 6f;
        Rect captureZone = new(laneX + reelPosition * laneWidth, track.y - 4f, zoneSize * laneWidth, track.height + 8f);
        FishingUiTheme.DrawFlatRect(captureZone, new Color(.96f, .96f, .95f, 1f));
        FishingUiTheme.DrawFlatOutline(captureZone, new Color(.72f, .73f, .76f, 1f), 2f);
        FishingUiTheme.ColoredLabel(new Rect(captureZone.xMax - 54f, captureZone.y + 4f, 46f, captureZone.height - 8f),
            "→", FishingUiTheme.Title, new Color(.43f, .44f, .46f, 1f));

        float fishX = Mathf.Lerp(laneX + 8f, laneX + laneWidth - 8f, fishPosition);
        Rect marker = new(fishX - 4f, track.y - 16f, 8f, track.height + 32f);
        FishingUiTheme.DrawFlatRect(marker, new Color(.48f, .50f, .56f, 1f));
        FishingUiTheme.DrawFlatOutline(marker, new Color(.30f, .32f, .38f, 1f), 1f);
        if (currentFish?.Icon != null)
        {
            var iconRect = new Rect(fishX - 22f, track.y - 58f, 44f, 38f);
            GUI.DrawTexture(iconRect, currentFish.Icon, ScaleMode.ScaleToFit, true);
        }
        else FishingUiTheme.DrawFlatRect(new Rect(fishX - 8f, track.y - 40f, 16f, 16f), currentFish.RarityColor);

        float barWidth = Mathf.Min(620f, track.width * .58f);
        Rect progressBar = new((screenWidth - barWidth) * .5f, track.y + 88f, barWidth, 18f);
        FishingUiTheme.DrawFlatRect(progressBar, new Color(.025f, .025f, .03f, .94f));
        FishingUiTheme.DrawFlatOutline(progressBar, borderColor, 2f);
        Rect progressFill = new(progressBar.x + 2f, progressBar.y + 2f,
            (progressBar.width - 4f) * Mathf.Clamp01(progress), progressBar.height - 4f);
        FishingUiTheme.DrawFlatRect(progressFill, new Color(.94f, .94f, .92f, 1f));

        Rect dangerLine = new(progressBar.x + 2f, progressBar.yMax + 5f,
            (progressBar.width - 4f) * Mathf.Clamp01(escape), 4f);
        FishingUiTheme.DrawFlatRect(dangerLine, FishingUiTheme.Danger);
        GUI.Label(new Rect(progressBar.x, progressBar.y - 25f, 180f, 22f),
            $"포획 {Mathf.RoundToInt(progress * 100)}%", FishingUiTheme.Small);
        GUI.Label(new Rect(progressBar.xMax - 180f, progressBar.y - 25f, 180f, 22f),
            $"도주 위험 {Mathf.RoundToInt(escape * 100)}%", FishingUiTheme.SmallCentered);
        GUI.Label(new Rect(progressBar.x, progressBar.yMax + 14f, progressBar.width, 24f),
            "SPACE  누르면 오른쪽 · 놓으면 왼쪽", FishingUiTheme.SmallCentered);
        GUI.color = previousColor;
    }

    private void UpdateRegionNotice(FishingSpotDefinition spot)
    {
        string regionId = spot != null ? spot.Id : "";
        if (string.IsNullOrEmpty(regionId))
        {
            lastRegionId = "";
            return;
        }
        if (regionId == lastRegionId) return;
        lastRegionId = regionId;
        string displayName = string.IsNullOrWhiteSpace(spot.DisplayName) ? "낚시터" : spot.DisplayName;
        string[] pieces = displayName.Split('·');
        regionTitle = pieces[0].Trim();
        regionSubtitle = pieces.Length > 1 ? pieces[1].Trim() + " 지역" : "낚시 지역";
        regionNoticeTimer = RegionNoticeDuration;
    }

    private void DrawRegionNotice(float screenWidth, FishingHudSettings hud)
    {
        if (regionNoticeTimer <= 0f || string.IsNullOrWhiteSpace(regionTitle)) return;
        float elapsed = RegionNoticeDuration - regionNoticeTimer;
        float alpha = Mathf.Clamp01(Mathf.Min(elapsed / .2f, regionNoticeTimer / .25f));
        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        var card = new Rect((screenWidth - 390f) * .5f, hud.ScreenMargin + 18f, 390f, 70f);
        FishingUiTheme.DrawPanel(card, true);
        GUI.Label(new Rect(card.x + 20f, card.y + 8f, card.width - 40f, 31f), regionTitle, FishingUiTheme.Title);
        FishingUiTheme.ColoredLabel(new Rect(card.x + 20f, card.y + 39f, card.width - 40f, 22f),
            regionSubtitle, FishingUiTheme.SmallCentered, FishingUiTheme.AccentBright);
        GUI.color = old;
    }

    private void DrawResultWindow(float screenWidth, float screenHeight)
    {
        float width = Mathf.Min(620f, screenWidth - 40f);
        float height = newFishResult ? 350f : 310f;
        float reveal = Mathf.Clamp01(resultUiReveal);
        width *= Mathf.Lerp(.94f, 1f, reveal);
        height *= Mathf.Lerp(.94f, 1f, reveal);
        Color previousColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, reveal);
        var panel = new Rect((screenWidth - width) * .5f, (screenHeight - height) * .5f, width, height);
        FishingUiTheme.DrawPanel(panel);
        Color resultColor = resultSuccess ? FishingUiTheme.AccentBright : FishingUiTheme.Danger;
        FishingUiTheme.DrawTitlePlate(new Rect(panel.x + 70f, panel.y - 16f, panel.width - 140f, 58f),
            resultSuccess ? (firstCatchResult ? "첫 물고기를 낚았다!" : "낚았다!") : "놓쳤다!", resultColor);
        float contentOffset = newFishResult ? 38f : 0f;
        if (resultSuccess && newFishResult)
        {
            float badgeScale = Mathf.Lerp(.55f, 1f, Mathf.SmoothStep(0f, 1f, newFishUiReveal));
            var badge = new Rect(panel.center.x - 78f * badgeScale, panel.y + 47f,
                156f * badgeScale, 34f * badgeScale);
            FishingUiTheme.DrawBadge(badge, "NEW!", FishingUiTheme.Gold);
            DrawNewFishSparkles(badge, newFishUiReveal);
        }
        if (resultSuccess && resultFish?.Icon != null)
        {
            FishingUiTheme.DrawTextureOrSwatch(new Rect(panel.x + 42f, panel.y + 74f + contentOffset, 122f, 122f), resultFish.Icon, resultFish.RarityColor);
            FishingUiTheme.DrawBadge(new Rect(panel.x + 42f, panel.y + 206f + contentOffset, 122f, 30f), resultFish.RarityName, resultFish.RarityColor);
        }
        GUI.Label(new Rect(panel.x + (resultSuccess ? 188f : 42f), panel.y + 72f + contentOffset,
            panel.width - (resultSuccess ? 228f : 84f), 150f), resultDetails, FishingUiTheme.BodyCentered);
        if (FishingUiTheme.StyledButton(new Rect(panel.x + panel.width * .5f - 82f, panel.yMax - 58f, 164f, 42f),
            resultSuccess ? "가방에 넣기" : "다시 도전", primary: true))
        {
            resultTimer = 0f;
            resultFish = null;
            resultDetails = "";
            newFishResult = false;
        }
        GUI.color = previousColor;
    }

    private static void DrawNewFishSparkles(Rect badge, float reveal)
    {
        float pulse = .65f + Mathf.Sin(Time.unscaledTime * 8f) * .25f;
        Color color = FishingUiTheme.Gold;
        color.a = Mathf.Clamp01(reveal) * pulse;
        DrawSparkle(new Vector2(badge.x - 18f, badge.center.y - 7f), 9f, color);
        DrawSparkle(new Vector2(badge.xMax + 18f, badge.center.y + 6f), 7f, color);
        DrawSparkle(new Vector2(badge.x + 18f, badge.y - 10f), 5f, color);
        DrawSparkle(new Vector2(badge.xMax - 14f, badge.yMax + 10f), 5f, color);
    }

    private static void DrawSparkle(Vector2 center, float size, Color color)
    {
        FishingUiTheme.DrawFlatRect(new Rect(center.x - 1f, center.y - size, 2f, size * 2f), color);
        FishingUiTheme.DrawFlatRect(new Rect(center.x - size, center.y - 1f, size * 2f, 2f), color);
    }

    private float SpotDifficulty => activeFishingSpot != null ? Mathf.Max(.5f, activeFishingSpot.DifficultyMultiplier) : 1f;
    private float EffectiveSpotDifficulty => Mathf.Lerp(1f, SpotDifficulty, .55f);
    private float AdjustedDirectionInterval => currentFish != null ? currentFish.DirectionChangeInterval / EffectiveSpotDifficulty * 1.15f : 1f;
    private FishingRodDefinition EquippedRod => rodInventory?.GetEquipped(rodCatalog);
    private float AdjustedReelZoneSize
    {
        get
        {
            if (currentFish == null) return .24f;
            float rodMultiplier = EquippedRod != null ? EquippedRod.CaptureZoneMultiplier : 1f;
            return Mathf.Clamp(currentFish.ReelZoneSize * 1.25f * rodMultiplier / Mathf.Sqrt(EffectiveSpotDifficulty), .18f, .48f);
        }
    }

    private void OnDestroy()
    {
        if (playerDebt != null && earlyGameProgress != null)
            playerDebt.DebtRepaid -= earlyGameProgress.NotifyDebtRepaid;
    }

    private void OnValidate()
    {
        FishingUiTheme.Configure(hudSettings);
    }
}
